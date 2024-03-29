namespace VideoConverter.Core.Extensions
{
	using VideoConverter.Core.Assertions;
	using VideoConverter.Core.Models;

	public static class EpisodeCriteriaExtensions
	{
		public static bool UpdateEpisodeData(this EpisodeCriteria criteria, EpisodeData data)
		{
			criteria.AssertNotNull();
			data.AssertNotNull();

			if (data < criteria)
			{
				return false;
			}

			if (criteria.NewSeason is not null)
			{
				data.SeasonNumber = criteria.NewSeason;
				if (criteria.Episode is not null)
				{
					data.EpisodeNumber -= criteria.Episode.Value - 1;
					if (data.EpisodeNumber <= 0)
					{
						data.EpisodeNumber = 1;
					}

					if (criteria.NewEpisode is not null)
					{
						data.EpisodeNumber += criteria.NewEpisode.Value - 1;
					}
				}
			}
			else if (criteria.Season is not null && criteria.NewEpisode is not null)
			{
				data.EpisodeNumber = criteria.NewEpisode.Value;
			}

#pragma warning disable RCS1208 // Reduce 'if' nesting.
			if (criteria.NewSeries is not null)
			{
				data.Series = criteria.NewSeries;
			}
#pragma warning restore RCS1208 // Reduce 'if' nesting.

			return true;
		}
	}
}
