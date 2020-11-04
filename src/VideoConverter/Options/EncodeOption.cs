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
	}
}
