namespace VideoConverter.Storage.Models
{
    using System.IO;
    using System;

    [Serializable]
    public class Configuration
    {
        public Configuration()
        {
            WorkDirectory = Path.GetTempPath();
        }

        public bool IncludeFansubber { get; set; } = true;

        public string VideoCodec { get; set; } = "hevc";
        public string AudioCodec { get; set; } = "libopus";
        public string SubtitleCodec { get; set; } = "copy";

        public string? MapperDatabase { get; set; }

        public string WorkDirectory { get; set; }
    }
}
