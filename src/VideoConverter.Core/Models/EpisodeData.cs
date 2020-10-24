using System;
using System.Text;

namespace VideoConverter.Core.Models
{
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
                sb.Append($"[{Fansubber}] ");

            sb.Append($"{Series} - ");

            if (SeasonNumber is not null)
                sb.Append($"S{SeasonNumber:D2}");
            sb.Append($"E{EpisodeNumber:D2}");

            if (EpisodeName is not null)
                sb.Append($" - {EpisodeName}");

            switch (Container.ToUpperInvariant())
            {
                case "MATROSKA":
                    sb.Append(".mkv");
                    break;
                case "MPEG-4":
                    sb.Append(".mp4");
                    break;
                case "AUDIO VIDEO INTERLEAVE":
                    sb.Append(".avi");
                    break;
                case "ADVANCED SYSTEMS FORMAT":
                    sb.Append(".wmv");
                    break;
                default:
                    throw new Exception("Nope");
            }

            return sb.ToString();
        }
    }
}
