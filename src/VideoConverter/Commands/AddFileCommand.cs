namespace VideoConverter.Commands
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Threading.Tasks;

	using Humanizer;

	using Spectre.Console;
	using Spectre.Console.Cli;

	using VideoConverter.Core.Assertions;
	using VideoConverter.Core.Extensions;
	using VideoConverter.Core.IO;
	using VideoConverter.Core.Models;
	using VideoConverter.Core.Parsers;
	using VideoConverter.Core.Services;
	using VideoConverter.Extensions;
	using VideoConverter.Options;
	using VideoConverter.Prompts;
	using VideoConverter.Prompts.Streams;
	using VideoConverter.Storage.Models;
	using VideoConverter.Storage.Repositories;

	using Xabe.FFmpeg;

	public sealed partial class AddFileCommand : AsyncCommand<AddFileOption>, IDisposable
	{
		private readonly ConverterConfiguration config;
		private readonly IAnsiConsole console;
		private readonly AddCriteriaCommand criteriaCommand;
		private readonly EpisodeCriteriaRepository criteriaRepo;
		private readonly IHashProvider hashProvider;
		private readonly IIOHelpers ioHelpers;
		private readonly QueueRepository queueRepository;
		private readonly CancellationTokenSource tokenSource;

		public AddFileCommand(
			ConverterConfiguration config,
			IAnsiConsole console,
			AddCriteriaCommand criteriaCommand,
			EpisodeCriteriaRepository criteriaRepo,
			QueueRepository queueRepository,
			IHashProvider hashProvider,
			IIOHelpers ioHelpers)
		{
			this.config = config;
			this.console = console;
			this.criteriaCommand = criteriaCommand;
			this.criteriaRepo = criteriaRepo;
			this.queueRepository = queueRepository;
			this.hashProvider = hashProvider;
			tokenSource = new CancellationTokenSource();
			this.ioHelpers = ioHelpers;
		}

		public void Dispose()
		{
			tokenSource?.Dispose();
		}

		public override async Task<int> ExecuteAsync(CommandContext context, AddFileOption settings)
		{
			settings.AssertNotNull();

			if (!string.IsNullOrWhiteSpace(settings.FileExtension))
			{
				config.FileType = settings.FileExtension.GetExtensionFileType();
			}

			try
			{
				Console.CancelKeyPress += CancelProcessing;

				foreach (var file in settings.Files.Select(f => Path.GetFullPath(f)))
				{
					if (tokenSource.IsCancellationRequested)
					{
						break;
					}

					if (!ioHelpers.FileExists(file))
					{
						console.MarkupLine("[red on black] ERROR: The file '[fuchsia]{0}[/]' do not exist.[/]", file.EscapeMarkup());
						return 1;
					}

					var hash = hashProvider.ComputeHash(file);

					var existingFile = await queueRepository.GetQueueItemAsync(file).ConfigureAwait(false);

					if (existingFile is not null && settings.IgnoreStatuses.Contains(existingFile.Status))
					{
						if (ioHelpers.FileExists(existingFile.OutputPath))
						{
							if (settings.RemoveDuplicates)
							{
								console.MarkupLine(
									"[yellow]WARNING: [fuchsia]'{0}'[/] exists with status [aqua]{1}[/]. Removing duplicate...[/]",
									file.EscapeMarkup(),
									existingFile.Status);
								ioHelpers.FileRemove(file);
							}
							else
							{
								console.MarkupLine(
									"[yellow]WARNING: [fuchsia]'{0}'[/] exists with status [aqua]{1}[/]. Ignoring duplicate...[/]",
									file.EscapeMarkup(),
									existingFile.Status);
							}

							continue;
						}
					}
					else if (settings.IgnoreDuplicates || settings.RemoveDuplicates)
					{
						var fileExists = await queueRepository.FileExistsAsync(file, hash).ConfigureAwait(false);
						if (fileExists)
						{
							if (settings.RemoveDuplicates)
							{
								console.MarkupLine(
									"[yellow]WARNING: [fuchsia]'{0}'[/] already exists. Removing duplication...[/]",
									file.EscapeMarkup());
								ioHelpers.FileRemove(file);
							}
							else
							{
								console.MarkupLine(
									"[yellow]WARNING: [fuchsia]'{0}'[/] already exists. Ignoring duplicate...[/]",
									file.EscapeMarkup());
							}
							continue;
						}
					}

					var mediaInfoTask = FFmpeg.GetMediaInfo(file, tokenSource.Token);

					var isAccepted = false;
					EpisodeData? episodeData = null;

					var outputPath = settings.OutputPath;

					while (
						!tokenSource.Token.IsCancellationRequested &&
						!settings.ReEncode &&
						string.IsNullOrEmpty(outputPath) &&
						!isAccepted
					)
					{
						episodeData = FileParser.ParseEpisode(file);

						var relativePath = settings.OutputDir ?? Environment.CurrentDirectory;

						if (settings.ReEncode)
						{
							break;
						}

						if (episodeData is null)
						{
							var path = await TextPromptOptionalAsync<string>(
								console,
								$"File '[fuchsia]{file.EscapeMarkup()}[/]'...\n" +
								"We were unable to extract necessary information. Please input the location of the file to save,\n" +
								$"[grey][[Optional]][/] Relative to the path ([fuchsia]{relativePath.EscapeMarkup()}[/]):",
								tokenSource.Token);

							if (string.IsNullOrWhiteSpace(path))
							{
								break;
							}
							else
							{
								outputPath = path.StartsWith('/')
									? path.Trim()
									: Path.Combine(relativePath, path.Trim());
							}

							break;
						}

						if (tokenSource.Token.IsCancellationRequested)
						{
							break;
						}

						var fansubber = episodeData.Fansubber;

						var foundFile = await UpdateEpisodeDataAsync(episodeData, relativePath).ConfigureAwait(false);

						if (foundFile && !string.IsNullOrWhiteSpace(fansubber) && config.Fansubbers.Count > 0)
						{
							var fansubberConfig = config.Fansubbers
								.Find(f =>
									string.Equals(
										f.Name,
										fansubber,
										StringComparison.OrdinalIgnoreCase));

							if (fansubberConfig?.IgnoreOnDuplicates == true)
							{
								console.WriteLine(
									$"WARNING: The file '{file}' already exist, and the fansubber '{fansubber}' is ignored for existing files...",
									new Style(Color.Yellow));
								episodeData.Series = "SKIP";
								break;
							}
						}

						if (tokenSource.Token.IsCancellationRequested)
						{
							break;
						}

						isAccepted = await AskAcceptableAsync(context, episodeData, tokenSource.Token).ConfigureAwait(false);
					}

					if (tokenSource.Token.IsCancellationRequested)
					{
						break;
					}

					if ((episodeData is null && string.IsNullOrEmpty(outputPath) && !settings.ReEncode) ||
						(episodeData is not null && episodeData.Series == "SKIP"))
					{
						continue;
					}

					var mediaInfo = await mediaInfoTask.ConfigureAwait(false);
					var videoStreams = mediaInfo.VideoStreams.Where(
						v => !string.Equals(v.Codec, "mjpeg", StringComparison.OrdinalIgnoreCase)
					).ToList();
					var audioStreams = mediaInfo.AudioStreams.ToList();
					var subtitleStreams = mediaInfo.SubtitleStreams.ToList();

					if (tokenSource.Token.IsCancellationRequested)
					{
						break;
					}

					var streams = new List<IStream>();

					if (!await AddStreamsAsync(
						streams,
						mediaInfo.VideoStreams
							.Where(v =>
								!string.Equals(
									v.Codec, "mjpeg",
									StringComparison.OrdinalIgnoreCase)
								)
							.ToArray(),
						tokenSource.Token
						)
					)
					{
						streams.AddRange(mediaInfo.VideoStreams);
					}
					else if (tokenSource.Token.IsCancellationRequested)
					{
						break;
					}

					await AddStreamsAsync(streams, mediaInfo.AudioStreams, tokenSource.Token);

					if (tokenSource.Token.IsCancellationRequested)
					{
						break;
					}

					await AddStreamsAsync(streams, mediaInfo.SubtitleStreams, tokenSource.Token);

					if (tokenSource.IsCancellationRequested)
					{
						break;
					}

					var fileExist = await queueRepository.FileExistsAsync(file.Normalize(), null).ConfigureAwait(false);

					if (fileExist)
					{
						if (settings.RemoveDuplicates)
						{
							console.WriteLine(
								$"The file '{file}' is a duplicate of an existing file. Removing...",
								new Style(Color.Green)
							);
							ioHelpers.FileRemove(file);
							continue;
						}
						else if (settings.IgnoreDuplicates)
						{
							console.WriteLine(
								$"WARNING: The file '{file}' is a duplicate of an existing file. Ignoring...",
								new Style(Color.Yellow)
							);
							continue;
						}
						else
						{
							console.MarkupLine(
								"[yellow]WARNING: Found duplicate file, ignoring or removing duplicates have not been specified. Continuing[/]"
							);
						}
					}

					if (!string.IsNullOrEmpty(outputPath))
					{
						outputPath = Path.GetFullPath(outputPath);
						if (string.IsNullOrEmpty(Path.GetExtension(outputPath)))
						{
							outputPath = "." + (settings.FileExtension ?? config.FileType.GetFileExtension()).TrimStart('.');
						}
					}
					else if (!string.IsNullOrEmpty(settings.OutputDir))
					{
						var rootDir = Path.GetFullPath(settings.OutputDir!);
						ioHelpers.EnsureDirectory(rootDir);

						var directory = rootDir;

						if (episodeData is not null)
						{
							episodeData.Container = settings.FileExtension is not null
								? settings.FileExtension.GetExtensionFileType()
								: config.FileType;

							directory = GetOutputDir(episodeData, directory);

							outputPath = Path.Combine(directory, RemoveInvalidChars(episodeData.ToString()));
						}
						else if (settings.ReEncode)
						{
							var extension = settings.FileExtension ?? config.FileType.GetFileExtension();

							outputPath = Path.Combine(directory, Path.GetFileName(file));
							outputPath = Path.ChangeExtension(outputPath, extension);
						}
						else
						{
							console.MarkupLine(
								"[yellow on black] ERROR: No information was found in the data, and no output path is specified. " +
								"This should not happen, please report this error to the developers.[/]"
							);
							await queueRepository.AbortChangesAsync().ConfigureAwait(false);
							return 1;
						}
					}
					else if (settings.ReEncode)
					{
						var extension = settings.FileExtension ?? config.FileType.GetFileExtension();
						outputPath = Path.ChangeExtension(outputPath ?? file, extension);
					}
					else
					{
						console.MarkupLine(
							"[yellow on black] ERROR: No information was found in the data, and no output path is specified. " +
							"This should not happen, please report this error to the developers.[/]"
						);
						await queueRepository.AbortChangesAsync().ConfigureAwait(false);
						return 1;
					}

					var queueItem = existingFile ?? new FileQueue
					{
						Path = file.Normalize()
					};
					queueItem.OldHash = hash;
					queueItem.SkipThumbnails = settings.SkipThumbnails;

					var audioCodec = settings.AudioCodec ?? config.AudioCodec;
					var videoCodec = settings.VideoCodec ?? config.VideoCodec;
					var subtitleCodec = settings.SubtitleCodec ?? config.SubtitleCodec;

					if (settings.UseEncodingCopy)
					{
						if (!string.Equals(audioCodec, "copy", StringComparison.OrdinalIgnoreCase) &&
							mediaInfo.AudioStreams
								.Where(a => streams.Any(s => s.Index == a.Index))
								.All(a =>
									string.Equals(a.Codec, audioCodec, StringComparison.OrdinalIgnoreCase) ||
									string.Equals("lib" + a.Codec, audioCodec, StringComparison.OrdinalIgnoreCase) ||
									string.Equals(a.Codec, "lib" + audioCodec, StringComparison.OrdinalIgnoreCase)
								)
						)
						{
							audioCodec = "copy";
						}

						if (!string.Equals(videoCodec, "copy", StringComparison.OrdinalIgnoreCase) &&
							mediaInfo.VideoStreams
								.Where(v => streams.Any(s => s.Index == v.Index))
								.All(v =>
									string.Equals(v.Codec, videoCodec, StringComparison.OrdinalIgnoreCase) ||
									string.Equals("lib" + v.Codec, videoCodec, StringComparison.OrdinalIgnoreCase) ||
									string.Equals(v.Codec, "lib" + videoCodec, StringComparison.OrdinalIgnoreCase)
								)
						)
						{
							videoCodec = "copy";
						}

						if (!string.Equals(subtitleCodec, "copy", StringComparison.OrdinalIgnoreCase) &&
							mediaInfo.SubtitleStreams
								.Where(ss => streams.Any(s => s.Index == ss.Index))
								.All(s =>
									string.Equals(s.Codec, subtitleCodec, StringComparison.OrdinalIgnoreCase) ||
									string.Equals("lib" + s.Codec, subtitleCodec, StringComparison.OrdinalIgnoreCase) ||
									string.Equals(s.Codec, "lib" + subtitleCodec, StringComparison.OrdinalIgnoreCase)
								)
						)
						{
							subtitleCodec = "copy";
						}
					}

					queueItem.AudioCodec = audioCodec;
					queueItem.NewHash = string.Empty;
					queueItem.OutputPath = outputPath!;
					queueItem.Parameters = settings.Parameters.Any(p => !string.IsNullOrEmpty(p))
											? string.Join(' ', settings.Parameters.Where(p => !string.IsNullOrEmpty(p)))
											: config.ExtraEncodingParameters;
					queueItem.Status = QueueStatus.Pending;
					queueItem.StatusMessage = string.Empty;
					queueItem.StereoMode = settings.StereoMode;
					queueItem.Streams = streams.ConvertAll(s => s.Index);
					queueItem.SubtitleCodec = subtitleCodec;
					queueItem.VideoCodec = videoCodec;

					if (!string.IsNullOrWhiteSpace(settings.Repeat))
					{
						if (TimeSpan.TryParse(settings.Repeat, CultureInfo.InvariantCulture, out var ts))
						{
							var repeatTimes = 0;
							var firstStream = videoStreams.FirstOrDefault();

							if (firstStream?.Duration.TotalMilliseconds > 0)
							{
								repeatTimes = (int)Math.Ceiling(ts.TotalMilliseconds / firstStream.Duration.TotalMilliseconds);
							}
							else if (firstStream?.Duration.TotalSeconds > 0)
							{
								repeatTimes = (int)Math.Ceiling(ts.TotalSeconds / firstStream.Duration.TotalSeconds);
							}
							else
							{
								console.MarkupLine(
									"[yellow]WARNING: We were unable to detect how long the video is, " +
									"unable to specify repeat vairable automatically[/]"
								);
								console.MarkupLine(
									"[yellow]         Assuming a length of 1 second, and will repeat {0}", "time[/]"
										.ToQuantity((int)ts.TotalSeconds)
								);
								repeatTimes = (int)ts.TotalSeconds;
							}

							if (repeatTimes > 1)
							{
								queueItem.InputParameters = "-stream_loop " + repeatTimes;
							}
						}
						else if (int.TryParse(settings.Repeat, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) && i > 1)
						{
							queueItem.InputParameters = "-stream_loop " + settings.Repeat;
						}
					}

					if (await queueRepository.AddToQueueAsync(queueItem).ConfigureAwait(false))
					{
						await queueRepository.SaveChangesAsync().ConfigureAwait(false);
						try
						{
							console.MarkupLine(
								"Added or updated '[fuchsia]{0}[/]' to the encoding queue!",
								file.EscapeMarkup());
						}
						catch
						{
							console.WriteLine(
								$"Added or updated '{file.EscapeMarkup()}' to the encoding queue!",
								new Style(Color.Fuchsia));
						}
					}
					else
					{
						await queueRepository.AbortChangesAsync().ConfigureAwait(false);
						console.WriteLine(
							$"WARNING: Unable to update '{file.EscapeMarkup()}'. Encoding have already started on the file!",
							new Style(Color.Yellow, Color.Black)
						);
					}
				}
			}
			catch (Exception ex)
			{
				console.WriteException(ex);
				tokenSource.Cancel();
			}

			return tokenSource.Token.IsCancellationRequested ? 1 : 0;
		}

		private static string RemoveInvalidChars(string text)
		{
			var sb = new StringBuilder();
			const string invalidChars = "<>:\"/\\|?*";

			foreach (var ch in text)
			{
				if (!invalidChars.Contains(ch, StringComparison.Ordinal))
				{
					sb.Append(ch);
				}
				else
				{
					sb.Append(' ');
				}
			}

			return WhiteSpaceRegex().Replace(sb.ToString(), " ").Trim();
		}

		private static Task<TType> TextPromptAsync<TType>(
			IAnsiConsole console,
			string title,
			TType defaultValue,
			CancellationToken cancellationToken = default)
		{
			var textPrompt = new TextPrompt<TType>(title)
				.DefaultValue(defaultValue);

			return textPrompt.ShowAsync(console, cancellationToken);
		}

		private static Task<TType> TextPromptOptionalAsync<TType>(
			IAnsiConsole console,
			string title,
			CancellationToken cancellationToken = default)
		{
			var textPrompt = new TextPrompt<TType>(title)
				.AllowEmpty();

			return textPrompt.ShowAsync(console, cancellationToken);
		}

		[GeneratedRegex("\\s{2,}", RegexOptions.Compiled)]
		private static partial Regex WhiteSpaceRegex();

		private async Task<bool> AddStreamsAsync<T>(
			List<IStream> addedStreams,
			IEnumerable<T> streams,
			CancellationToken cancellationToken = default)
												where T : IStream
		{
			var streamCount = streams.Count();
			if (streamCount == 0)
			{
				return false;
			}
			if (streamCount == 1)
			{
				addedStreams.Add(streams.First());
				return true;
			}

			if (streams is IEnumerable<IVideoStream> videoStreams)
			{
				var prompt = new VideoStreamPrompt()
					.AddStreams(videoStreams);
				addedStreams.AddRange(await prompt.ShowAsync(console, cancellationToken));
			}
			else if (streams is IEnumerable<IAudioStream> audioStreams)
			{
				var prompt = new AudioStreamPrompt()
					.AddStreams(audioStreams);
				addedStreams.AddRange(await prompt.ShowAsync(console, cancellationToken));
			}
			else if (streams is IEnumerable<ISubtitleStream> subtitleStreams)
			{
				var prompt = new SubtitleStreamPrompt()
					.AddStreams(subtitleStreams);
				addedStreams.AddRange(await prompt.ShowAsync(console, cancellationToken));
			}

			return true;
		}

		private async Task<bool> AskAcceptableAsync(
			CommandContext context,
			EpisodeData episodeData,
			CancellationToken cancellationToken)
		{
			DisplayEpisodeData(episodeData);

			var consolePrompt = new YesNoPrompt("Do this information look correct?");
			var prompt = await consolePrompt.ShowAsync(console, cancellationToken).ConfigureAwait(false);

			if (cancellationToken.IsCancellationRequested)
			{
				return true;
			}

			if (prompt == PromptResponse.Skip)
			{
				episodeData.Series = "SKIP";
				return true;
			}
			else if (prompt == PromptResponse.Yes)
			{
				return true;
			}

			console.MarkupLine("Okay then, please enter the correct information!");
			console.MarkupLine("[italic]Press {{Enter}} with an empty line to use default values[/]");
			console.WriteLine();

			var settings = new AddCriteriaOption
			{
				FilePath = episodeData.FileName,
				SeriesName = await TextPromptAsync(
					console,
					"Name of Series",
					episodeData.Series.EscapeMarkup(),
					tokenSource.Token),
				SeasonNumber = await TextPromptAsync(
					console,
					"Season Number",
					episodeData.SeasonNumber ?? 1,
					tokenSource.Token),
				EpisodeNumber = await TextPromptAsync(
					console,
					"Episode Number",
					episodeData.EpisodeNumber,
					tokenSource.Token)
			};

			if (cancellationToken.IsCancellationRequested)
			{
				return true;
			}

			await criteriaCommand.ExecuteAsync(context, settings).ConfigureAwait(false);

			return false;
		}

		private void CancelProcessing(object? sender, ConsoleCancelEventArgs e)
		{
			if (e.SpecialKey != ConsoleSpecialKey.ControlC)
			{
				return;
			}

			console.MarkupLine(
				"We are cancelling all processing once current prompts are complete, please wait for cleanup to be complete!");

			e.Cancel = true;
			tokenSource.Cancel();
		}

		private void DisplayEpisodeData(EpisodeData episodeData)
		{
			var grid = new Grid()
				.AddColumn(new GridColumn().RightAligned())
				.AddColumn(new GridColumn().PadLeft(1))
				.AddEmptyRow()
				.AddRow(
					new Text("Series Name:"),
					episodeData.Series.GetAnsiText()
				)
				.AddRow(
					new Text("Season:"),
					episodeData.SeasonNumber.GetAnsiText()
				)
				.AddRow(
					new Text("Episode:"),
					episodeData.EpisodeNumber.GetAnsiText()
				)
				.AddRow(
					new Text("Old File Name:"),
					episodeData.FileName.GetAnsiText()
				)
				.AddRow(
					new Text("New File Name:"),
					episodeData.GetAnsiText()
				);

			var panel = new Panel(grid)
				.Header("New Episode Data", Justify.Center);
			console.Write(panel);
		}

		private EpisodeData? GetMatchingEpisodeData(EpisodeData episodeData, string outputDir)
		{
			if (!ioHelpers.DirectoryExists(outputDir))
			{
				return null;
			}

			var trimmedName = RemoveInvalidChars(episodeData.Series);

			foreach (var file in ioHelpers.EnumerateMovieFiles(outputDir, false)
				.Select(f => FileParser.ParseEpisode(f)).Where(ed => ed is not null))
			{
				var fileTrimmed = RemoveInvalidChars(file!.Series);
				if (!string.Equals(trimmedName, fileTrimmed, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				else if (file.EpisodeNumber == episodeData.EpisodeNumber && file.SeasonNumber == episodeData.SeasonNumber)
				{
					return file;
				}
			}

			return null;
		}

		private string GetOutputDir(EpisodeData episodeData, string relativeParentDir)
		{
			var newDirectory = relativeParentDir;
			var seriesName = RemoveInvalidChars(episodeData.Series);
			newDirectory = Path.Combine(newDirectory, seriesName);
			if (!ioHelpers.DirectoryExists(newDirectory))
			{
				var yearDir = ioHelpers.EnumerateDirectories(relativeParentDir, seriesName + " (*)").FirstOrDefault();
				if (yearDir is not null)
				{
					newDirectory = yearDir;
				}
			}

			if (episodeData.SeasonNumber is not null)
			{
				newDirectory = episodeData.SeasonNumber == 0
					? Path.Combine(newDirectory, "Specials")
					: Path.Combine(newDirectory, "Season " + episodeData.SeasonNumber);
			}

			return newDirectory;
		}

		private async Task<bool> UpdateEpisodeDataAsync(Core.Models.EpisodeData episodeData, string relativeParentDir)
		{
			await foreach (var criteria in criteriaRepo.GetEpisodeCriteriasAsync(episodeData.Series))
			{
				if (criteria.UpdateEpisodeData(episodeData) && criteria.SeriesName is not null)
				{
					break;
				}
			}

			episodeData.SeasonNumber ??= 1;

			if (!config.IncludeFansubber)
			{
				episodeData.Fansubber = null;
			}

			var existingEpisodeData = GetMatchingEpisodeData(
				episodeData,
				GetOutputDir(episodeData, relativeParentDir));
			if (existingEpisodeData is null)
			{
				return false;
			}

			episodeData.EpisodeName = existingEpisodeData.EpisodeName;
			episodeData.Fansubber = existingEpisodeData.Fansubber;

			return true;
		}
	}
}
