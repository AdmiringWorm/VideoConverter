namespace VideoConverter
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Spectre.Cli;
    using Spectre.Console;
    using VideoConverter.Storage.Repositories;
    using VideoConverter.Commands;
    using VideoConverter.DependencyInjection;
    using DryIoc;
    using VideoConverter.Storage.Models;
    using VideoConverter.Storage.Database;

    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            var container = CreateContainer();

            //var console = container.Resolve<IAnsiConsole>();

            var registrar = new TypeRegistrar(container);

            try
            {
                var app = new CommandApp(registrar);

                app.Configure(config =>
                {
                    config.AddBranch("add", add =>
                    {
                        add.SetDescription("Common command collection for all things related to adding file/directory/criteria");

                        add.AddCommand<AddDirectoryCommand>("directory")
                            .WithAlias("directories")
                            .WithAlias("dir")
                            .WithAlias("dir")
                            .WithAlias("d")
                            .WithDescription("Adds files in the specified directory/directories to the existing queue. If they are parsable!");

                        add.AddCommand<AddFileCommand>("file")
                            .WithAlias("files")
                            .WithAlias("f")
                            .WithDescription("Adds a new file(s) to the existing queue");

                        add.AddCommand<AddCriteriaCommand>("criteria")
                            .WithAlias("criterias")
                            .WithDescription("Adds or updates a new criteria to existing criterias.");
                    });

                    config.AddCommand<EncodeCommand>("encode")
                        .WithAlias("start")
                        .WithDescription("Starts encoding using already queued files!");

                    config.AddCommand<ConfigCommand>("config")
                        .WithDescription("Shows or updates the stored user configuration!");

                    config.AddBranch("queue", (queue) =>
                    {
                        queue.AddCommand<QueueClearCommand>("clear");
                        queue.AddCommand<QueueListCommand>("list")
                            .WithDescription("List files already in the queue!");
                        queue.AddCommand<QueueRemoveCommand>("remove");
                        queue.AddCommand<QueueShowCommand>("show");
                    });
                });

                return await app.RunAsync(args);
            }
            finally
            {
                container.Dispose();
            }
        }

        private static IContainer CreateContainer()
        {
            var container = new Container(rules => rules.WithTrackingDisposableTransients().WithCaptureContainerDisposeStackTrace());
            container.RegisterDelegate<IAnsiConsole>(context => AnsiConsole.Create(new AnsiConsoleSettings { Ansi = Spectre.Console.AnsiSupport.Detect, ColorSystem = ColorSystemSupport.Detect }), Reuse.Singleton);
            container.RegisterDelegate(RegisterConfigurationRepository, Reuse.Singleton);
            container.RegisterDelegate(RegisterConfiguration, Reuse.Singleton);
            container.Register<DatabaseFactory>(Reuse.Singleton);
            container.Register<EpisodeCriteriaRepository>(Reuse.ScopedOrSingleton);
            container.Register<QueueRepository>(Reuse.ScopedOrSingleton);

            return container;
        }

        private static ConfigurationRepository RegisterConfigurationRepository(IResolverContext arg)
        {
            var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoConverter", "config.xml");

            return new ConfigurationRepository(configPath);
        }

        private static Configuration RegisterConfiguration(IResolverContext context)
        {
            var repository = context.Resolve<ConfigurationRepository>();

            var config = repository.GetConfiguration();

            if (string.IsNullOrEmpty(config.MapperDatabase))

            {
                config.MapperDatabase = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoConverter", "storage.db");
                repository.SaveConfiguration(config);
            }

            return config;
        }

        // private static EpisodeCriteria ParseEpisodeCriteria(int defaultSeason, EpisodeData episodeData, List<EpisodeCriteria> criterias, EpisodeData originalEpisodeData)
        // {
        //     console.WriteLine("Please enter new values for the following data (use a empty line for default value)", Style.Plain);
        //     console.Write("Series Name: ", Style.Plain);
        //     var newSeries = Console.ReadLine() ?? string.Empty;
        //     console.Write("Season: ", Style.Plain);
        //     var newSeasonString = Console.ReadLine() ?? string.Empty;
        //     string newEpisodeString = string.Empty;
        //     if (!string.IsNullOrEmpty(newSeasonString))
        //     {
        //         console.Write("Episode: ", Style.Plain);
        //         newEpisodeString = Console.ReadLine() ?? string.Empty;
        //     }

        //     var newCriteria = new EpisodeCriteria();
        //     if (!string.IsNullOrWhiteSpace(newSeries) && newSeries != originalEpisodeData.Series)
        //     {
        //         newCriteria.SeriesName = originalEpisodeData.Series;
        //         newCriteria.NewSeries = newSeries;
        //     }
        //     else
        //     {
        //         newCriteria.SeriesName = originalEpisodeData.Series;
        //         if (newCriteria.SeriesName != episodeData.Series)
        //             newCriteria.NewSeries = episodeData.Series;
        //     }

        //     if (int.TryParse(newSeasonString, out var newSeason) && newSeason != originalEpisodeData.SeasonNumber)
        //     {
        //         newCriteria.Season = originalEpisodeData.SeasonNumber;
        //         newCriteria.NewSeason = newSeason;

        //         if (int.TryParse(newEpisodeString, out var newEpisode) && newEpisode != originalEpisodeData.EpisodeNumber)
        //         {
        //             if (newEpisode > 1)
        //             {
        //                 newCriteria.Episode = originalEpisodeData.EpisodeNumber - newEpisode + 1;
        //             }
        //             else if (newEpisode == 1)
        //             {
        //                 newCriteria.Episode = originalEpisodeData.EpisodeNumber;
        //             }

        //             var existingCriteria = criterias.FirstOrDefault(i => i.Episode > newCriteria.Episode && i.Episode < originalEpisodeData.EpisodeNumber);
        //             if (existingCriteria is not null)
        //             {
        //                 newCriteria.Episode = originalEpisodeData.EpisodeNumber;
        //                 newCriteria.NewEpisode = newEpisode;
        //             }
        //         }
        //         else if (originalEpisodeData.EpisodeNumber != episodeData.EpisodeNumber)
        //         {
        //             if (originalEpisodeData.EpisodeNumber > episodeData.EpisodeNumber)
        //                 newCriteria.Episode = originalEpisodeData.EpisodeNumber - episodeData.EpisodeNumber + 1;
        //             else
        //                 newCriteria.Episode = episodeData.EpisodeNumber;
        //         }
        //     }
        //     else
        //     {
        //         if (originalEpisodeData.SeasonNumber != episodeData.SeasonNumber && episodeData.SeasonNumber != defaultSeason)
        //         {
        //             newCriteria.Season = originalEpisodeData.SeasonNumber;
        //             newCriteria.NewSeason = episodeData.SeasonNumber;
        //         }
        //         if (int.TryParse(newEpisodeString, out var forcedEpisode) && forcedEpisode != originalEpisodeData.EpisodeNumber)
        //         {
        //             newCriteria.Episode = originalEpisodeData.EpisodeNumber;
        //             newCriteria.NewEpisode = forcedEpisode;
        //             newCriteria.Season = originalEpisodeData.SeasonNumber;
        //             newCriteria.NewSeason = originalEpisodeData.SeasonNumber;
        //         }
        //     }

        //     return newCriteria;
        // }

        // private static void UpdateEpisodeData(int defaultSeason, EpisodeData episodeData, out List<EpisodeCriteria> criterias, out EpisodeData originalEpisodeData)
        // {
        //     using (var db = new LiteDatabase("mappings2.db"))
        //     {
        //         var repo = new EpisodeCriteriaRepository(db);
        //         criterias = repo.GetEpisodeCriterias(episodeData.Series).ToList();
        //     }

        //     originalEpisodeData = episodeData.Copy();
        //     foreach (var criteria in criterias)
        //     {
        //         if (criteria.UpdateEpisodeData(episodeData) && criteria.SeriesName is not null)
        //             break;
        //     }

        //     if (episodeData.SeasonNumber is null)
        //         episodeData.SeasonNumber = defaultSeason;
        // }

        // private static void DisplayInformation(EpisodeData episodeData)
        // {
        //     console.MarkupLine(
        //                     "File: [invert]{0}[/]\nSeries: [invert]{1}[/]\nSeason: [invert]{2:D2}[/]\nEpisode: [invert]{3:D2}[/]\nNew File: [invert]{4}[/]",
        //                     episodeData.FileName.EscapeMarkup(),
        //                     episodeData.Series,
        //                     episodeData.SeasonNumber!,
        //                     episodeData.EpisodeNumber,
        //                     RemoveInvalidChars(episodeData.ToString()).EscapeMarkup());
        // }

        // private static string RemoveInvalidChars(string text)
        // {
        //     var sb = new StringBuilder();
        //     const string invalidChars = "<>:\"/\\|?*";

        //     foreach (var ch in text)
        //     {
        //         if (!invalidChars.Contains(ch))
        //             sb.Append(ch);
        //         else
        //             sb.Append(' ');
        //     }

        //     return Regex.Replace(sb.ToString(), @"\s{2,}", " ", RegexOptions.Compiled);
        // }
    }
}
