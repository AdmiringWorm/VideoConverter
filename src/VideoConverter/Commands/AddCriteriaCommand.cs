namespace VideoConverter.Commands
{
	using System;
	using System.Threading.Tasks;

	using Spectre.Console;
	using Spectre.Console.Cli;

	using VideoConverter.Core.Assertions;
	using VideoConverter.Core.Models;
	using VideoConverter.Core.Parsers;
	using VideoConverter.Options;
	using VideoConverter.Storage.Repositories;

	public class AddCriteriaCommand : AsyncCommand<AddCriteriaOption>
	{
		private readonly IAnsiConsole console;
		private readonly EpisodeCriteriaRepository repository;

		public AddCriteriaCommand(EpisodeCriteriaRepository repository, IAnsiConsole console)
		{
			this.repository = repository;
			this.console = console;
		}

		public override async Task<int> ExecuteAsync(CommandContext context, AddCriteriaOption settings)
		{
			settings.AssertNotNull();

			EpisodeData? episodeData;
			try
			{
				episodeData = FileParser.ParseEpisode(settings.FilePath);
			}
			catch (Exception ex)
			{
				console.WriteException(ex);
				throw;
			}

			if (episodeData is null)
			{
				console.MarkupLine(
					"[red on black] ERROR: We could not extract any of the necessary information from the file name![/]"
				);
				console.MarkupLine(
					"[red on black] ERROR: Please note that only Episodes are currently supported!"
				);

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
				{
					newCriteria.Season = episodeData.SeasonNumber;
				}
			}

			await repository.AddOrUpdateCriteriaAsync(newCriteria).ConfigureAwait(false);
			await repository.SaveChangesAsync().ConfigureAwait(false);
		}
	}
}
