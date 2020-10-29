using System.Linq;
namespace VideoConverter.Storage.Repositories
{
    using System.Collections.Generic;
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

        public IEnumerable<CoreEpisodeCriteria> GetEpisodeCriterias(string seriesName)
        {
            var collection = this.dbFactory.GetCollection<Criteria<EpisodeCriteria>>(TABLE_NAME);

            var criterias = collection.Query()
                .Where(c => c.Name == seriesName)
                .Include(c => c.Criterias)
                .ToEnumerable();

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

        public void AddOrUpdateCriteria(CoreEpisodeCriteria criteria)
        {
            var collection = this.dbFactory.GetCollection<Criteria<EpisodeCriteria>>(TABLE_NAME);
            var epCol = this.dbFactory.GetCollection<EpisodeCriteria>();
            this.dbFactory.EnsureTransaction();

            var existingCriteria = collection
                .Include(c => c.Criterias)
                .FindOne(c => c.Name == criteria.SeriesName);

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
                epCol.Insert(epCriteria);
                this.dbFactory.CreateCheckpoint();

                newCriteria.Criterias.Add(epCriteria);
                collection.Insert(newCriteria);
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
                    epCol.Insert(actualCriteria);
                    this.dbFactory.CreateCheckpoint();
                    existingCriteria.Criterias.Add(actualCriteria);
                }
                else
                {
                    actualCriteria.NewEpisode = criteria.NewEpisode;
                    actualCriteria.NewName = criteria.NewSeries ?? actualCriteria.NewName;
                    actualCriteria.NewSeason = criteria.NewSeason;
                    actualCriteria.OldEpisode = criteria.Episode;
                    actualCriteria.OldSeason = criteria.Season;
                    epCol.Update(actualCriteria);
                    this.dbFactory.CreateCheckpoint();
                }
                collection.Update(existingCriteria);
            }

            collection.EnsureIndex(c => c.Name);


            this.dbFactory.CreateCheckpoint();
        }

        public void SaveChanges()
        {
            this.dbFactory.CommitTransaction();
        }

        public void AbortChanges()
        {
            this.dbFactory.RollbackTransaction();
        }
    }
}
