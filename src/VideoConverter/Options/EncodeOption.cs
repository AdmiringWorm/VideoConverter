namespace VideoConverter.Options
{
	using System;
	using System.ComponentModel;
	using Spectre.Cli;

	public class EncodeOption : CommandSettings
	{
		[CommandOption("--include-failed|--include-failing|--failed|--failing")]
		[Description("Also include files that previously failed encoding")]
		public bool IncludeFailing { get; set; }

		[CommandOption("--id|--ids")]
		[Description("Only encode the following indexes")]
		public int[] Indexes { get; set; } = Array.Empty<int>();

		[CommandOption("-r|--remove|--remove-file|--remove-old")]
		[Description("Remove any old file when there is a successful encoding")]
		public bool RemoveOldFiles { get; set; }

		[CommandOption("--ignore-duplicates|--skip-duplicates")]
		[Description("Ignore any duplicate entries found after encoding")]
		public bool IgnoreDuplicates { get; set; }

		[CommandOption("--use-copy|--allow-copy")]
		[Description("Use encoding copy when target and source uses same codec")]
		public bool UseEncodingCopy { get; set; }

		[CommandOption("--monitor|--monitor-db|--monitor-database")]
		[Description("Do not exit the application, but monitor for additional changes")]
		public bool MonitorDatabase { get; set; }

		[CommandOption("--no-stereo-meta|--no-stereo-metadata|--no-stereo-mode")]
		[Description("Do not set the stereo_mode metadata in the output")]
		public bool NoStereoModeMetadata { get; set; }
	}
}
