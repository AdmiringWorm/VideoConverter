using System.Threading;
using System.Security.Cryptography;
using System.Drawing;
namespace VideoConverter.Commands
{
	using System.Globalization;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading.Tasks;
	using Spectre.Cli;
	using Spectre.Console;
	using VideoConverter.Core.Extensions;
	using VideoConverter.Core.Models;
	using VideoConverter.Core.Parsers;
	using VideoConverter.Extensions;
	using VideoConverter.Options;
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

		public AddFileCommand(Configuration config,
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

					var existingFile = await this.queueRepository.GetQueueItemAsync(file).ConfigureAwait(false);

					if (existingFile is not null && settings.IgnoreStatuses.Contains(existingFile.Status))
					{
						this.console.MarkupLine("[yellow]WARNING: [fuchsia]'{0}'[/] exists with status [aqua]{1}[/]. Ignoring...[/]", file.EscapeMarkup(), existingFile.Status);
						continue;
					}

					var mediaInfoTask = FFmpeg.GetMediaInfo(file, this.tokenSource.Token);

					bool isAccepted = false;
					EpisodeData? episodeData = null;

					var outputPath = settings.OutputPath;

					while (!this.tokenSource.Token.IsCancellationRequested && string.IsNullOrEmpty(outputPath) && !isAccepted)
					{
						episodeData = FileParser.ParseEpisode(file);

						if (settings.ReEncode)
							break;
						if (episodeData is null)
						{
							var relativePath = settings.OutputDir ?? Environment.CurrentDirectory;
							this.console.MarkupLine("File '[fuchsia]{0}[/]'...", file.EscapeMarkup());
							this.console.MarkupLine("We were unable to extract necessary information. Please input the location of the file to save,");
							this.console.MarkupLine("Relative to the path ([fuchsia]{0}[/]), or press {{Enter}} to skip the file.", relativePath.EscapeMarkup());
							var path = Console.ReadLine();
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

						await UpdateEpisodeDataAsync(episodeData).ConfigureAwait(false);

						if (this.tokenSource.Token.IsCancellationRequested)
							break;

						isAccepted = await AskAcceptableAsync(context, episodeData).ConfigureAwait(false);
					}

					if (this.tokenSource.Token.IsCancellationRequested)
						break;

					if ((episodeData is null && string.IsNullOrEmpty(outputPath) && !settings.ReEncode) ||
						(episodeData is not null && episodeData.Series == "SKIP"))
					{
						continue;
					}

					var mediaInfo = await mediaInfoTask.ConfigureAwait(false);
					var videoStreams = mediaInfo.VideoStreams.Where(v => !string.Equals(v.Codec, "mjpeg", StringComparison.OrdinalIgnoreCase)).ToList();
					var audioStreams = mediaInfo.AudioStreams.ToList();
					var subtitleStreams = mediaInfo.SubtitleStreams.ToList();

					if (this.tokenSource.Token.IsCancellationRequested)
						break;

					var streams = new List<int>();

					if (videoStreams.Count > 1)
					{
						var table = new Table()
							.SetDefaults()
							.AddColumns(
								new TableColumn("Index").RightAligned(),
								new TableColumn("Codec").Centered(),
								new TableColumn("Framerate").Centered(),
								new TableColumn("Duration").Centered()
							);

						foreach (var stream in videoStreams)
						{
							table.AddColorRow(
								stream.Index,
								stream.Codec,
								stream.Framerate + "fps",
								stream.Duration
							);
						}

						this.console.RenderTable(table, "VIDEO STREAMS");

						streams.AddRange(AskAndSelectStreams(videoStreams, "video streams"));

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
						var table = new Table()
							.SetDefaults()
							.AddColumns(
								new TableColumn("Index").RightAligned(),
								new TableColumn("Language").Centered(),
								new TableColumn("Codec").Centered(),
								new TableColumn("Channels").Centered(),
								new TableColumn("Sample Rate").Centered()
							);

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

							table.AddColorRow(
								stream.Index,
								ci?.EnglishName ?? stream.Language,
								stream.Codec,
								stream.Channels,
								stream.SampleRate + " Hz"
							);
						}

						this.console.RenderTable(table, "AUDIO STREAMS");

						streams.AddRange(AskAndSelectStreams(audioStreams, "audio streams"));

						if (this.tokenSource.Token.IsCancellationRequested)
							break;
					}
					else
					{
						streams.AddRange(audioStreams.Select(a => a.Index));
					}

					if (subtitleStreams.Count > 1)
					{
						var table = new Table()
							.SetDefaults()
							.AddColumns(
								new TableColumn("Index").RightAligned(),
								new TableColumn("Language").Centered(),
								new TableColumn("Title").Centered(),
								new TableColumn("Codec").Centered()
						);

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
							table.AddColorRow(
								stream.Index,
								ci?.EnglishName ?? stream.Language,
								stream.Title,
								stream.Codec
							);
						}

						this.console.RenderTable(table, "SUBTITLE STREAMS");

						streams.AddRange(AskAndSelectStreams(subtitleStreams, "subtitle streams"));

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
							this.console.WriteLine($"The file '{file}' is a duplicate of an existing file. Removing...", new Style(Color.Green));
							File.Delete(file);
							continue;
						}
						else if (settings.IgnoreDuplicates)
						{
							this.console.WriteLine($"WARNING: The file '{file}' is a duplicate of an existing file. Ignoring...", new Style(Color.Yellow));
							continue;
						}
						else
						{
							this.console.MarkupLine("[yellow]WARNING: Found duplicate file, ignoring or removing duplicates have not been specified. Continuing[/]");
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
								episodeData.Container = settings.FileExtension;
							else
								episodeData.Container = this.config.FileType;

							var seriesName = RemoveInvalidChars(episodeData.Series);
							directory = Path.Combine(directory, seriesName);

							if (!Directory.Exists(directory))
							{
								var yearDir = Directory.EnumerateDirectories(rootDir, seriesName + " (*)").FirstOrDefault();
								if (yearDir is not null)
									directory = yearDir;
							}

							if (episodeData.SeasonNumber is not null)
								directory = Path.Combine(directory, "Season " + episodeData.SeasonNumber);

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
							this.console.MarkupLine("[yellow on black] ERROR: No information was found in the data, and no output path is specified. This should not happen, please report this error to the developers.[/]");
							await queueRepository.AbortChangesAsync().ConfigureAwait(false);
							return 1;
						}
					}
					else if (settings.ReEncode)
					{
						var extension = settings.FileExtension ?? this.config.FileType.GetFileExtension();
						outputPath = Path.ChangeExtension(outputPath, extension);
					}
					else
					{
						this.console.MarkupLine("[yellow on black] ERROR: No information was found in the data, and no output path is specified. This should not happen, please report this error to the developers.[/]");
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

					var audioCodec = settings.AudioCodec ?? this.config.AudioCodec;
					var videoCodec = settings.VideoCodec ?? this.config.VideoCodec;
					var subtitleCodec = settings.SubtitleCodec ?? this.config.SubtitleCodec;

					if (settings.UseEncodingCopy)
					{
						if (!string.Equals(audioCodec, "copy", StringComparison.OrdinalIgnoreCase) &&
							mediaInfo.AudioStreams.Where(a => streams.Contains(a.Index)).All(a => string.Equals(a.Codec, audioCodec, StringComparison.OrdinalIgnoreCase)))
						{
							audioCodec = "copy";
						}

						if (!string.Equals(videoCodec, "copy", StringComparison.OrdinalIgnoreCase) &&
							mediaInfo.VideoStreams.Where(v => streams.Contains(v.Index)).All(v => string.Equals(v.Codec, videoCodec, StringComparison.OrdinalIgnoreCase)))
						{
							videoCodec = "copy";
						}

						if (!string.Equals(subtitleCodec, "copy", StringComparison.OrdinalIgnoreCase) &&
							mediaInfo.SubtitleStreams.Where(s => streams.Contains(s.Index)).All(s => string.Equals(s.Codec, subtitleCodec, StringComparison.OrdinalIgnoreCase)))
						{
							subtitleCodec = "copy";
						}
					}

					queueItem.AudioCodec = audioCodec;
					queueItem.OutputPath = outputPath;
					queueItem.NewHash = string.Empty;
					queueItem.Status = QueueStatus.Pending;
					queueItem.StatusMessage = string.Empty;
					queueItem.Streams = streams;
					queueItem.SubtitleCodec = subtitleCodec;
					queueItem.VideoCodec = videoCodec;
					queueItem.Parameters = settings.Parameters.Any(p => !string.IsNullOrEmpty(p)) ? string.Join(' ', settings.Parameters.Where(p => !string.IsNullOrEmpty(p))) : this.config.ExtraEncodingParameters;

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
						this.console.WriteLine($"WARNING: Unable to update '{file}'. Encoding have already started on the file!", new Style(Color.Yellow, Color.Black));
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
				this.console.MarkupLine("We are cancelling all processing, please wait for cleanup to be complete!");

				e.Cancel = true;
				this.tokenSource.Cancel();
			}
		}

		private IEnumerable<int> AskAndSelectStreams(IEnumerable<IStream> videoStreams, string type)
		{
			this.console.MarkupLine(
				"We found {0} {1}, please select the index of the streams "
				+ "you wish to keep (seperated by a space)\n"
				+ "or press {{Enter}} to keep all the {1}",
				videoStreams.Count(),
				type
			);

			var result = Console.ReadLine();

			if (string.IsNullOrWhiteSpace(result))
			{
				return videoStreams.Select(i => i.Index);
			}

			var indexes = result.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(i => int.Parse(i, CultureInfo.InvariantCulture));

			return videoStreams.Where(a => indexes.Contains(a.Index)).Select(i => i.Index);
		}

		private async Task<bool> AskAcceptableAsync(CommandContext context, Core.Models.EpisodeData episodeData)
		{
			DisplayEpisodeData(episodeData);

			this.console.MarkupLine("Do this information look correct? (Y/n/s) ");

			var key = Console.ReadKey();
			this.console.WriteLine();

			if (key.KeyChar == 's' || key.KeyChar == 'S')
				episodeData.Series = "SKIP";
			if (key.KeyChar != 'n' && key.KeyChar != 'N')
				return true;

			this.console.MarkupLine("Okay then, please enter the correct information!");
			this.console.MarkupLine("[italic]Press {{Enter}} with an empty line to use default values[/]");
			this.console.WriteLine();
			this.console.Markup("[aqua] Name of Series: [/]");

			var settings = new AddCriteriaOption
			{
				FilePath = episodeData.FileName,
				SeriesName = Console.ReadLine()
			};
			if (string.IsNullOrEmpty(settings.SeriesName))
				settings.SeriesName = episodeData.Series;

			this.console.Markup("[aqua] Season Number: [/]");
			var input = Console.ReadLine();

			if (int.TryParse(input, out var season))
				settings.SeasonNumber = season;
			else
				settings.SeasonNumber = episodeData.SeasonNumber;

			this.console.Markup("[aqua] Episode Number: [/]");
			input = Console.ReadLine();

			if (int.TryParse(input, out var episode))
				settings.EpisodeNumber = episode;
			else
				settings.EpisodeNumber = episodeData.EpisodeNumber;

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
			var table = new Table()
				.SetDefaults()
				.AddColumns(
					new TableColumn("Series").Centered(),
					new TableColumn("Season").Centered(),
					new TableColumn("Episode").Centered(),
					new TableColumn("Old Name").Centered(),
					new TableColumn("New Name").Centered()
				);

			table.AddColorRow(
				episodeData.Series,
				episodeData.SeasonNumber,
				episodeData.EpisodeNumber,
				episodeData.FileName,
				episodeData
			);

			this.console.RenderTable(table, "New Episode Data");
		}

		private async Task UpdateEpisodeDataAsync(Core.Models.EpisodeData episodeData)
		{
			var criterias = this.criteriaRepo.GetEpisodeCriteriasAsync(episodeData.Series);

			await foreach (var criteria in criterias)
			{
				if (criteria.UpdateEpisodeData(episodeData) && criteria.SeriesName is not null)
					break;
			}

			if (episodeData.SeasonNumber is null)
				episodeData.SeasonNumber = 1;

			if (!config.IncludeFansubber)
				episodeData.Fansubber = null;
		}

		public void Dispose()
		{
			this.tokenSource?.Dispose();
		}
	}
}
