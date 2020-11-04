using System;
using System.Text;
using VideoConverter.Core.Assertions;

namespace VideoConverter.Core.Models
{
	public class EpisodeCriteria
	{
		public string SeriesName { get; set; } = string.Empty;
		public int? Season { get; set; }

		public int? Episode { get; set; }

		public string? NewSeries { get; set; }
		public int? NewSeason { get; set; }

		public int? NewEpisode { get; set; }

		public static bool operator ==(EpisodeData data, EpisodeCriteria criteria)
		{
			criteria.IsNotNull();
			data.IsNotNull();

			if (criteria.SeriesName is null && criteria.Season is null && criteria.Episode is null && criteria.NewEpisode is null)
				return false;

			return (criteria.SeriesName is null || string.Equals(criteria.SeriesName, data.Series)) &&
				   (criteria.Season is null || criteria.Season == data.SeasonNumber || (criteria.Season == 0 && data.SeasonNumber is null)) &&
				   (criteria.Episode is null || criteria.Episode == data.EpisodeNumber);
		}

		public static bool operator !=(EpisodeData data, EpisodeCriteria criteria)
			=> !(data == criteria);

		public static bool operator >(EpisodeData data, EpisodeCriteria criteria)
		{
			criteria.IsNotNull();
			data.IsNotNull();

			if (criteria.Season is null && criteria.Episode is null)
				return false;

			return (criteria.Season is null || data.SeasonNumber > criteria.Season) &&
				   (criteria.Episode is null || data.EpisodeNumber > criteria.Episode);
		}

		public static bool operator <(EpisodeData data, EpisodeCriteria criteria)
		{
			criteria.IsNotNull();
			data.IsNotNull();

			if (criteria.Season is null && criteria.Episode is null)
				return false;

			return (criteria.Season is null || data.SeasonNumber < criteria.Season) &&
				   (criteria.Episode is null || data.EpisodeNumber < criteria.Episode);
		}

		public static bool operator >=(EpisodeData data, EpisodeCriteria criteria)
		{
			return data > criteria || data == criteria;
		}

		public static bool operator <=(EpisodeData data, EpisodeCriteria criteria)
		{
			return data < criteria || data == criteria;
		}

		public override bool Equals(object? obj)
		{
			return (obj is EpisodeData data && data == this) ||
				(obj is EpisodeCriteria criteria &&
				 criteria.Episode == Episode &&
				 criteria.NewSeason == NewSeason &&
				 criteria.NewSeries == NewSeries &&
				 criteria.Season == Season &&
				 criteria.SeriesName == SeriesName);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(SeriesName, Season, Episode, NewSeries, NewSeason);
		}

		public override string ToString()
		{
			var sb = new StringBuilder("Episode Criteria [");
			bool previous = false;

			if (SeriesName is not null)
			{
				sb.Append("Series(").Append(SeriesName).Append(')');
				previous = true;
			}

			if (Season is not null)
			{
				if (previous)
					sb.Append(" AND ");
				sb.Append("Season(").Append(Season).Append(')');
				previous = true;
			}

			if (Episode is not null)
			{
				if (previous)
					sb.Append(" AND ");
				sb.Append("Episode(").Append(Episode).Append(')');
			}

			sb.Append("] => [");
			previous = false;

			if (NewSeries is not null && NewSeries != SeriesName)
			{
				sb.Append("Series(").Append(NewSeries).Append(')');
				previous = true;
			}

			if (NewSeason is not null)
			{
				if (previous && NewSeason != Season)
					sb.Append(" AND ");
				if (NewSeason != Season)
					sb.Append("Season(").Append(NewSeason).Append(')');
				if (NewEpisode is not null)
					sb.Append(" AND Episode(").Append(NewEpisode).Append(')');
				else if (Episode is not null)
					sb.Append(" AND Episode(1)");
			}

			sb.Append(']');

			return sb.ToString();
		}
	}
}
