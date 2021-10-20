namespace VideoConverter.Core.Models
{
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

#pragma warning disable CA1724

	public class Configuration
	{
		private readonly List<FansubberConfiguration> _ignoreVideosWithFansubbers;
		private readonly List<PrefixConfiguration> _prefixes;

		public Configuration()
		{
			WorkDirectory = Path.GetTempPath();
			_prefixes = new List<PrefixConfiguration>();
			_ignoreVideosWithFansubbers = new List<FansubberConfiguration>();
		}

		public string AudioCodec { get; set; } = "libopus";
		public string ExtraEncodingParameters { get; set; } = string.Empty;

		public List<FansubberConfiguration> Fansubbers
		{
			get => _ignoreVideosWithFansubbers;
			set
			{
				_ignoreVideosWithFansubbers.Clear();
				_ignoreVideosWithFansubbers.AddRange(value.OrderBy(v => v.Name));
			}
		}

		public string FileType { get; set; } = "Matroska";
		public bool IncludeFansubber { get; set; } = true;

		public string? MapperDatabase { get; set; }

		public List<PrefixConfiguration> Prefixes
		{
			get => _prefixes;
			set
			{
				_prefixes.Clear();
				_prefixes.AddRange(value.OrderBy(v => v.Prefix));
			}
		}

		public string SubtitleCodec { get; set; } = "copy";
		public string VideoCodec { get; set; } = "hevc";
		public string WorkDirectory { get; set; }
#pragma warning disable CA2227
	}
}
