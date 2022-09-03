namespace VideoConverter.Core.Models
{
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml.Serialization;

	[XmlRoot("Configuration")]
	public class ConverterConfiguration
	{
		private readonly List<FansubberConfiguration> _ignoreVideosWithFansubbers;
		private readonly List<PrefixConfiguration> _prefixes;

		public ConverterConfiguration()
		{
			WorkDirectory = Path.GetTempPath();
			_prefixes = new List<PrefixConfiguration>();
			_ignoreVideosWithFansubbers = new List<FansubberConfiguration>();
		}

		public string AudioCodec { get; set; } = "libopus";
		public string ExtraEncodingParameters { get; set; } = string.Empty;

#pragma warning disable CA2227 // Collection properties should be read only
#pragma warning disable CA1002 // Do not expose generic lists

		public List<FansubberConfiguration> Fansubbers
#pragma warning restore CA1002 // Do not expose generic lists
#pragma warning restore CA2227 // Collection properties should be read only
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

#pragma warning disable CA2227 // Collection properties should be read only
#pragma warning disable CA1002 // Do not expose generic lists

		public List<PrefixConfiguration> Prefixes
#pragma warning restore CA1002 // Do not expose generic lists
#pragma warning restore CA2227 // Collection properties should be read only
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

		public void Reset()
		{
			AudioCodec = "libopus";
			ExtraEncodingParameters = string.Empty;
			Fansubbers.Clear();
			FileType = "Matroska";
			IncludeFansubber = true;
			MapperDatabase = null;
			Prefixes.Clear();
			SubtitleCodec = "copy";
			VideoCodec = "hevc";
			WorkDirectory = Path.GetTempPath();
		}
	}
}
