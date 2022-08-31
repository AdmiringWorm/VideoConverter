namespace VideoConverter.Options
{
	using System;
	using System.ComponentModel;
	using Spectre.Console.Cli;

	public class EncodeOption : CommandSettings
	{
		private int[] indexes = Array.Empty<int>();

		[CommandOption("--ignore-duplicates|--skip-duplicates")]
		[Description("Ignore any duplicate entries found after encoding")]
		public bool IgnoreDuplicates { get; set; }

		[CommandOption("--include-failed|--include-failing|--failed|--failing")]
		[Description("Also include files that previously failed encoding")]
		public bool IncludeFailing { get; set; }

		[CommandOption("--id|--ids")]
		[Description("Only encode the following indexes")]
		public int[] Indexes
		{
			get => indexes;
			set
			{
				if (value is null)
				{
					indexes = Array.Empty<int>();
				}
				else
				{
					indexes = value;
				}
			}
		}

		[CommandOption("--monitor|--monitor-db|--monitor-database")]
		[Description("Do not exit the application, but monitor for additional changes")]
		public bool MonitorDatabase { get; set; }

		[CommandOption("--no-stereo-meta|--no-stereo-metadata|--no-stereo-mode")]
		[Description("Do not set the stereo_mode metadata in the output")]
		public bool NoStereoModeMetadata { get; set; }

		[CommandOption("-r|--remove|--remove-file|--remove-old")]
		[Description("Remove any old file when there is a successful encoding")]
		public bool RemoveOldFiles { get; set; }

		[CommandOption("--skip-thumbnails|--no-thumbnails")]
		[Description("After conversion, do not create any thumbnails together with the video")]
		public bool SkipThumbnails { get; set; }

		[CommandOption("--use-copy|--allow-copy")]
		[Description("Use encoding copy when target and source uses same codec")]
		public bool UseEncodingCopy { get; set; }
	}
}
