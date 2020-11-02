namespace VideoConverter.Core.Models
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    public class Configuration
    {
        private List<PrefixConfiguration> prefixes;

        public Configuration()
        {
            WorkDirectory = Path.GetTempPath();
            prefixes = new List<PrefixConfiguration>();
        }

        public bool IncludeFansubber { get; set; } = true;

        public string VideoCodec { get; set; } = "hevc";
        public string AudioCodec { get; set; } = "libopus";
        public string SubtitleCodec { get; set; } = "copy";

        public string? MapperDatabase { get; set; }

        public string WorkDirectory { get; set; }

        public string FileType { get; set; } = "Matroska";

        public List<PrefixConfiguration> Prefixes
        {
            get => prefixes;
            set
            {
                prefixes.Clear();
                prefixes.AddRange(value.OrderBy(v => v.Prefix));
            }
        }
    }
}
