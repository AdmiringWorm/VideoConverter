namespace VideoConverter.Commands
{
    using VideoConverter.Options;
    using Spectre.Cli;
    using VideoConverter.Storage.Repositories;
    using VideoConverter.Core.Parsers;
    using Spectre.Console;
    using System.Linq;
    using Humanizer;
    using System;
    using VideoConverter.Core.Models;

    public class AddCriteriaCommand : Command<AddCriteriaOption>
    {
        private readonly EpisodeCriteriaRepository repository;
        private readonly IAnsiConsole console;

        public AddCriteriaCommand(EpisodeCriteriaRepository repository, IAnsiConsole console)
        {
            this.repository = repository;
            this.console = console;
        }

        public override int Execute(CommandContext context, AddCriteriaOption settings)
        {
            EpisodeData? episodeData;
            try
            {
                episodeData = FileParser.ParseEpisode(settings.FilePath);
            }
            catch (Exception ex)
            {
                this.console.WriteException(ex);
                return 1;
            }

            if (episodeData is null)
            {
                this.console.MarkupLine("[red on black] ERROR: We could not extract any of the necessary information from the file name![/]");
                this.console.MarkupLine("[red on black] ERROR: Please note that only Episodes are currently supported!");

                return 1;
            }

            AddOrUpdateCriteria(episodeData, settings);

            return 0;
        }
        private void AddOrUpdateCriteria(Core.Models.EpisodeData episodeData, AddCriteriaOption settings)
        {
            var newCriteria = new EpisodeCriteria
            {
                SeriesName = episodeData.Series,
            };

            if (string.Compare(episodeData.Series, settings.SeriesName, StringComparison.Ordinal) != 0)
            {
                newCriteria.NewSeries = settings.SeriesName;
            }

            if (settings.SeasonNumber is not null && episodeData.SeasonNumber != settings.SeasonNumber)
            {
                newCriteria.Season = episodeData.SeasonNumber;
                newCriteria.NewSeason = settings.SeasonNumber;
            }

            if (settings.EpisodeNumber is not null && episodeData.EpisodeNumber != settings.EpisodeNumber)
            {
                int episodeNum = episodeData.EpisodeNumber;
                int newEpisode = settings.EpisodeNumber.Value;
                if (settings.EpisodeNumber > 1 && (episodeData.EpisodeNumber - settings.EpisodeNumber + 1) > 0)
                {
                    (episodeNum, newEpisode) = GetEpisodeNumbers(episodeData.Series, episodeData.EpisodeNumber, settings.EpisodeNumber.Value);
                }
                else
                {
                    episodeNum = episodeData.EpisodeNumber;
                    newEpisode = settings.EpisodeNumber.Value;
                }

                if (episodeNum != newEpisode || episodeNum != episodeData.EpisodeNumber || newEpisode != settings.EpisodeNumber)
                {
                    newCriteria.Episode = episodeNum;
                    newCriteria.NewEpisode = newEpisode;
                }
            }

            this.repository.AddOrUpdateCriteria(newCriteria);
            this.repository.SaveChanges();
        }

        private (int, int) GetEpisodeNumbers(string seriesName, int originalEpisode, int wantedEpisode)
        {
            int episodeNum = originalEpisode - wantedEpisode + 1;
            int newEpisode = 1;

            while (episodeNum < 1)
            {
                episodeNum++;
                newEpisode++;
            }

            var criteria = this.repository.GetEpisodeCriterias(seriesName).FirstOrDefault(c => (c.Episode > episodeNum && episodeNum < originalEpisode) || c.NewEpisode == newEpisode);
            if (criteria is not null)
            {
                episodeNum = originalEpisode;
                newEpisode = wantedEpisode;
            }


            return (episodeNum, newEpisode);
        }
    }
}
