using System.Text.RegularExpressions;
using System.Security.Cryptography;
namespace VideoConverter.Commands
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Humanizer;
	using ShellProgressBar;
	using Spectre.Cli;
	using Spectre.Console;
	using System.Security.Cryptography;
	using System.Text;
	using VideoConverter.Core.Models;
	using VideoConverter.Options;
	using VideoConverter.Storage.Models;
	using VideoConverter.Storage.Repositories;
	using Xabe.FFmpeg;
	using System.Globalization;
	using Xabe.FFmpeg.Streams.SubtitleStream;

	public class EncodeCommand : AsyncCommand<EncodeOption>
	{
		private readonly QueueRepository queueRepo;
		private readonly Configuration config;
		private readonly CancellationToken cancellationToken;
		private readonly IAnsiConsole console;
		private readonly Random rand = new Random();

		public EncodeCommand(QueueRepository queueRepo, Configuration config, IAnsiConsole console)
		{
			this.queueRepo = queueRepo;
			this.config = config;
			var tokenSource = new CancellationTokenSource();
			this.cancellationToken = tokenSource.Token;

			Console.CancelKeyPress += (sender, e) =>
			{
				Console.WriteLine("Cancelling!!!");
				if (e.SpecialKey == ConsoleSpecialKey.ControlC)
				{
					tokenSource.Cancel();
					e.Cancel = true;
				}
			};
			this.console = console;
		}

		public override async Task<int> ExecuteAsync(CommandContext context, EncodeOption settings)
		{
			if (settings is null)
				throw new ArgumentNullException(nameof(settings));

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

			var progressOptions = new ProgressBarOptions
			{
				ProgressCharacter = '─',
				EnableTaskBarProgress = RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
				ForegroundColor = ConsoleColor.Cyan,
				ForegroundColorDone = ConsoleColor.DarkCyan,
			};

			var childOptions = new ProgressBarOptions
			{
				ProgressCharacter = progressOptions.ProgressCharacter,
				ForegroundColor = ConsoleColor.Magenta,
				ProgressBarOnBottom = progressOptions.ProgressBarOnBottom
			};
			var encodingOptions = new ProgressBarOptions
			{
				ProgressCharacter = childOptions.ProgressCharacter,
				ForegroundColor = ConsoleColor.DarkYellow,
				ProgressBarOnBottom = childOptions.ProgressBarOnBottom,
				CollapseWhenFinished = true,
			};

			using var pbMain = new ProgressBar(count, string.Empty, progressOptions);

			var mapperDb = this.config.MapperDatabase ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoConverter", "storage.db");
			var dbDirectory = Path.GetDirectoryName(mapperDb) ?? Environment.CurrentDirectory;
			var fileName = Path.GetFileName(mapperDb);

			using var monitor = new FileSystemWatcher(dbDirectory, fileName);
			var onHold = false;
			Console.CancelKeyPress += (sender, e) =>
			{
				if (e.SpecialKey == ConsoleSpecialKey.ControlC)
				{
					monitor.EnableRaisingEvents = false;
					onHold = false;
				}
			};

			monitor.Changed += (sender, e) =>
			{
				if (e.ChangeType != WatcherChangeTypes.Changed)
					return;
				var originalHold = onHold;
				onHold = false;

				monitor.EnableRaisingEvents = false;
				int newCount;
				if (count == 0 || originalHold)
					newCount = GetPendingCountAsync(pbMain.CurrentTick, settings.Indexes).GetAwaiter().GetResult();
				else
					newCount = GetPendingCountAsync(pbMain.CurrentTick + 1, settings.Indexes).GetAwaiter().GetResult();

				if (newCount != count)
				{
					count = newCount;
					pbMain.MaxTicks = count;
					pbMain.Tick(pbMain.CurrentTick, $"Processing file {pbMain.CurrentTick + 1} / {pbMain.MaxTicks}");
				}
				monitor.EnableRaisingEvents = true;
			};

		monitorStart:

			(FileQueue? queue, int indexCount) = await GetNextQueueItemAsync(settings.Indexes, 0).ConfigureAwait(false);
			monitor.EnableRaisingEvents = true;

			while (queue != null)
			{
				try
				{
					monitor.EnableRaisingEvents = false;
					await queueRepo.SaveChangesAsync().ConfigureAwait(false);
					monitor.EnableRaisingEvents = true;
				}
				catch (Exception ex)
				{
					this.console.WriteException(ex);
					throw;
				}

				if (cancellationToken.IsCancellationRequested)
				{
					return 1;
				}

				pbMain.Message = $"Processing file {pbMain.CurrentTick + 1} / {pbMain.MaxTicks}";

				var newFileName = Path.GetFileNameWithoutExtension(queue.OutputPath);

				int maxSteps = 6;
				if (settings.RemoveOldFiles)
					maxSteps++;

				using var stepChild = pbMain.Spawn(maxSteps, "Collecting information", childOptions);

				try
				{
					var oldHash = await GetSHA1Async(queue.Path, cancellationToken).ConfigureAwait(false);

					var exists = await queueRepo.FileExistsAsync(queue.Path, oldHash).ConfigureAwait(false);
					queue.OldHash = oldHash;

					if (exists && settings.IgnoreDuplicates)
					{
						queue.Status = QueueStatus.Completed;
						queue.StatusMessage = "Duplicate file...";
						stepChild.ForegroundColor = ConsoleColor.DarkGray;
						stepChild.Tick($"Duplicate file '{Path.GetFileNameWithoutExtension(queue.Path)}'");
						await queueRepo.UpdateQueueAsync(queue).ConfigureAwait(false);
						monitor.EnableRaisingEvents = false;
						await queueRepo.SaveChangesAsync().ConfigureAwait(false);
						monitor.EnableRaisingEvents = true;
						continue;
					}

					var mediaInfo = await FFmpeg.GetMediaInfo(queue.Path).ConfigureAwait(false);

					stepChild.Tick("Setting encoding options");

					var directory = Path.GetDirectoryName(queue.OutputPath) ?? Environment.CurrentDirectory;
					if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
						Directory.CreateDirectory(directory);

					var streams = mediaInfo.Streams.Where(s => queue.Streams.Contains(s.Index));

					var conversion = FFmpeg.Conversions.New();

					foreach (var stream in streams)
					{
						if (stream is IVideoStream videoStream)
						{
							if (settings.UseEncodingCopy && string.Equals(videoStream.Codec, queue.VideoCodec ?? this.config.VideoCodec, StringComparison.OrdinalIgnoreCase))
								videoStream.SetCodec(VideoCodec.copy);
							else
								videoStream.SetCodec(queue.VideoCodec ?? this.config.VideoCodec);
						}
						else if (stream is IAudioStream audioStream)
						{
							if (settings.UseEncodingCopy && string.Equals(audioStream.Codec, queue.AudioCodec ?? this.config.AudioCodec, StringComparison.OrdinalIgnoreCase))
								audioStream.SetCodec(AudioCodec.copy);
							else
								audioStream.SetCodec(queue.AudioCodec ?? this.config.AudioCodec);
						}
						else if (stream is ISubtitleStream subtitleStream)
						{
							if (settings.UseEncodingCopy && string.Equals(subtitleStream.Codec, queue.SubtitleCodec ?? this.config.SubtitleCodec, StringComparison.OrdinalIgnoreCase))
								subtitleStream.SetCodec(SubtitleCodec.copy);
							else
								subtitleStream.SetCodec(queue.SubtitleCodec ?? this.config.SubtitleCodec);
						}

						conversion.AddStream(stream);
					}

					if (!Directory.Exists(this.config.WorkDirectory))
						Directory.CreateDirectory(this.config.WorkDirectory);

					var tempWorkPath = Path.Combine(this.config.WorkDirectory, Guid.NewGuid() + Path.GetExtension(queue.OutputPath));

					var firstVideoStream = mediaInfo.VideoStreams.First();

					string parameters = string.Empty;

					if (!string.IsNullOrEmpty(queue.Parameters))
						parameters = queue.Parameters;
					else
						parameters = this.config.ExtraEncodingParameters;

					if (!parameters.Contains("faststart") && !parameters.Contains("movflags"))
						parameters = "-movflags +faststart " + parameters;

					if (queue.OutputPath.EndsWith(".mk3d", StringComparison.OrdinalIgnoreCase) || queue.OutputPath.EndsWith(".mkv3d", StringComparison.Ordinal))
						conversion.SetOutputFormat(Format.matroska);

					string parameters3D = string.Empty;
					string stereo3d = string.Empty;

					switch (queue.StereoMode)
					{
						case StereoScopicMode.Mono:
							if (!parameters.Contains("v360") && !settings.NoStereoModeMetadata)
								parameters3D = "-metadata:s:v stereo_mode=mono";
							break;
						case StereoScopicMode.AboveBelowLeft:
							if (parameters.Contains("v360"))
								parameters = Regex.Replace(parameters, "v360=([^\\s\"])", "v360=$1:in_stereo=tb:out_stereo=tb");
							else
								parameters3D = "-vf \"stereo3d=tbl:tbl\"";
							if (!settings.NoStereoModeMetadata)
								parameters3D += " -metadata:s:v stereo_mode=top_bottom";
							stereo3d = "tbl";
							break;
						case StereoScopicMode.AboveBelowRight:
							parameters3D = "-vf \"stereo3d=tbr:tbl\"";
							if (!settings.NoStereoModeMetadata)
								parameters3D += " -metadata:s:v stereo_mode=top_bottom";
							stereo3d = "tbl";
							break;
						case StereoScopicMode.AboveBelowLeftHalf:
							parameters3D = "-vf \"stereo3d=tb2l:tb2l\"";
							if (!settings.NoStereoModeMetadata)
								parameters3D += " -metadata:s:v stereo_mode=top_bottom";
							stereo3d = "tb2l";
							break;
						case StereoScopicMode.AboveBelowRightHalf:
							parameters3D = "-vf \"stereo3d=tb2r:tb2l\"";
							if (!settings.NoStereoModeMetadata)
								parameters3D += " -metadata:s:v stereo_mode=top_bottom";
							stereo3d = "tb2l";
							break;
						case StereoScopicMode.SideBySideLeft:
							if (parameters.Contains("v360"))
								parameters = Regex.Replace(parameters, "v360=([^\\s\"]+)", "v360=$1:in_stereo=sbs:out_stereo=sbs");
							else
								parameters3D = "-vf \"stereo3d=sbsl:sbsl\"";
							if (!settings.NoStereoModeMetadata)
								parameters3D += " -metadata:s:v stereo_mode=left_right";
							stereo3d = "sbsl";
							break;
						case StereoScopicMode.SideBySideLeftHalf:
							parameters3D = "-vf \"stereo3d=sbs2l:sbs2l\"";
							if (!settings.NoStereoModeMetadata)
								parameters3D += " -metadata:s:v stereo_mode=left_right";
							stereo3d = "sbs2l";
							break;
						case StereoScopicMode.SideBySideRight:
							parameters3D = "-vf \"stereo3d=sbsr:sbsl\"";
							if (!settings.NoStereoModeMetadata)
								parameters3D += " -metadata:s:v stereo_mode=left_right";
							stereo3d = "sbsl";
							break;
						case StereoScopicMode.SideBySideRightHalf:
							parameters3D = "-vf \"stereo3d=sbs2r:sbs2l\"";
							if (!settings.NoStereoModeMetadata)
								parameters3D += " -metadata:s:v stereo_mode=left_right";
							stereo3d = "sbs2l";
							break;
					}

					if (!string.IsNullOrEmpty(queue.InputParameters))
						conversion.AddParameter(queue.InputParameters, ParameterPosition.PreInput);

					conversion.AddParameter($"{parameters} {parameters3D}")
						.SetOverwriteOutput(true)
						.SetOutput(tempWorkPath);

					var result = conversion.Build();

					stepChild.Tick($"Encoding '{newFileName}'...");
					queue.Status = QueueStatus.Encoding;
					await queueRepo.UpdateQueueAsync(queue).ConfigureAwait(false);
					monitor.EnableRaisingEvents = false;
					await queueRepo.SaveChangesAsync().ConfigureAwait(false);
					monitor.EnableRaisingEvents = true;

					using var encodingPb = stepChild.Spawn(100, "Initialized", encodingOptions);
					conversion.OnProgress += (sender, args) =>
					{
						encodingPb.MaxTicks = (int)args.TotalLength.TotalSeconds;
						encodingPb.Tick((int)args.Duration.TotalSeconds, $"Completed: {args.Duration} / Total: {args.TotalLength}");
					};

					try
					{
						var initialSize = new FileInfo(queue.Path).Length;
						await conversion.Start(cancellationToken).ConfigureAwait(false);

						if (cancellationToken.IsCancellationRequested)
						{
							failed = true;
							await queueRepo.UpdateQueueStatusAsync(queue.Id, QueueStatus.Pending, "Progress was cancelled by user").ConfigureAwait(false);
							stepChild.ForegroundColor = ConsoleColor.DarkGray;
							stepChild.Tick(stepChild.CurrentTick, $"Encoding Cancelled for '{newFileName}'");
							if (File.Exists(tempWorkPath))
								File.Delete(tempWorkPath);
						}
						else
						{
							stepChild.Tick($"Calculating new hash for '{newFileName}'");
							var newHash = await GetSHA1Async(tempWorkPath, CancellationToken.None).ConfigureAwait(false);
							queue.NewHash = newHash;

							var isDuplicate = await queueRepo.FileExistsAsync(queue.Path, newHash).ConfigureAwait(false);
							queue.Status = QueueStatus.Completed;

							if (isDuplicate)
							{
								queue.StatusMessage = "Duplicate file...";
								if (settings.IgnoreDuplicates)
								{
									stepChild.Tick("Removing duplicate file...");
									File.Delete(tempWorkPath);
									stepChild.ForegroundColor = ConsoleColor.DarkGray;
								}
							}

							if (!isDuplicate || !settings.IgnoreDuplicates)
							{
								var newSize = new FileInfo(tempWorkPath).Length;
								if (newSize > initialSize)
									queue.StatusMessage = $"Lost {(newSize - initialSize).Bytes().Humanize("#.##", CultureInfo.CurrentCulture)}";
								else if (newSize < initialSize)
									queue.StatusMessage = $"Saved {(initialSize - newSize).Bytes().Humanize("#.##", CultureInfo.CurrentCulture)}";
								else
									queue.StatusMessage = "No loss or gain in size";
							}

							await queueRepo.UpdateQueueAsync(queue).ConfigureAwait(false);

							//this.queueRepo.UpdateQueueStatus(queue.Id, QueueStatus.Completed, statusMessage);
							encodingPb.Tick(encodingPb.MaxTicks, "Completed");
							if (!isDuplicate || !settings.IgnoreDuplicates)
							{
								var newThumbPath = Path.ChangeExtension(queue.OutputPath, "-thumb.jpg").Replace(".-thumb", "-thumb");
								var newFanArtPath = Path.ChangeExtension(queue.OutputPath, "-fanart.jpg").Replace(".-fanart", "-fanart");

								stepChild.Tick($"Creating snapshot of '{newFileName}'");

								if (File.Exists(newThumbPath))
									File.Delete(newThumbPath);

								if (File.Exists(newFanArtPath))
									File.Delete(newFanArtPath);

								mediaInfo = await FFmpeg.GetMediaInfo(tempWorkPath).ConfigureAwait(false);
								firstVideoStream = mediaInfo.VideoStreams.First();

								var thumbnailAt = this.rand.Next((int)firstVideoStream.Duration.TotalMilliseconds + 1);
								var fanArtAt = this.rand.Next((int)firstVideoStream.Duration.TotalMilliseconds + 1);
								var thumbConversion = await FFmpeg.Conversions.FromSnippet.Snapshot(tempWorkPath, newThumbPath, TimeSpan.FromMilliseconds(thumbnailAt)).ConfigureAwait(false);
								var fanArtConversion = await FFmpeg.Conversions.FromSnippet.Snapshot(tempWorkPath, newFanArtPath, TimeSpan.FromMilliseconds(fanArtAt)).ConfigureAwait(false);
								if (!string.IsNullOrEmpty(stereo3d))
								{
									thumbConversion.AddParameter($"-vf \"stereo3d={stereo3d}:ml\"");
									fanArtConversion.AddParameter($"-vf \"stereo3d={stereo3d}:mr\"");
								}

								var thumbArgs = thumbConversion.Build();
								var fanArtArgs = fanArtConversion.Build();

								await Task.WhenAll(thumbConversion.Start(cancellationToken), fanArtConversion.Start(cancellationToken)).ConfigureAwait(false);

								stepChild.Tick($"Moving encoded file to new location '{newFileName}'");

								if (File.Exists(queue.OutputPath))
									File.Delete(queue.OutputPath);

								File.Move(tempWorkPath, queue.OutputPath);
								stepChild.ForegroundColor = ConsoleColor.DarkGreen;
							}

							if (settings.RemoveOldFiles)
							{
								stepChild.Tick($"Removing old encoded file '{Path.GetFileNameWithoutExtension(queue.Path)}");
								if (queue.Path != queue.OutputPath && File.Exists(queue.Path))
									File.Delete(queue.Path);
							}

							stepChild.Tick($"Encoding completed for '{newFileName}'");
						}
					}
					catch (Exception ex)
					{
						failed = true;
						if (cancellationToken.IsCancellationRequested)
						{
							await queueRepo.UpdateQueueStatusAsync(queue.Id, QueueStatus.Pending, "Progress was cancelled by user").ConfigureAwait(false);
							stepChild.ForegroundColor = ConsoleColor.DarkGray;
							stepChild.Tick(stepChild.CurrentTick, $"Encoding Cancelled for '{newFileName}'");
						}
						else
						{
							stepChild.ForegroundColor = ConsoleColor.DarkRed;
							await queueRepo.UpdateQueueStatusAsync(queue.Id, QueueStatus.Failed, ex).ConfigureAwait(false);
							stepChild.Tick(stepChild.CurrentTick, $"Encoding failed for '{newFileName}'");
						}

						if (File.Exists(tempWorkPath))
							File.Delete(tempWorkPath);
					}
				}
				catch (Exception ex)
				{
					stepChild.ForegroundColor = ConsoleColor.DarkRed;
					await queueRepo.UpdateQueueStatusAsync(queue.Id, QueueStatus.Failed, ex).ConfigureAwait(false);
					stepChild.Tick(stepChild.CurrentTick, $"Encoding failed for '{newFileName}'");
					failed = true;
				}

				try
				{
					monitor.EnableRaisingEvents = false;
					await queueRepo.SaveChangesAsync().ConfigureAwait(false);
					monitor.EnableRaisingEvents = true;

					pbMain.Tick($"Completed file {pbMain.CurrentTick + 1} / {pbMain.MaxTicks}");

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

					this.console.WriteException(ex);
					return 1;
				}
			}

			if (settings.MonitorDatabase)
			{
				onHold = true;
				while (onHold)
					Thread.Sleep(TimeSpan.FromSeconds(5));

				if (cancellationToken.IsCancellationRequested)
					return 1;

				onHold = false;

				goto monitorStart;
			}

			return failed ? 1 : 0;
		}

		private async Task<int> GetPendingCountAsync(int currentTick, int[] indexes)
		{
			if (indexes is not null && indexes.Length > 0)
				return indexes.Length;

			var count = await queueRepo.GetPendingQueueCountAsync().ConfigureAwait(false);

			return count + currentTick;
		}

		private async Task<(FileQueue? queue, int count)> GetNextQueueItemAsync(int[] indexes, int indexCount)
		{
			int newIndex = indexCount;
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

		private static async Task<string> GetSHA1Async(string file, CancellationToken cancellationToken)
		{
			using var algo = SHA1.Create();
			using var stream = File.OpenRead(file);
			var sb = new StringBuilder();

			var hashBytes = await algo.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);

			foreach (var b in hashBytes)
			{
				sb.AppendFormat(CultureInfo.InvariantCulture, "{0:x2}", b);
			}

			return sb.ToString();
		}
	}
}
