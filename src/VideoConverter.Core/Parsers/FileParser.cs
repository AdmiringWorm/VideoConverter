namespace VideoConverter.Core.Parsers
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using VideoConverter.Core.Models;

    public static class FileParser
    {
        private readonly static LinkedList<string> episodeRegexes = new LinkedList<string>();

        static FileParser()
        {
            episodeRegexes.AddLast(@"^[\s_]*[\[\(][\s_]*(?<Fansubber>[^\]\)]+)[\s_]*[\]\)][\s_]*(?<Series>.+)[\s_]*S(?<Season>\d+)[\s_]*-[\s_]*(?<Episode>\d+|(?:OVA|OAV) ?\d*)[^\.]*\.(?<Extension>[a-z\d]+)$");
            episodeRegexes.AddLast(@"^[\s_]*[\[\(][\s_]*(?<Fansubber>[^\]\)]+)[\s_]*[\]\)][\s_]*(?<Series>.+)[\s_]*-[\s_]*(?<Episode>\d+|(?:OVA|OAV) ?\d*)[^\.]*\.(?<Extension>[a-z\d]+)$");
            episodeRegexes.AddLast(@"^[\s_]*[\[\(][\s_]*(?<Fansubber>[^\]\)]+)[\s_]*[\]\)][\s_]*(?<Series>.+)[\s_]*-[\s_]*S(?<Season>\d+)E(?<Episode>\d+)(?:[\s_]*-[\s_]*(?<EpisodeName>[^\.]+)[\s_]*|[^\.]*)\.(?<Extension>[a-z\d]+)$");
            episodeRegexes.AddLast(@"^[\s_]*(?<Series>.+)[\s_]*-[\s_]*S(?<Season>\d+)E(?<Episode>\d+)[\s_]*-[\s_]*(?<EpisodeName>[^\.]+)\.(?<Extension>[a-z\d]+)$");
            episodeRegexes.AddLast(@"^(?<Series>.+)[\s_]*(?:S(?<Season>\d+))[\s_]*-[\s_]*(?<Episode>\d+)[\s_]*\.(?<Extension>[a-z\d]+)$");
            episodeRegexes.AddLast(@"^[\s_]*(?<Series>.+)[\s_]*S(?<Season>\d+)[\s_]*-[\s_]*(?<Episode>\d+|(?:OVA|OAV) ?\d*)[^\.]*\.(?<Extension>[a-z\d]+)$");
            episodeRegexes.AddLast(@"^[\s_]*(?<Series>.+)[\s_]*-[\s_]*(?<Episode>\d+|(?:OVA|OAV) ?\d*)[^\.]*\.(?<Extension>[a-z\d]+)$");
            episodeRegexes.AddLast(@"^[\s_]*(?<Series>.+)[\s_]*-[\s_]*S(?<Season>\d+)E(?<Episode>\d+)(?:[\s_]*-[\s_]*(?<EpisodeName>[^\.]+)[\s_]*|[^\.]*)\.(?<Extension>[a-z\d]+)$");
            episodeRegexes.AddLast(@"^[\s_]*[\[\(][\s_]*(?<Fansubber>[^\]\)]+)[\s_]*[\]\)][\s_]*(?<Series>[^\[]+)[\s_]+(?<Episode>\d+|(?:OVA|OAV) ?\d*)[^\.]*\.(?<Extension>[a-z\d]+)$");
        }

        public static EpisodeData? ParseEpisode(string fileName)
        {
            var index = fileName.LastIndexOfAny(new[] { '/', '\\' });
            if (index > 0)
            {
                fileName = fileName.Substring(index + 1);
            }

            foreach (var regex in episodeRegexes)
            {
                var m = Regex.Match(fileName, regex, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                if (!m.Success)
                    continue;

                string container = string.Empty;
                switch (m.Groups["Extension"].Value.ToUpperInvariant())
                {
                    case "MKV":
                    case "MK3D":
                    case "MKA":
                    case "MKS":
                        container = "Matroska";
                        break;
                    case "MP4":
                    case "M4A":
                    case "M4P":
                    case "M4B":
                    case "M4R":
                    case "M4V":
                        container = "MPEG-4";
                        break;
                    case "AVI":
                        container = "Audio Video Interleave";
                        break;
                    case "WMV":
                    case "WMA":
                    case "ASF":
                        container = "Advanced Systems Format";
                        break;

                    default:
                        continue;
                }

                var episode = m.Groups["Episode"].Value;
                var episodeNum = 0;
                bool isSpecial = false;

                if (!string.IsNullOrEmpty(episode) && !char.IsDigit(episode[0]))
                {
                    int i = 0;
                    isSpecial = true;
                    for (; i < episode.Length; i++)
                    {
                        if (char.IsNumber(episode[i]))
                            break;
                    }
                    episode = episode.Substring(i);
                }

                if (!string.IsNullOrEmpty(episode))
                {
                    episodeNum = int.Parse(episode);
                }

                var data = new EpisodeData(
                    fileName,
                    m.Groups["Series"].Value.Replace('_', ' ').Trim(),
                    episodeNum,
                    container
                );

                if (m.Groups["Fansubber"].Success)
                    data.Fansubber = m.Groups["Fansubber"].Value;

                if (m.Groups["Season"].Success && int.TryParse(m.Groups["Season"].Value, out var season))
                    data.SeasonNumber = season;
                else if (string.IsNullOrEmpty(episode) || isSpecial)
                    data.SeasonNumber = 0;
                else if (m.Groups["Season"].Success)
                    continue;

                if (m.Groups["EpisodeName"].Success)
                    data.EpisodeName = m.Groups["EpisodeName"].Value.Replace('_', ' ').Trim();

                return data;
            }

            return null;
        }
    }
}
