namespace VideoConverter.Storage.Models
{
	using System.Collections.Generic;

	using VideoConverter.Core.Models;

	public sealed class FileQueue
	{
		public string AudioCodec { get; set; } = "libopus";
		public int Id { get; set; }
		public string InputParameters { get; set; } = string.Empty;
		public string? NewHash { get; set; }
		public string? OldHash { get; set; }
		public string OutputPath { get; set; } = string.Empty;
		public string Parameters { get; set; } = string.Empty;
		public string Path { get; set; } = string.Empty;
		public bool SkipThumbnails { get; set; }
		public QueueStatus Status { get; set; }
		public string? StatusMessage { get; set; }
		public StereoScopicMode StereoMode { get; set; }
		public List<int> Streams { get; set; } = new List<int>();
		public string SubtitleCodec { get; set; } = "copy";
		public string VideoCodec { get; set; } = "hevc";
	}
}
