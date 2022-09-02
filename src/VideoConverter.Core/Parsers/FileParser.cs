namespace VideoConverter.Core.Parsers
{
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text.RegularExpressions;

	using VideoConverter.Core.Assertions;
	using VideoConverter.Core.Extensions;
	using VideoConverter.Core.Models;

	public static class FileParser
	{
		private const string SPACE = @"[\s_]*";
		private static readonly LinkedList<string> episodeRegexes = new();

		static FileParser()
		{
			// editorconfig-checker-disable
			episodeRegexes.AddLast($@"^{SPACE}[\[\(]{SPACE}(?<Fansubber>[^\]\)]+)[\s*_]*[\]\)]{SPACE}(?<Series>.+){SPACE}S(?<Season>\d+){SPACE}-{SPACE}(?<Episode>\d+)[^\.]*(H\.265[^\.]*)?\.(?<Extension>[a-z\d]+)$");
			episodeRegexes.AddLast($@"^{SPACE}[\[\(]{SPACE}(?<Fansubber>[^\]\)]+)[\s*_]*[\]\)]{SPACE}(?<Series>.+){SPACE}-{SPACE}(?<Episode>\d+)[^\.]*(H\.265[^\.]*)?\.(?<Extension>[a-z\d]+)$");
			episodeRegexes.AddLast($@"^{SPACE}[\[\(]{SPACE}(?<Fansubber>[^\]\)]+){SPACE}[\]\)]{SPACE}(?<Series>.+){SPACE}S(?<Season>\d+){SPACE}-{SPACE}(?<Episode>\d+|(?:OVA|OAV) ?\d*)[^\.]*\.(?<Extension>[a-z\d]+)$");
			episodeRegexes.AddLast($@"^{SPACE}[\[\(]{SPACE}(?<Fansubber>[^\]\)]+){SPACE}[\]\)]{SPACE}(?<Series>.+){SPACE}-{SPACE}(?<Episode>\d+|(?:OVA|OAV) ?\d*)[^\.]*\.(?<Extension>[a-z\d]+)$");
			episodeRegexes.AddLast($@"^{SPACE}[\[\(]{SPACE}(?<Fansubber>[^\]\)]+){SPACE}[\]\)]{SPACE}(?<Series>.+){SPACE}-{SPACE}S(?<Season>\d+)E(?<Episode>\d+)(?:{SPACE}-{SPACE}(?<EpisodeName>[^\.]+){SPACE}|[^\.]*)\.(?<Extension>[a-z\d]+)$");
			episodeRegexes.AddLast($@"^{SPACE}(?<Series>.+){SPACE}-{SPACE}S(?<Season>\d+)E(?<Episode>\d+){SPACE}-{SPACE}(?<EpisodeName>[^\.]+)\.(?<Extension>[a-z\d]+)$");
			episodeRegexes.AddLast($@"^(?<Series>.+){SPACE}(?:S(?<Season>\d+)){SPACE}-{SPACE}(?<Episode>\d+){SPACE}\.(?<Extension>[a-z\d]+)$");
			episodeRegexes.AddLast($@"^{SPACE}(?<Series>.+){SPACE}S(?<Season>\d+){SPACE}-{SPACE}(?<Episode>\d+|(?:OVA|OAV) ?\d*)[^\.]*\.(?<Extension>[a-z\d]+)$");
			episodeRegexes.AddLast($@"^(?:{SPACE}[\[\(]{SPACE}(?<Fansubber>[^\]\)]+){SPACE}[\]\)]{SPACE})?(?<Series>.+)\s*-\s*(?<Season>\d+)x(?<Episode>\d+)(?:{SPACE}-{SPACE}(?<EpisodeName>[^\.]+){SPACE}|[^\.]*)\.(?<Extension>[a-z\d]+)$");
			episodeRegexes.AddLast($@"^{SPACE}(?<Series>.+){SPACE}-{SPACE}(?<Episode>\d+|(?:OVA|OAV) ?\d*)[^\.]*\.(?<Extension>[a-z\d]+)$");
			episodeRegexes.AddLast($@"^{SPACE}(?<Series>.+){SPACE}-{SPACE}S(?<Season>\d+)E(?<Episode>\d+)(?:{SPACE}-{SPACE}(?<EpisodeName>[^\.]+){SPACE}|[^\.]*)\.(?<Extension>[a-z\d]+)$");
			episodeRegexes.AddLast($@"^{SPACE}[\[\(]{SPACE}(?<Fansubber>[^\]\)]+){SPACE}[\]\)]{SPACE}(?<Series>[^\[]+)[\s_]+(?<Episode>\d+|(?:OVA|OAV) ?\d*)[^\.]*\.(?<Extension>[a-z\d]+)$");
			episodeRegexes.AddLast($@"^(?:{SPACE}[\[\(]{SPACE}(?<Fansubber>[^\]\)]+){SPACE}[\]\)]{SPACE})?(?<Series>.+)(?<Season>\d+)x(?<Episode>\d+)(?:{SPACE}-{SPACE}(?<EpisodeName>[^\.]+){SPACE}|[^\.]*)\.(?<Extension>[a-z\d]+)$");
			episodeRegexes.AddLast($@"^(?:{SPACE}[\[\(]{SPACE}(?<Fansubber>[^\]\)]+){SPACE}[\]\)]{SPACE})?(?<Series>.+)S(?<Season>\d+)E(?<Episode>\d+)(?:{SPACE}-{SPACE}(?<EpisodeName>[^\.]+){SPACE}|[^\.]*)\.(?<Extension>[a-z\d]+)$");
			// editorconfig-checker-enable
		}

		public static EpisodeData? ParseEpisode(string fileName)
		{
			fileName.AssertNotWhitespace();

			var index = fileName.LastIndexOfAny(new[] { '/', '\\' });
			if (index > 0)
			{
				fileName = fileName[(index + 1)..];
			}

			foreach (var regex in episodeRegexes)
			{
				var m = Regex.Match(
					fileName,
					regex,
					RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
				);
				if (!m.Success)
				{
					continue;
				}

				var container = m.Groups["Extension"].Value.GetExtensionFileType();

				var episode = m.Groups["Episode"].Value;
				var episodeNum = 0;
				var isSpecial = false;

				if (!string.IsNullOrEmpty(episode) && !char.IsDigit(episode[0]))
				{
					var i = 0;
					isSpecial = true;
					for (; i < episode.Length; i++)
					{
						if (char.IsNumber(episode[i]))
						{
							break;
						}
					}
					episode = episode[i..];
				}

				if (!string.IsNullOrEmpty(episode))
				{
					episodeNum = int.Parse(episode, CultureInfo.InvariantCulture);
				}

				var data = new EpisodeData(
					fileName,
					m.Groups["Series"].Value.Replace('_', ' ').Trim(),
					episodeNum,
					container
				);

				if (m.Groups["Fansubber"].Success)
				{
					data.Fansubber = m.Groups["Fansubber"].Value;
				}

				if (m.Groups["Season"].Success && int.TryParse(m.Groups["Season"].Value, out var season))
				{
					data.SeasonNumber = season;
				}
				else if (string.IsNullOrEmpty(episode) || isSpecial)
				{
					data.SeasonNumber = 0;
				}
				else if (m.Groups["Season"].Success)
				{
					continue;
				}

				if (m.Groups["EpisodeName"].Success)
				{
					data.EpisodeName = m.Groups["EpisodeName"].Value.Replace('_', ' ').Trim();
				}

				return data;
			}

			return null;
		}
	}
}
