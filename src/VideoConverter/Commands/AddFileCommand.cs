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

    public class AddFileCommand : AsyncCommand<AddFileOption>
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

                    var hashTask = GetSHA1Async(file, this.tokenSource.Token);

                    var mediaInfoTask = FFmpeg.GetMediaInfo(file, this.tokenSource.Token);

                    bool isAccepted = false;
                    EpisodeData? episodeData = null;

                    while (!this.tokenSource.Token.IsCancellationRequested && string.IsNullOrEmpty(settings.OutputPath) && !isAccepted)
                    {
                        try
                        {
                            episodeData = FileParser.ParseEpisode(file);
                        }
                        catch (Exception ex)
                        {
                            this.console.WriteException(ex);
                            return 1;
                        }
                        if (settings.ReEncode)
                            break;
                        if (episodeData is null)
                        {
                            this.console.MarkupLine("[yellow on black] WARNING: We were unable to extract necessary information from '[fuchsia]{0}[/]'. Ignoring...[/]", file.EscapeMarkup());
                            break;
                        }

                        if (this.tokenSource.Token.IsCancellationRequested)
                            break;

                        UpdateEpisodeData(episodeData);

                        if (this.tokenSource.Token.IsCancellationRequested)
                            break;

                        isAccepted = AskAcceptable(context, episodeData);
                    }

                    if (this.tokenSource.Token.IsCancellationRequested)
                        break;

                    if ((episodeData is null && string.IsNullOrEmpty(settings.OutputPath) && !settings.ReEncode) ||
                        (episodeData is not null && episodeData.Series == "SKIP"))
                        continue;

                    var mediaInfo = await mediaInfoTask;
                    var videoStreams = mediaInfo.VideoStreams.ToList();
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

                    var hash = await hashTask.ConfigureAwait(false);
                    var fileExist = this.queueRepository.FileExists(file.Normalize(), hash);

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

                    string outputPath = string.Empty;
                    if (!string.IsNullOrEmpty(settings.OutputPath))
                    {
                        outputPath = Path.GetFullPath(settings.OutputPath);
                    }
                    else if (!string.IsNullOrEmpty(settings.OutputDir))
                    {
                        string rootDir = Path.GetFullPath(settings.OutputDir!);
                        if (!Directory.Exists(rootDir))
                            Directory.CreateDirectory(rootDir);
                        string directory = rootDir;
                        if (episodeData is not null)
                        {
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
                            outputPath = Path.Combine(directory, Path.GetFileName(file));
                            outputPath = Path.ChangeExtension(outputPath, ".mkv");
                        }
                        else
                        {
                            this.console.MarkupLine("[yellow on black] ERROR: No information was found in the data, and no output path is specified. This should not happen, please report this error to the developers.[/]");
                            this.queueRepository.AbortChanges();
                            return 1;
                        }
                    }
                    else if (settings.ReEncode)
                    {
                        outputPath = Path.ChangeExtension(file, ".mkv");
                    }
                    else
                    {
                        this.console.MarkupLine("[yellow on black] ERROR: No information was found in the data, and no output path is specified. This should not happen, please report this error to the developers.[/]");
                        this.queueRepository.AbortChanges();
                        return 1;
                    }

                    var queueItem = new FileQueue
                    {
                        AudioCodec = settings.AudioCodec ?? this.config.AudioCodec,
                        OldHash = hash,
                        OutputPath = outputPath,
                        Path = file.Normalize(),
                        Status = QueueStatus.Pending,
                        StatusMessage = string.Empty,
                        Streams = streams,
                        SubtitleCodec = settings.SubtitleCodec ?? this.config.SubtitleCodec,
                        VideoCodec = settings.VideoCodec ?? this.config.VideoCodec,
                    };

                    if (queueRepository.AddToQueue(queueItem))
                    {
                        this.queueRepository.SaveChanges();
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
                        this.queueRepository.AbortChanges();
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

        private async Task<string> GetSHA1Async(string file, CancellationToken cancellationToken)
        {
            using var algo = SHA1.Create();
            using var stream = File.OpenRead(file);
            var sb = new StringBuilder();

            var hashBytes = await algo.ComputeHashAsync(stream, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return string.Empty;

            foreach (var b in hashBytes)
            {
                sb.AppendFormat("{0:x2}", b);
            }

            return sb.ToString();
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

            var indexes = result.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(i => int.Parse(i));

            return videoStreams.Where(a => indexes.Contains(a.Index)).Select(i => i.Index);
        }

        private bool AskAcceptable(CommandContext context, Core.Models.EpisodeData episodeData)
        {
            DisplayEpisadeData(episodeData);

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

            this.criteriaCommand.Execute(context, settings);

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

        private void DisplayEpisadeData(Core.Models.EpisodeData episodeData)
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

        private void UpdateEpisodeData(Core.Models.EpisodeData episodeData)
        {
            var criterias = this.criteriaRepo.GetEpisodeCriterias(episodeData.Series);

            foreach (var criteria in criterias)
            {
                if (criteria.UpdateEpisodeData(episodeData) && criteria.SeriesName is not null)
                    break;
            }

            if (episodeData.SeasonNumber is null)
                episodeData.SeasonNumber = 1;

            episodeData.Container = "Matroska"; // TODO: Make Configurable

            if (!config.IncludeFansubber)
                episodeData.Fansubber = null;
        }
    }
}
