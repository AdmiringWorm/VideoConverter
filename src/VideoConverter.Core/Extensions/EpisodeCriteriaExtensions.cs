namespace VideoConverter.Core.Extensions
{
    using VideoConverter.Core.Models;

    public static class EpisodeCriteriaExtensions
    {
        public static bool UpdateEpisodeData(this EpisodeCriteria criteria, EpisodeData data)
        {
            if (data >= criteria)
            {
                if (criteria.NewSeason is not null)
                {
                    data.SeasonNumber = criteria.NewSeason;
                    if (criteria.Episode is not null)
                    {
                        data.EpisodeNumber -= criteria.Episode.Value - 1;
                        if (data.EpisodeNumber <= 0)
                            data.EpisodeNumber = 1;
                        if (criteria.NewEpisode is not null)
                            data.EpisodeNumber += criteria.NewEpisode.Value - 1;
                    }
                }

                if (criteria.NewSeries is not null)
                    data.Series = criteria.NewSeries;

                return true;
            }

            return false;
        }
    }
}
