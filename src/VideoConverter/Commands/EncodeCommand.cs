namespace VideoConverter.Commands
{
	using System;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Threading.Tasks;

	using Humanizer;

	using Spectre.Console;
	using Spectre.Console.Cli;

	using VideoConverter.Core.Assertions;
	using VideoConverter.Core.Models;
	using VideoConverter.Extensions;
	using VideoConverter.Options;
	using VideoConverter.Storage.Models;
	using VideoConverter.Storage.Repositories;

	using Xabe.FFmpeg;
	using Xabe.FFmpeg.Streams.SubtitleStream;

	public class EncodeCommand : AsyncCommand<EncodeOption>
	{
		private readonly CancellationToken cancellationToken;
		private readonly Configuration config;
		private readonly IAnsiConsole console;
		private readonly QueueRepository queueRepo;
		private readonly Random rand = new();

		public EncodeCommand(QueueRepository queueRepo, Configuration config, IAnsiConsole console)
		{
			this.queueRepo = queueRepo;
			this.config = config;
			var tokenSource = new CancellationTokenSource();
			cancellationToken = tokenSource.Token;

			Console.CancelKeyPress += (sender, e) =>
				{
					Console.WriteLine("Cancelling!!!");

					if (e.SpecialKey != ConsoleSpecialKey.ControlC)
					{
						return;
					}

					tokenSource.Cancel();
					e.Cancel = true;
				}
;
			this.console = console;
		}

		public override async Task<int> ExecuteAsync(CommandContext context, EncodeOption settings)
		{
			settings.AssertNotNull();

			Console.Clear();
			var failed = false;
			if (settings.IncludeFailing)
			{
				await queueRepo.ResetFailedQueueAsync().ConfigureAwait(false);
				await queueRepo.SaveChangesAsync().ConfigureAwait(false);
			}

			var count = await GetPendingCountAsync(0, settings.Indexes).ConfigureAwait(false);

			if (count == 0 && !settings.MonitorDatabase)
			{
				return 1;
			}

			var exitCode = 0;

			var mapperDb = config.MapperDatabase
				?? Path.Combine(
					Environment.GetFolderPath(
						Environment.SpecialFolder.LocalApplicationData),
					"VideoConverter",
					"storage.db"
			);
			var dbDirectory = Path.GetDirectoryName(mapperDb) ?? Environment.CurrentDirectory;
			var fileName = Path.GetFileName(mapperDb);

			using var monitor = new FileSystemWatcher(dbDirectory, fileName);
			var onHold = false;
			Console.CancelKeyPress += (sender, e) =>
				{
					if (e.SpecialKey != ConsoleSpecialKey.ControlC)
					{
						return;
					}

					monitor.EnableRaisingEvents = false;
					onHold = false;
				}
;
			var currentValue = 0.0;

			monitor.Changed += (sender, e) =>
			{
				if (e.ChangeType != WatcherChangeTypes.Changed)
				{
					return;
				}

				var originalHold = onHold;
				onHold = false;

				monitor.EnableRaisingEvents = false;
				int newCount;

#pragma warning disable IDE0045 // Convert to conditional expression
				if (count == 0 || originalHold)
				{
					newCount = GetPendingCountAsync(
						currentValue,
						settings.Indexes
					).GetAwaiter().GetResult();
				}
				else
				{
					newCount = GetPendingCountAsync(
						currentValue + 1,
						settings.Indexes)
					.GetAwaiter().GetResult();
				}
#pragma warning restore IDE0045 // Convert to conditional expression

				if (newCount != count)
				{
					count = newCount;
					currentValue = (double)count;
				}
				monitor.EnableRaisingEvents = true;
			};

		monitorStart:

			(var queue, var indexCount) = await GetNextQueueItemAsync(
				settings.Indexes,
				0
			).ConfigureAwait(false);
			monitor.EnableRaisingEvents = true;

			while (queue != null)
			{
				await console.Progress()
					.AutoClear(false)
					.Columns(new ProgressColumn[]
					{
								new SpinnerColumn(),
								new TaskDescriptionColumn(),
								new ProgressBarColumn(),
								new PercentageColumn(),
								new RemainingTimeColumn(),
						})
						.StartAsync(async ctx =>
						{
							var mainTask = ctx.AddTask("Processing...");
							mainTask.MaxValue = (double)count;
							mainTask.Increment(currentValue);
							const double parseStep = 17.0;
							const double hashStep = 50.0;
							const double moveStep = 25.0;
							var parseTask = ctx.AddTask("Aquiring information...");
							parseTask.MaxValue = 102;
							parseTask.StartTask();

							try
							{
								monitor.EnableRaisingEvents = false;
								await queueRepo.SaveChangesAsync().ConfigureAwait(false);
								monitor.EnableRaisingEvents = true;
							}
							catch (Exception ex)
							{
								console.WriteException(ex);
								throw;
							}

							if (cancellationToken.IsCancellationRequested)
							{
								exitCode = 1;
								return;
							}

							mainTask.Description = string.Format(
								CultureInfo.InvariantCulture,
								"[green]Processing [aqua]{0}[/] [fuchsia]{1:0} / {2}[/]...[/]",
								Path.GetFileNameWithoutExtension(queue.OutputPath),
								mainTask.Value + 1,
								count
								);
							mainTask.Increment(1.0);
							currentValue = mainTask.Value;
							mainTask.StopTask(); // Work around so it doesn't show as in progress

							var newFileName = Path.GetFileNameWithoutExtension(queue.OutputPath);

							try
							{
								var hashTask = ctx.AddTask("Calculating hash...");
								hashTask.StartTask();
								if (queue.OldHash is not string oldHash || oldHash is null)
								{
									oldHash = oldHash = await GetSHA1Async(queue.Path, cancellationToken).ConfigureAwait(false);
								}
								hashTask.Increment(hashStep);
								parseTask.Increment(parseStep);

								var exists = await queueRepo.FileExistsAsync(queue.Path, oldHash).ConfigureAwait(false);

								queue.OldHash = oldHash;

								parseTask.Increment(parseStep);

								if (exists && settings.IgnoreDuplicates)
								{
									queue.Status = QueueStatus.Completed;
									queue.StatusMessage = "Duplicate file...";
									await queueRepo.UpdateQueueAsync(queue).ConfigureAwait(false);
									monitor.EnableRaisingEvents = false;
									await queueRepo.SaveChangesAsync().ConfigureAwait(false);
									monitor.EnableRaisingEvents = true;
									return;
								}

								var mediaInfo = await FFmpeg.GetMediaInfo(queue.Path).ConfigureAwait(false);

								parseTask.Increment(parseStep);

								var directory = Path.GetDirectoryName(queue.OutputPath) ?? Environment.CurrentDirectory;
								if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
								{
									Directory.CreateDirectory(directory);
								}

								var streams = mediaInfo.Streams.Where(s => queue.Streams.Contains(s.Index));

								var conversion = FFmpeg.Conversions.New();

								foreach (var stream in streams)
								{
									if (stream is IVideoStream videoStream)
									{
										if (settings.UseEncodingCopy &&
											string.Equals(
												videoStream.Codec,
												queue.VideoCodec ?? config.VideoCodec,
												StringComparison.OrdinalIgnoreCase)
										)
										{
											videoStream.SetCodec(VideoCodec.copy);
										}
										else
										{
											videoStream.SetCodec(queue.VideoCodec ?? config.VideoCodec);
										}
									}
									else if (stream is IAudioStream audioStream)
									{
										if (settings.UseEncodingCopy &&
											string.Equals(
												audioStream.Codec,
												queue.AudioCodec ?? config.AudioCodec,
												StringComparison.OrdinalIgnoreCase)
										)
										{
											audioStream.SetCodec(AudioCodec.copy);
										}
										else
										{
											audioStream.SetCodec(queue.AudioCodec ?? config.AudioCodec);
										}
									}
									else if (stream is ISubtitleStream subtitleStream)
									{
										if (settings.UseEncodingCopy &&
											string.Equals(
												subtitleStream.Codec,
												queue.SubtitleCodec ?? config.SubtitleCodec,
												StringComparison.OrdinalIgnoreCase)
										)
										{
											subtitleStream.SetCodec(SubtitleCodec.copy);
										}
										else
										{
											subtitleStream.SetCodec(queue.SubtitleCodec ?? config.SubtitleCodec);
										}
									}

									conversion.AddStream(stream);
								}

								parseTask.Increment(parseStep);

								if (!Directory.Exists(config.WorkDirectory))
								{
									Directory.CreateDirectory(config.WorkDirectory);
								}

								var tempWorkPath = Path.Combine(
									config.WorkDirectory,
									Guid.NewGuid() + Path.GetExtension(queue.OutputPath)
								);

								var firstVideoStream = mediaInfo.VideoStreams.First();

								var parameters = string.Empty;

								parameters = !string.IsNullOrEmpty(queue.Parameters)
									? queue.Parameters
									: config.ExtraEncodingParameters;

								if (!parameters.ContainsInvariant("faststart") && !parameters.ContainsInvariant("movflags"))
								{
									parameters = "-movflags +faststart " + parameters;
								}

								if (queue.OutputPath.EndsWith(
										".mk3d",
										StringComparison.OrdinalIgnoreCase) ||
										queue.OutputPath.EndsWith(".mkv3d", StringComparison.Ordinal)
								)
								{
									conversion.SetOutputFormat(Format.matroska);
								}

								var parameters3D = string.Empty;
								var stereo3d = string.Empty;

								switch (queue.StereoMode)
								{
									case StereoScopicMode.Mono:
										if (!parameters.ContainsInvariant("v360") && !settings.NoStereoModeMetadata)
										{
											parameters3D = "-metadata:s:v stereo_mode=mono";
										}

										break;

									case StereoScopicMode.AboveBelowLeft:
										if (parameters.ContainsExact("v360"))
										{
											parameters = Regex.Replace(parameters, "v360=([^\\s\"])", "v360=$1:in_stereo=tb:out_stereo=tb");
										}
										else
										{
											parameters3D = "-vf \"stereo3d=tbl:tbl\"";
										}

										if (!settings.NoStereoModeMetadata)
										{
											parameters3D += " -metadata:s:v stereo_mode=top_bottom";
										}

										stereo3d = "tbl";
										break;

									case StereoScopicMode.AboveBelowRight:
										parameters3D = "-vf \"stereo3d=tbr:tbl\"";
										if (!settings.NoStereoModeMetadata)
										{
											parameters3D += " -metadata:s:v stereo_mode=top_bottom";
										}

										stereo3d = "tbl";
										break;

									case StereoScopicMode.AboveBelowLeftHalf:
										parameters3D = "-vf \"stereo3d=tb2l:tb2l\"";
										if (!settings.NoStereoModeMetadata)
										{
											parameters3D += " -metadata:s:v stereo_mode=top_bottom";
										}

										stereo3d = "tb2l";
										break;

									case StereoScopicMode.AboveBelowRightHalf:
										parameters3D = "-vf \"stereo3d=tb2r:tb2l\"";
										if (!settings.NoStereoModeMetadata)
										{
											parameters3D += " -metadata:s:v stereo_mode=top_bottom";
										}

										stereo3d = "tb2l";
										break;

									case StereoScopicMode.SideBySideLeft:
										if (parameters.ContainsExact("v360"))
										{
											parameters = Regex.Replace(parameters, "v360=([^\\s\"]+)", "v360=$1:in_stereo=sbs:out_stereo=sbs");
										}
										else
										{
											parameters3D = "-vf \"stereo3d=sbsl:sbsl\"";
										}

										if (!settings.NoStereoModeMetadata)
										{
											parameters3D += " -metadata:s:v stereo_mode=left_right";
										}

										stereo3d = "sbsl";
										break;

									case StereoScopicMode.SideBySideLeftHalf:
										parameters3D = "-vf \"stereo3d=sbs2l:sbs2l\"";
										if (!settings.NoStereoModeMetadata)
										{
											parameters3D += " -metadata:s:v stereo_mode=left_right";
										}

										stereo3d = "sbs2l";
										break;

									case StereoScopicMode.SideBySideRight:
										parameters3D = "-vf \"stereo3d=sbsr:sbsl\"";
										if (!settings.NoStereoModeMetadata)
										{
											parameters3D += " -metadata:s:v stereo_mode=left_right";
										}

										stereo3d = "sbsl";
										break;

									case StereoScopicMode.SideBySideRightHalf:
										parameters3D = "-vf \"stereo3d=sbs2r:sbs2l\"";
										if (!settings.NoStereoModeMetadata)
										{
											parameters3D += " -metadata:s:v stereo_mode=left_right";
										}

										stereo3d = "sbs2l";
										break;
								}

								if (!string.IsNullOrEmpty(queue.InputParameters))
								{
									conversion.AddParameter(queue.InputParameters, ParameterPosition.PreInput);
								}

								conversion.AddParameter($"{parameters} {parameters3D}")
									.SetOverwriteOutput(true)
									.SetOutput(tempWorkPath);

								parseTask.Increment(parseStep);
								//parseTask.StopTask();

								var result = conversion.Build();

								var encodeTask = ctx.AddTask("Encoding video...");
								encodeTask.StartTask();
								queue.Status = QueueStatus.Encoding;
								await queueRepo.UpdateQueueAsync(queue).ConfigureAwait(false);
								monitor.EnableRaisingEvents = false;
								await queueRepo.SaveChangesAsync().ConfigureAwait(false);
								monitor.EnableRaisingEvents = true;
								var lastSeconds = 0.0;

								conversion.OnProgress += (sender, args) =>
								{
									encodeTask.MaxValue = args.TotalLength.TotalSeconds;
									//encodeTask.Value = args.Duration.TotalSeconds;
									encodeTask.Increment(args.Duration.TotalSeconds - lastSeconds);
									lastSeconds = args.Duration.TotalSeconds;
								};

								try
								{
									var initialSize = new FileInfo(queue.Path).Length;
									await conversion.Start(cancellationToken).ConfigureAwait(false);
									//encodeTask.StopTask();

									if (cancellationToken.IsCancellationRequested)
									{
										failed = true;
										await queueRepo.UpdateQueueStatusAsync(
											queue.Id,
											QueueStatus.Pending,
											"Progress was cancelled by user"
										).ConfigureAwait(false);
										encodeTask.Description = "[grey]Encoding cancelled...[/]";
										if (File.Exists(tempWorkPath))
										{
											File.Delete(tempWorkPath);
										}
									}
									else
									{
										hashTask.StartTask();
										var newHash = await GetSHA1Async(tempWorkPath, CancellationToken.None).ConfigureAwait(false);
										hashTask.Increment(hashStep);
										queue.NewHash = newHash;
										//hashTask.StopTask();
										var moveTask = ctx.AddTask("Moving video to new location...");

										moveTask.StartTask();

										var isDuplicate = await queueRepo.FileExistsAsync(queue.Path, newHash).ConfigureAwait(false);
										queue.Status = QueueStatus.Completed;

										if (isDuplicate)
										{
											queue.StatusMessage = "Duplicate file...";
											if (settings.IgnoreDuplicates)
											{
												File.Delete(tempWorkPath);
											}
										}

										if (!isDuplicate || !settings.IgnoreDuplicates)
										{
											var newSize = new FileInfo(tempWorkPath).Length;
#pragma warning disable IDE0045 // Convert to conditional expression
											if (newSize > initialSize)
											{
												queue.StatusMessage =
													$"Lost {(newSize - initialSize).Bytes().Humanize("#.##", CultureInfo.CurrentCulture)}";
											}
											else
											{
												queue.StatusMessage = newSize < initialSize
													? $"Saved {(initialSize - newSize).Bytes().Humanize("#.##", CultureInfo.CurrentCulture)}"
													: "No loss or gain in size";
											}
#pragma warning restore IDE0045 // Convert to conditional expression
										}

										await queueRepo.UpdateQueueAsync(queue).ConfigureAwait(false);

										//this.queueRepo.UpdateQueueStatus(queue.Id, QueueStatus.Completed, statusMessage);
										if (!isDuplicate || !settings.IgnoreDuplicates)
										{
											moveTask.Increment(moveStep);

											mediaInfo = await FFmpeg.GetMediaInfo(tempWorkPath).ConfigureAwait(false);
											firstVideoStream = mediaInfo.VideoStreams.First();
											parseTask.Increment(parseStep);

											if (!queue.SkipThumbnails)
											{
												var newThumbPath = Path
													.ChangeExtension(
														queue.OutputPath,
														"-thumb.jpg")
													.Replace(
														".-thumb",
														"-thumb",
														StringComparison.Ordinal);
												var newFanArtPath = Path
													.ChangeExtension(
														queue.OutputPath,
														"-fanart.jpg")
													.Replace(
														".-fanart",
														"-fanart",
														StringComparison.Ordinal);

												if (File.Exists(newThumbPath))
												{
													File.Delete(newThumbPath);
												}

												if (File.Exists(newFanArtPath))
												{
													File.Delete(newFanArtPath);
												}

												var thumbnailAt = rand.Next((int)firstVideoStream.Duration.TotalMilliseconds + 1);
												var fanArtAt = rand.Next((int)firstVideoStream.Duration.TotalMilliseconds + 1);
												var thumbConversion = await FFmpeg.Conversions.FromSnippet.Snapshot(
													tempWorkPath,
													newThumbPath,
													TimeSpan.FromMilliseconds(thumbnailAt)
												).ConfigureAwait(false);
												var fanArtConversion = await FFmpeg.Conversions.FromSnippet.Snapshot(
													tempWorkPath,
													newFanArtPath,
													TimeSpan.FromMilliseconds(fanArtAt)
												).ConfigureAwait(false);
												if (!string.IsNullOrEmpty(stereo3d))
												{
													thumbConversion.AddParameter($"-vf \"stereo3d={stereo3d}:ml\"");
													fanArtConversion.AddParameter($"-vf \"stereo3d={stereo3d}:mr\"");
												}

												var thumbArgs = thumbConversion.Build();
												var fanArtArgs = fanArtConversion.Build();

												await Task.WhenAll(
													thumbConversion.Start(cancellationToken),
													fanArtConversion.Start(cancellationToken)
												).ConfigureAwait(false);
											}

											moveTask.Increment(moveStep);

											if (File.Exists(queue.OutputPath))
											{
												File.Delete(queue.OutputPath);
											}

											File.Move(tempWorkPath, queue.OutputPath);

											moveTask.Increment(moveStep);
										}
										else
										{
											moveTask.Increment(moveStep * 3);
											parseTask.Increment(parseStep);
										}

										if (settings.RemoveOldFiles)
										{
											if (queue.Path != queue.OutputPath && File.Exists(queue.Path))
											{
												File.Delete(queue.Path);
											}
										}
										moveTask.Increment(moveStep);
										/*moveTask.StopTask();
										parseTask.StopTask();*/
									}
								}
								catch (Exception ex)
								{
									failed = true;
									if (cancellationToken.IsCancellationRequested)
									{
										await queueRepo.UpdateQueueStatusAsync(
											queue.Id,
											QueueStatus.Pending,
											"Progress was cancelled by user"
										).ConfigureAwait(false);
									}
									else
									{
										await queueRepo.UpdateQueueStatusAsync(queue.Id, QueueStatus.Failed, ex).ConfigureAwait(false);
									}

									if (File.Exists(tempWorkPath))
									{
										File.Delete(tempWorkPath);
									}
								}
							}
							catch (Exception ex)
							{
								await queueRepo.UpdateQueueStatusAsync(queue.Id, QueueStatus.Failed, ex).ConfigureAwait(false);
								failed = true;
							}

							try
							{
								monitor.EnableRaisingEvents = false;
								await queueRepo.SaveChangesAsync().ConfigureAwait(false);
								monitor.EnableRaisingEvents = true;

								if (!cancellationToken.IsCancellationRequested)
								{
									(queue, indexCount) = await GetNextQueueItemAsync(settings.Indexes, indexCount).ConfigureAwait(false);
								}
							}
							catch (Exception ex)
							{
								if (queue?.Status == QueueStatus.Encoding)
								{
									queue.Status = QueueStatus.Pending;
									await queueRepo.UpdateQueueAsync(queue).ConfigureAwait(false);
									await queueRepo.SaveChangesAsync().ConfigureAwait(false);
								}

								console.WriteException(ex);
								exitCode = 1;
								return;
							}
						}).ConfigureAwait(false);

				if (exitCode != 0)
				{
					break;
				}
			}

			if (settings.MonitorDatabase)
			{
				onHold = true;
				while (onHold)
				{
					Thread.Sleep(TimeSpan.FromSeconds(5));
				}

				if (cancellationToken.IsCancellationRequested)
				{
					return 1;
				}

				onHold = false;

				goto monitorStart;
			}

			return exitCode = failed ? 1 : 0;
		}

		internal static async Task<string> GetSHA1Async(string file, CancellationToken cancellationToken)
		{
#pragma warning disable CA5350
			using var algo = SHA1.Create();
#pragma warning restore CA5350
			using var stream = File.OpenRead(file);
			var sb = new StringBuilder();

			var hashBytes = await algo.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);

			foreach (var b in hashBytes)
			{
				sb.AppendFormat(CultureInfo.InvariantCulture, "{0:x2}", b);
			}

			return sb.ToString();
		}

		private async Task<(FileQueue? queue, int count)> GetNextQueueItemAsync(int[] indexes, int indexCount)
		{
			var newIndex = indexCount;
			FileQueue? queue = null;
			if (indexes is not null && indexes.Length > 0)
			{
				if (newIndex < indexes.Length)
				{
					queue = await queueRepo.GetQueueItemAsync(indexes[newIndex]).ConfigureAwait(false);
					newIndex++;
				}
			}
			else
			{
				queue = await queueRepo.GetNextQueueItemAsync().ConfigureAwait(false);
			}

			return (queue, newIndex);
		}

		private async Task<int> GetPendingCountAsync(double currentTick, int[] indexes)
		{
			if (indexes is not null && indexes.Length > 0)
			{
				return indexes.Length;
			}

			var count = await queueRepo.GetPendingQueueCountAsync().ConfigureAwait(false);

			return count + (int)currentTick;
		}
	}
}
