namespace VideoConverter.Options
{
	using System;
	using System.ComponentModel;
	using System.Globalization;
	using Spectre.Console;
	using Spectre.Console.Cli;
	using VideoConverter.Core.Models;
	using VideoConverter.Storage.Models;

	public class AddDirectoryOption : CommandSettings
	{
		[CommandArgument(0, "<DIRECTORY_PATH>")]
		[Description("The path to the directory containing video files. [bold]Multiple paths can be used[/].")]
		public string[] Directories { get; set; } = Array.Empty<string>();

		[CommandOption("-o|-d|--output|--dir <OUTPUT_DIRECTORY>")]
		[Description(
			"The base directory where outputs will be located.\n" +
			"[italic]Directories for TV Series/Movie and a Season directory will be relative to this path![/]"
		)]
		public string OutputDirectory { get; set; } = string.Empty;

		[CommandOption("-r|--recurse|--recursive")]
		[Description("Do a recursive search for files in all sub directories")]
		public bool RecursiveSearch { get; set; }

		[CommandOption("--vcodec <CODEC>")]
		[Description("The video codec to use for the added files, useful to override global configuration.")]
		public string? VideoCodec { get; set; }

		[CommandOption("--acodec <CODEC>")]
		[Description("The audio codec to use for the added files, useful to override global configuration.")]
		public string? AudioCodec { get; set; }

		[CommandOption("--scodec <CODEC>")]
		[Description("The subtitle codec to use for the added files, useful to override global configuration.")]
		public string? SubtitleCodec { get; set; }

		[CommandOption("--parameters <PARAMETERS>")]
		[Description(
			"Additional parameters that should be passed when calling ffmpeg " +
			"(by default, the vaules in global configuration is used)"
		)]
		public string[] Parameters { get; set; } = Array.Empty<string>();

		[CommandOption("--use-copy|--allow-copy")]
		[Description("Use encoding copy when target and source uses same codec")]
		public bool UseEncodingCopy { get; set; }

		[CommandOption("--re-encode|--reencode")]
		[Description(
			"Pure re-encode of the of the file name (allows re-using the same filename, without a output directory)"
		)]
		public bool ReEncode { get; set; }

		[CommandOption("--remove-duplicates")]
		[Description("Remove any duplicate files that have already been added to the queue")]
		public bool RemoveDuplicates { get; set; }

		[CommandOption("--ignore-duplicates")]
		[Description("Ignore any duplicate files that have already been added to the queue")]
		public bool IgnoreDuplicates { get; set; }

		[CommandOption("--ignore")]
		[Description("The statuses that should be ignored when adding new queue items")]
		public QueueStatus[] IgnoreStatuses { get; set; } = new[] { QueueStatus.Encoding };

		[CommandOption("--extension")]
		[Description("The file extension to use when encoding files")]
		public string? FileExtension { get; set; }

		[CommandOption("--stereo-mode|--stereo")]
		[Description("The movie is in 3D with the following stereoscopic view")]
		public StereoScopicMode StereoMode { get; set; }

		[CommandOption("--repeat")]
		[Description(
			"Repeat the movie until it reaches a certain threshold (value can be a timespan, or a number of repeated loops)"
		)]
		public string? Repeat { get; set; }

		public override ValidationResult Validate()
		{
			if (string.IsNullOrEmpty(OutputDirectory) && !ReEncode)
			{
				return ValidationResult.Error("A output directory is required!");
			}

			if (!string.IsNullOrEmpty(Repeat) &&
				!TimeSpan.TryParse(
					Repeat,
					CultureInfo.InvariantCulture,
					out _) &&
				!int.TryParse(
					Repeat,
					NumberStyles.Integer,
					CultureInfo.InvariantCulture,
					out _)
			)
			{
				return ValidationResult.Error("The repeat argument must either be a timespan, or a number of loops");
			}

			return base.Validate();
		}
	}
}
