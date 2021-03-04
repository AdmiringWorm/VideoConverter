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
	using VideoConverter.Core.Extensions;
	using VideoConverter.Core.Models;
	using VideoConverter.Core.Parsers;
	using VideoConverter.Extensions;
	using VideoConverter.Options;
	using VideoConverter.Prompts;
	using VideoConverter.Storage.Models;
	using VideoConverter.Storage.Repositories;
	using Xabe.FFmpeg;

	public sealed class AddFileCommand : AsyncCommand<AddFileOption>, IDisposable
	{
		private readonly Configuration config;
		private readonly IAnsiConsole console;
		private readonly AddCriteriaCommand criteriaCommand;
		private readonly EpisodeCriteriaRepository criteriaRepo;
		private readonly QueueRepository queueRepository;
		private readonly CancellationTokenSource tokenSource;

		public AddFileCommand(
			Configuration config,
			IAnsiConsole console,
			AddCriteriaCommand criteriaCommand,
			EpisodeCriteriaRepository criteriaRepo,
			QueueRepository queueRepository)
		{
			this.config = config;
			this.console = console;
			this.criteriaCommand = criteriaCommand;
			this.criteriaRepo = criteriaRepo;
			this.queueRepository = queueRepository;
			tokenSource = new CancellationTokenSource();
		}

		public override async Task<int> ExecuteAsync(CommandContext context, AddFileOption settings)
		{
			if (settings is null)
				throw new ArgumentNullException(nameof(settings));

			if (!string.IsNullOrWhiteSpace(settings.FileExtension))
				this.config.FileType = settings.FileExtension.GetExtensionFileType();

			try
			{
				Console.CancelKeyPress += CancelProcessing;

				foreach (var file in settings.Files.Select(f => Path.GetFullPath(f)))
				{
					if (this.tokenSource.IsCancellationRequested)
						break;

					if (!File.Exists(file))
					{
						this.console.MarkupLine("[red on black] ERROR: The file '[fuchsia]{0}[/]' do not exist.[/]", file.EscapeMarkup());
						return 1;
					}

					//var hash = await EncodeCommand.GetSHA1Async(file, tokenSource.Token).ConfigureAwait(false);
					var hash = string.Empty;

					var existingFile = await this.queueRepository.GetQueueItemAsync(file).ConfigureAwait(false);

					if (existingFile is not null && settings.IgnoreStatuses.Contains(existingFile.Status))
					{
						this.console.MarkupLine(
							"[yellow]WARNING: [fuchsia]'{0}'[/] exists with status [aqua]{1}[/]. Ignoring...[/]",
							file.EscapeMarkup(), existingFile.Status
						);
						continue;
					}
					else if (settings.IgnoreDuplicates || settings.RemoveDuplicates)
					{
						var fileExists = await this.queueRepository.FileExistsAsync(file, hash).ConfigureAwait(false);
						if (fileExists)
						{
							this.console.MarkupLine("[yellow]WARNING: [fuchsia]'{0}'[/] exists already exists. Ignoring...[/]", file.EscapeMarkup());
							continue;
						}
					}

					var mediaInfoTask = FFmpeg.GetMediaInfo(file, this.tokenSource.Token);

					bool isAccepted = false;
					EpisodeData? episodeData = null;

					var outputPath = settings.OutputPath;

					while (
						!this.tokenSource.Token.IsCancellationRequested &&
						!settings.ReEncode &&
						string.IsNullOrEmpty(outputPath) &&
						!isAccepted
					)
					{
						episodeData = FileParser.ParseEpisode(file);

						var relativePath = settings.OutputDir ?? Environment.CurrentDirectory;

						if (settings.ReEncode)
							break;
						if (episodeData is null)
						{
							var path = this.console.Prompt(
								new TextPrompt<string>(
									$"File '[fuchsia]{file.EscapeMarkup()}[/]'...\n" +
									"We were unable to extract necessary information. Please input the location of the file to save,\n" +
									$"[grey][[Optional]][/] Relative to the path ([fuchsia]{relativePath.EscapeMarkup()}[/]):"
								)
								.AllowEmpty()
							);

							if (string.IsNullOrWhiteSpace(path))
								break;
							else if (path.StartsWith('/'))
								outputPath = path.Trim();
							else
								outputPath = Path.Combine(relativePath, path.Trim());

							break;
						}

						if (this.tokenSource.Token.IsCancellationRequested)
							break;

						await UpdateEpisodeDataAsync(episodeData, relativePath).ConfigureAwait(false);

						if (this.tokenSource.Token.IsCancellationRequested)
							break;

						isAccepted = await AskAcceptableAsync(context, episodeData, this.tokenSource.Token).ConfigureAwait(false);
					}

					if (this.tokenSource.Token.IsCancellationRequested)
						break;

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

					if (this.tokenSource.Token.IsCancellationRequested)
						break;

					var streams = new List<int>();

					if (videoStreams.Count > 1)
					{
						var prompt = new MultiSelectionPrompt<(int, string, string, TimeSpan)>()
							.Title("Which video streams do you wish to use ([fuchsia]Select no streams to use all streams)[/]?")
							.NotRequired();

						foreach (var stream in videoStreams)
						{
							prompt = prompt.AddChoice(
								(
									stream.Index,
									stream.Codec,
									stream.Framerate + "fps",
									stream.Duration
								)
							);
						}

						var selectedStreams = this.console.Prompt(prompt).Select(i => i.Item1);
						if (!selectedStreams.Any())
						{
							streams.AddRange(mediaInfo.VideoStreams.Select(i => i.Index));
						}
						else
						{
							streams.AddRange(selectedStreams);
						}

						if (this.tokenSource.IsCancellationRequested)
							break;
					}
					else if (videoStreams.Count == 0)
					{
						streams.AddRange(mediaInfo.VideoStreams.Select(i => i.Index));
					}
					else
					{
						streams.AddRange(videoStreams.Select(i => i.Index));
					}

					if (audioStreams.Count > 1)
					{
						var prompt = new MultiSelectionPrompt<(int, string, string, int, string)>()
							.Title("Which audio streams to you wish to use ([fuchsia]Select no streams to use all streams)[/]?")
							.NotRequired();

						foreach (var stream in audioStreams)
						{
							CultureInfo? ci = null;
							try
							{
								ci = new CultureInfo(stream.Language);
							}
							catch
							{
								// Ignore any excetpion on purpose
							}
							prompt.AddChoice(
								(
									stream.Index,
									ci?.EnglishName ?? stream.Language,
									stream.Codec,
									stream.Channels,
									stream.SampleRate + " Hz"
								)
							);
						}

						var selectedStreams = this.console.Prompt(prompt).Select(i => i.Item1);
						if (!selectedStreams.Any())
						{
							streams.AddRange(mediaInfo.AudioStreams.Select(i => i.Index));
						}
						else
						{
							streams.AddRange(selectedStreams);
						}

						if (this.tokenSource.Token.IsCancellationRequested)
							break;
					}
					else
					{
						streams.AddRange(audioStreams.Select(a => a.Index));
					}

					if (subtitleStreams.Count > 1)
					{
						var prompt = new MultiSelectionPrompt<(int, string, string, string)>()
							.Title("Which subtitle streams do you wish to use ([fuchsia]Select no streams to use all streams)[/]?")
							.NotRequired();

						foreach (var stream in subtitleStreams)
						{
							CultureInfo? ci = null;
							try
							{
								ci = new CultureInfo(stream.Language);
							}
							catch
							{
								// Ignore any excetpion on purpose
							}

							prompt = prompt.AddChoice(
								(
									stream.Index,
									ci?.EnglishName ?? stream.Language,
									stream.Title,
									stream.Codec
								)
							);
						}

						var selectedStreams = this.console.Prompt(prompt).Select(i => i.Item1);

						if (!selectedStreams.Any())
						{
							streams.AddRange(mediaInfo.VideoStreams.Select(i => i.Index));
						}
						else
						{
							streams.AddRange(selectedStreams);
						}

						if (this.tokenSource.IsCancellationRequested)
							break;
					}
					else
					{
						streams.AddRange(subtitleStreams.Select(s => s.Index));
					}

					if (this.tokenSource.IsCancellationRequested)
						break;

					var fileExist = await this.queueRepository.FileExistsAsync(file.Normalize(), null).ConfigureAwait(false);

					if (fileExist)
					{
						if (settings.RemoveDuplicates)
						{
							this.console.WriteLine(
								$"The file '{file}' is a duplicate of an existing file. Removing...",
								new Style(Color.Green)
							);
							File.Delete(file);
							continue;
						}
						else if (settings.IgnoreDuplicates)
						{
							this.console.WriteLine(
								$"WARNING: The file '{file}' is a duplicate of an existing file. Ignoring...",
								new Style(Color.Yellow)
							);
							continue;
						}
						else
						{
							this.console.MarkupLine(
								"[yellow]WARNING: Found duplicate file, ignoring or removing duplicates have not been specified. Continuing[/]"
							);
						}
					}

					if (!string.IsNullOrEmpty(outputPath))
					{
						outputPath = Path.GetFullPath(outputPath);
						if (string.IsNullOrEmpty(Path.GetExtension(outputPath)))
						{
							outputPath = "." + (settings.FileExtension ?? this.config.FileType.GetFileExtension()).TrimStart('.');
						}
					}
					else if (!string.IsNullOrEmpty(settings.OutputDir))
					{
						string rootDir = Path.GetFullPath(settings.OutputDir!);
						if (!Directory.Exists(rootDir))
							Directory.CreateDirectory(rootDir);
						string directory = rootDir;
						if (episodeData is not null)
						{
							if (settings.FileExtension is not null)
								episodeData.Container = settings.FileExtension.GetExtensionFileType();
							else
								episodeData.Container = this.config.FileType;

							directory = GetOutputDir(episodeData, directory);

							outputPath = Path.Combine(directory, RemoveInvalidChars(episodeData.ToString()));
						}
						else if (settings.ReEncode)
						{
							var extension = settings.FileExtension ?? this.config.FileType.GetFileExtension();

							outputPath = Path.Combine(directory, Path.GetFileName(file));
							outputPath = Path.ChangeExtension(outputPath, extension);
						}
						else
						{
							this.console.MarkupLine(
								"[yellow on black] ERROR: No information was found in the data, and no output path is specified. " +
								"This should not happen, please report this error to the developers.[/]"
							);
							await queueRepository.AbortChangesAsync().ConfigureAwait(false);
							return 1;
						}
					}
					else if (settings.ReEncode)
					{
						var extension = settings.FileExtension ?? this.config.FileType.GetFileExtension();
						outputPath = Path.ChangeExtension(outputPath ?? file, extension);
					}
					else
					{
						this.console.MarkupLine(
							"[yellow on black] ERROR: No information was found in the data, and no output path is specified. " +
							"This should not happen, please report this error to the developers.[/]"
						);
						await queueRepository.AbortChangesAsync().ConfigureAwait(false);
						return 1;
					}

					FileQueue queueItem;

					if (existingFile is not null)
					{
						queueItem = existingFile;
					}
					else
					{
						queueItem = new FileQueue
						{
							Path = file.Normalize()
						};
					}
					queueItem.OldHash = hash;

					var audioCodec = settings.AudioCodec ?? this.config.AudioCodec;
					var videoCodec = settings.VideoCodec ?? this.config.VideoCodec;
					var subtitleCodec = settings.SubtitleCodec ?? this.config.SubtitleCodec;

					if (settings.UseEncodingCopy)
					{
						if (!string.Equals(audioCodec, "copy", StringComparison.OrdinalIgnoreCase) &&
							mediaInfo.AudioStreams
								.Where(a => streams.Contains(a.Index))
								.All(a => string.Equals(a.Codec, audioCodec, StringComparison.OrdinalIgnoreCase))
						)
						{
							audioCodec = "copy";
						}

						if (!string.Equals(videoCodec, "copy", StringComparison.OrdinalIgnoreCase) &&
							mediaInfo.VideoStreams
								.Where(v => streams.Contains(v.Index))
								.All(v => string.Equals(v.Codec, videoCodec, StringComparison.OrdinalIgnoreCase))
						)
						{
							videoCodec = "copy";
						}

						if (!string.Equals(subtitleCodec, "copy", StringComparison.OrdinalIgnoreCase) &&
							mediaInfo.SubtitleStreams
								.Where(s => streams.Contains(s.Index))
								.All(s => string.Equals(s.Codec, subtitleCodec, StringComparison.OrdinalIgnoreCase))
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
											: this.config.ExtraEncodingParameters;
					queueItem.Status = QueueStatus.Pending;
					queueItem.StatusMessage = string.Empty;
					queueItem.StereoMode = settings.StereoMode;
					queueItem.Streams = streams;
					queueItem.SubtitleCodec = subtitleCodec;
					queueItem.VideoCodec = videoCodec;

					if (!string.IsNullOrWhiteSpace(settings.Repeat))
					{
						if (TimeSpan.TryParse(settings.Repeat, CultureInfo.InvariantCulture, out var ts))
						{
							int repeatTimes = 0;
							if (videoStreams[0].Duration.TotalMilliseconds > 0)
							{
								repeatTimes = (int)Math.Ceiling(ts.TotalMilliseconds / videoStreams[0].Duration.TotalMilliseconds);
							}
							else if (videoStreams[0].Duration.TotalSeconds > 0)
							{
								repeatTimes = (int)Math.Ceiling(ts.TotalSeconds / videoStreams[0].Duration.TotalSeconds);
							}
							else
							{
								this.console.MarkupLine(
									"[yellow]WARNING: We were unable to detect how long the video is, " +
									"unable to specify repeat vairable automatically[/]"
								);
								this.console.MarkupLine(
									"[yellow]         Assuming a length of 1 second, and will repeat {0}", "time[/]"
										.ToQuantity((int)ts.TotalSeconds)
								);
								repeatTimes = (int)ts.TotalSeconds;
							}

							if (repeatTimes > 1)
								queueItem.InputParameters = "-stream_loop " + repeatTimes;
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
							this.console.MarkupLine("Added or updated '[fuchsia]{0}[/]' to the encoding queue!", file.EscapeMarkup());
						}
						catch
						{
							this.console.WriteLine($"Adder or updated '{file}' to the encoding queue!", new Style(Color.Fuchsia));
						}
					}
					else
					{
						await queueRepository.AbortChangesAsync().ConfigureAwait(false);
						this.console.WriteLine(
							$"WARNING: Unable to update '{file}'. Encoding have already started on the file!",
							new Style(Color.Yellow, Color.Black)
						);
					}
				}
			}
			catch (Exception ex)
			{
				this.console.WriteException(ex);
				this.tokenSource.Cancel();
			}

			return this.tokenSource.Token.IsCancellationRequested ? 1 : 0;
		}

		private void CancelProcessing(object? sender, ConsoleCancelEventArgs e)
		{
			if (e.SpecialKey == ConsoleSpecialKey.ControlC)
			{
				this.console.MarkupLine("We are cancelling all processing once current prompts are complete, please wait for cleanup to be complete!");

				e.Cancel = true;
				this.tokenSource.Cancel();
			}
		}

		private async Task<bool> AskAcceptableAsync(CommandContext context, Core.Models.EpisodeData episodeData, CancellationToken cancellationToken)
		{
			DisplayEpisodeData(episodeData);

			var prompt = this.console.Prompt(new YesNoPrompt("Do this information look correct?"));

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

			this.console.MarkupLine("Okay then, please enter the correct information!");
			this.console.MarkupLine("[italic]Press {{Enter}} with an empty line to use default values[/]");
			this.console.WriteLine();

			var settings = new AddCriteriaOption
			{
				FilePath = episodeData.FileName,
				SeriesName = this.console.Prompt(new TextPrompt<string>("Name of Series").DefaultValue(episodeData.Series.EscapeMarkup())),
				SeasonNumber = this.console.Prompt(new TextPrompt<int>("Season Number").DefaultValue(episodeData.SeasonNumber ?? 1)),
				EpisodeNumber = this.console.Prompt(new TextPrompt<int>("Episode Number").DefaultValue(episodeData.EpisodeNumber))
			};

			if (cancellationToken.IsCancellationRequested)
			{
				return true;
			}

			await criteriaCommand.ExecuteAsync(context, settings).ConfigureAwait(false);

			return false;
		}

		private static string RemoveInvalidChars(string text)
		{
			var sb = new StringBuilder();
			const string invalidChars = "<>:\"/\\|?*";

			foreach (var ch in text)
			{
				if (!invalidChars.Contains(ch))
					sb.Append(ch);
				else
					sb.Append(' ');
			}

			return Regex.Replace(sb.ToString(), @"\s{2,}", " ", RegexOptions.Compiled).Trim();
		}

		private void DisplayEpisodeData(Core.Models.EpisodeData episodeData)
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
			this.console.Render(panel);
		}

		private async Task UpdateEpisodeDataAsync(Core.Models.EpisodeData episodeData, string relativeParentDir)
		{
			await foreach (var criteria in this.criteriaRepo.GetEpisodeCriteriasAsync(episodeData.Series))
			{
				if (criteria.UpdateEpisodeData(episodeData) && criteria.SeriesName is not null)
					break;
			}

			if (episodeData.SeasonNumber is null)
				episodeData.SeasonNumber = 1;

			if (!config.IncludeFansubber)
				episodeData.Fansubber = null;

			var existingEpisodeData = GetMatchingEpisodeData(episodeData, GetOutputDir(episodeData, relativeParentDir));
			if (existingEpisodeData is null)
			{
				return;
			}

			episodeData.EpisodeName = existingEpisodeData.EpisodeName;
			episodeData.Fansubber = existingEpisodeData.Fansubber;
		}

		private static string GetOutputDir(EpisodeData episodeData, string relativeParentDir)
		{
			var newDirectory = relativeParentDir;
			var seriesName = RemoveInvalidChars(episodeData.Series);
			newDirectory = Path.Combine(newDirectory, seriesName);
			if (!Directory.Exists(newDirectory))
			{
				var yearDir = Directory.EnumerateDirectories(relativeParentDir, seriesName + " (*)").FirstOrDefault();
				if (yearDir is not null)
					newDirectory = yearDir;
			}

			if (episodeData.SeasonNumber is not null)
			{
				if (episodeData.SeasonNumber == 0)
				{
					newDirectory = Path.Combine(newDirectory, "Specials");
				}
				else
				{
					newDirectory = Path.Combine(newDirectory, "Season " + episodeData.SeasonNumber);
				}
			}

			return newDirectory;
		}

		private static EpisodeData? GetMatchingEpisodeData(EpisodeData episodeData, string outputDir)
		{
			if (!Directory.Exists(outputDir))
			{
				return null;
			}

			var trimmedName = RemoveInvalidChars(episodeData.Series);

			foreach (var file in AddDirectoryCommand.FindVideoFiles(outputDir, false).Select(f => FileParser.ParseEpisode(f)).Where(ed => ed is not null))
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

		public void Dispose()
		{
			this.tokenSource?.Dispose();
		}
	}
}
