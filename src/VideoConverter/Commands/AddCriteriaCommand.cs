namespace VideoConverter.Commands
{
	using VideoConverter.Options;
	using Spectre.Console.Cli;
	using VideoConverter.Storage.Repositories;
	using VideoConverter.Core.Parsers;
	using Spectre.Console;
	using System;
	using VideoConverter.Core.Models;
	using System.Threading.Tasks;

	public class AddCriteriaCommand : AsyncCommand<AddCriteriaOption>
	{
		private readonly EpisodeCriteriaRepository repository;
		private readonly IAnsiConsole console;

		public AddCriteriaCommand(EpisodeCriteriaRepository repository, IAnsiConsole console)
		{
			this.repository = repository;
			this.console = console;
		}

		public override async Task<int> ExecuteAsync(CommandContext context, AddCriteriaOption settings)
		{
			if (settings is null)
				throw new ArgumentNullException(nameof(settings));

			EpisodeData? episodeData;
			try
			{
				episodeData = FileParser.ParseEpisode(settings.FilePath);
			}
			catch (Exception ex)
			{
				this.console.WriteException(ex);
				throw;
			}

			if (episodeData is null)
			{
				this.console.MarkupLine("[red on black] ERROR: We could not extract any of the necessary information from the file name![/]");
				this.console.MarkupLine("[red on black] ERROR: Please note that only Episodes are currently supported!");

				return 1;
			}

			await AddOrUpdateCriteriaAsync(episodeData, settings).ConfigureAwait(false);

			return 0;
		}

		private async Task AddOrUpdateCriteriaAsync(Core.Models.EpisodeData episodeData, AddCriteriaOption settings)
		{
			var newCriteria = new EpisodeCriteria
			{
				SeriesName = episodeData.Series,
			};

			if (!string.Equals(episodeData.Series, settings.SeriesName, StringComparison.Ordinal))
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
				newCriteria.Episode = episodeData.EpisodeNumber;
				newCriteria.NewEpisode = settings.EpisodeNumber.Value;
				if (newCriteria.Season is null)
					newCriteria.Season = episodeData.SeasonNumber;
			}

			await this.repository.AddOrUpdateCriteriaAsync(newCriteria).ConfigureAwait(false);
			await this.repository.SaveChangesAsync().ConfigureAwait(false);
		}
	}
}
