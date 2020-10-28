namespace VideoConverter.Storage.Models
{
    using System;
    using System.Collections.Generic;

    public sealed class FileQueue
    {
        public int Id { get; set; }
        public string Path { get; set; } = string.Empty;
        public List<int> Streams { get; set; } = new List<int>();
        public string AudioCodec { get; set; } = "libopus";
        public string VideoCodec { get; set; } = "hevc";
        public string SubtitleCodec { get; set; } = "copy";
        public string OutputPath { get; set; } = string.Empty;

        public QueueStatus Status { get; set; }
        public string? StatusMessage { get; set; }

        public string? OldHash { get; set; }
        public string? NewHash { get; set; }
    }
}
