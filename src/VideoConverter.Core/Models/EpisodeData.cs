using System.Globalization;
namespace VideoConverter.Core.Models
{
	using System;
	using System.Text;
	using VideoConverter.Core.Extensions;

	public sealed class EpisodeData
	{
		public EpisodeData(string fileName, string series, int episodeNumber, string container)
		{
			FileName = fileName;
			Series = series;
			EpisodeNumber = episodeNumber;
			Container = container;
		}

		public string FileName { get; set; }
		public string? Fansubber { get; set; }

		public string Series { get; set; }
		public int? SeasonNumber { get; set; }

		public string? EpisodeName { get; set; }

		public int EpisodeNumber { get; set; }

		public string Container { get; set; }

		public EpisodeData Copy()
		{
			return new EpisodeData(FileName, Series, EpisodeNumber, Container)
			{
				Fansubber = Fansubber,
				SeasonNumber = SeasonNumber,
				EpisodeName = EpisodeName
			};
		}

		public override bool Equals(object? obj)
		{
			return obj is EpisodeData data &&
				FileName == data.FileName &&
				Fansubber == data.Fansubber &&
				Series == data.Series &&
				SeasonNumber == data.SeasonNumber &&
				EpisodeNumber == data.EpisodeNumber &&
				EpisodeName == data.EpisodeName &&
				Container == data.Container;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(FileName, Fansubber, Series, SeasonNumber, EpisodeName, EpisodeNumber, Container);
		}

		public override string ToString()
		{
			var sb = new StringBuilder();

			if (Fansubber is not null)
				sb.Append('[').Append(Fansubber).Append("] ");

			sb.Append(Series).Append(" - ");

			if (SeasonNumber is not null)
				sb.Append('S').AppendFormat(CultureInfo.InvariantCulture, "{0:D2}", SeasonNumber);
			sb.Append('E').AppendFormat(CultureInfo.InvariantCulture, "{0:D2}", EpisodeNumber);

			if (EpisodeName is not null)
				sb.Append(" - ").Append(EpisodeName);

			sb.Append(Container.GetFileExtension());

			return sb.ToString();
		}
	}
}
