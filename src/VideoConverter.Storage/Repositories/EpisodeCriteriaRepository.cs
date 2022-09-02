namespace VideoConverter.Storage.Repositories
{
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;

	using VideoConverter.Core.Assertions;
	using VideoConverter.Storage.Database;
	using VideoConverter.Storage.Models;

	using CoreEpisodeCriteria = VideoConverter.Core.Models.EpisodeCriteria;

	public class EpisodeCriteriaRepository
	{
		public const string TABLE_NAME = "criterias";
		private readonly DatabaseFactory dbFactory;

		public EpisodeCriteriaRepository(DatabaseFactory dbFactory)
		{
			this.dbFactory = dbFactory;
		}

		public Task AbortChangesAsync()
		{
			return dbFactory.RollbackTransactionAsync();
		}

		public async Task AddOrUpdateCriteriaAsync(CoreEpisodeCriteria criteria)
		{
			criteria.AssertNotNull();

			var collection = dbFactory.GetCollection<Criteria<EpisodeCriteria>>(TABLE_NAME);
			var epCol = dbFactory.GetCollection<EpisodeCriteria>();
			await dbFactory.EnsureTransactionAsync().ConfigureAwait(false);

			var existingCriteria = await collection
				.Include(c => c.Criterias)
				.FindOneAsync(c => c.Name == criteria.SeriesName).ConfigureAwait(false);

			if (existingCriteria is null)
			{
				var newCriteria = new Criteria<EpisodeCriteria>
				{
					Name = criteria.SeriesName,
				};
				var epCriteria = new EpisodeCriteria
				{
					NewEpisode = criteria.NewEpisode,
					NewName = criteria.NewSeries,
					NewSeason = criteria.NewSeason,
					OldEpisode = criteria.Episode,
					OldSeason = criteria.Season,
				};
				await epCol.InsertAsync(epCriteria).ConfigureAwait(false);
				await dbFactory.CreateCheckpointAsync().ConfigureAwait(false);

				newCriteria.Criterias.Add(epCriteria);
				await collection.InsertAsync(newCriteria).ConfigureAwait(false);
			}
			else
			{
				var actualCriteria = existingCriteria.Criterias
					.Find(c => c.NewSeason is null || (c.OldSeason is null && c.OldEpisode is null));
				if (actualCriteria is null)
				{
					actualCriteria = new EpisodeCriteria
					{
						NewEpisode = criteria.NewEpisode,
						NewName = criteria.NewSeries,
						NewSeason = criteria.NewSeason,
						OldEpisode = criteria.Episode,
						OldSeason = criteria.Season,
					};
					await epCol.InsertAsync(actualCriteria).ConfigureAwait(false);
					await dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
					existingCriteria.Criterias.Add(actualCriteria);
				}
				else
				{
					actualCriteria.NewEpisode = criteria.NewEpisode;
					actualCriteria.NewName = criteria.NewSeries ?? actualCriteria.NewName;
					actualCriteria.NewSeason = criteria.NewSeason;
					actualCriteria.OldEpisode = criteria.Episode;
					actualCriteria.OldSeason = criteria.Season;
					await epCol.UpdateAsync(actualCriteria).ConfigureAwait(false);
					await dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
				}
				await collection.UpdateAsync(existingCriteria).ConfigureAwait(false);
			}

			await collection.EnsureIndexAsync(c => c.Name).ConfigureAwait(false);

			await dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
		}

		public async IAsyncEnumerable<CoreEpisodeCriteria> GetEpisodeCriteriasAsync(string seriesName)
		{
			var collection = dbFactory.GetCollection<Criteria<EpisodeCriteria>>(TABLE_NAME);

			var criterias = await collection.Query()
				.Where(c => c.Name == seriesName)
				.Include(c => c.Criterias)
				.ToEnumerableAsync().ConfigureAwait(false);

			foreach (var criteriaName in criterias)
			{
				foreach (var criteria in criteriaName.Criterias
					.OrderByDescending(c => c.OldEpisode)
					.ThenByDescending(c => c.OldSeason))
				{
					yield return new CoreEpisodeCriteria
					{
						Episode = criteria.OldEpisode,
						NewEpisode = criteria.NewEpisode,
						NewSeason = criteria.NewSeason,
						NewSeries = criteria.NewName,
						Season = criteria.OldSeason,
						SeriesName = criteriaName.Name
					};
				}
			}
		}

		public Task SaveChangesAsync()
		{
			return dbFactory.CommitTransactionAsync();
		}
	}
}
