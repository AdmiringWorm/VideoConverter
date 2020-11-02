using System.Linq;
namespace VideoConverter.Storage.Repositories
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LiteDB;
    using VideoConverter.Storage.Database;
    using VideoConverter.Storage.Models;
    using CoreEpisodeCriteria = VideoConverter.Core.Models.EpisodeCriteria;

    public class EpisodeCriteriaRepository
    {
        private const string TABLE_NAME = "criterias";
        private readonly DatabaseFactory dbFactory;

        public EpisodeCriteriaRepository(DatabaseFactory dbFactory)
        {
            this.dbFactory = dbFactory;
        }

        public async IAsyncEnumerable<CoreEpisodeCriteria> GetEpisodeCriteriasAsync(string seriesName)
        {
            var collection = this.dbFactory.GetCollectionAsync<Criteria<EpisodeCriteria>>(TABLE_NAME);

            var criterias = await collection.Query()
                .Where(c => c.Name == seriesName)
                .Include(c => c.Criterias)
                .ToEnumerableAsync().ConfigureAwait(false);

            foreach (var criteriaName in criterias)
            {
                foreach (var criteria in criteriaName.Criterias.OrderByDescending(c => c.OldEpisode).ThenByDescending(c => c.OldSeason))
                {
                    var newCriteria = new CoreEpisodeCriteria();
                    newCriteria.Episode = criteria.OldEpisode;
                    newCriteria.NewEpisode = criteria.NewEpisode;
                    newCriteria.NewSeason = criteria.NewSeason;
                    newCriteria.NewSeries = criteria.NewName;
                    newCriteria.Season = criteria.OldSeason;
                    newCriteria.SeriesName = criteriaName.Name;
                    yield return newCriteria;
                }
            }
        }

        public async Task AddOrUpdateCriteriaAsync(CoreEpisodeCriteria criteria)
        {
            var collection = this.dbFactory.GetCollectionAsync<Criteria<EpisodeCriteria>>(TABLE_NAME);
            var epCol = this.dbFactory.GetCollectionAsync<EpisodeCriteria>();
            await this.dbFactory.EnsureTransactionAsync().ConfigureAwait(false);

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
                await this.dbFactory.CreateCheckpointAsync().ConfigureAwait(false);

                newCriteria.Criterias.Add(epCriteria);
                await collection.InsertAsync(newCriteria).ConfigureAwait(false);
            }
            else
            {
                var actualCriteria = existingCriteria.Criterias.FirstOrDefault(c => c.NewSeason is null || (c.OldSeason is null && c.OldEpisode is null));
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
                    await this.dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
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
                    await this.dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
                }
                await collection.UpdateAsync(existingCriteria).ConfigureAwait(false);
            }

            await collection.EnsureIndexAsync(c => c.Name).ConfigureAwait(false);


            await this.dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
        }

        public Task SaveChangesAsync()
        {
            return this.dbFactory.CommitTransactionAsync();
        }

        public Task AbortChanges()
        {
            return this.dbFactory.RollbackTransactionAsync();
        }
    }
}
