namespace VideoConverter.Core.Models
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml.Serialization;

	[XmlRoot("Configuration")]
	public class ConverterConfiguration
	{
		private readonly List<FansubberConfiguration> _ignoreVideosWithFansubbers;
		private readonly List<PrefixConfiguration> _prefixes;
		private List<string>? _videoCodecs;

		public ConverterConfiguration()
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

		[Obsolete("The property is obsolete, and will be removed in the future.")]
		public string VideoCodec
		{
			get
			{
				return VideoCodecs[0];
			}
			set
			{
				VideoCodecs.Insert(0, value);
			}
		}

		public string WorkDirectory { get; set; }

		public List<string> VideoCodecs
		{
			get
			{
				if (_videoCodecs is null || _videoCodecs.Count == 0)
				{
					_videoCodecs = new List<string>
					{
						"hevc"
					};
				}

				return _videoCodecs;
			}
			set
			{
				_videoCodecs = value;
			}
		}

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
			WorkDirectory = Path.GetTempPath();
			VideoCodecs.Clear();
		}
	}
}
