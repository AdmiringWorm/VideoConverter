namespace VideoConverter.Options
{
	using System;
	using System.ComponentModel;
	using Spectre.Cli;
	using VideoConverter.Core.Models;
	using VideoConverter.Storage.Models;

	public class AddFileOption : CommandSettings
	{
		[CommandArgument(0, "<FILE_PATH>")]
		[Description("The path to the file to add to the queue. [bold]Multiple paths can be used[/]")]
		public string[] Files { get; set; } = Array.Empty<string>();

		[CommandOption("-o|--output <OUTPUT_PATH>")]
		[Description("The path to the location where the encoded file will be located")]
		public string? OutputPath { get; set; }

		[CommandOption("-d|--dir <OUTPUT_DIRECTORY>")]
		[Description("The base directory where outputs will be located.\n[italic]Directories for TV Series/Movie and a Season directory will be relative to this path![/]")]
		public string? OutputDir { get; set; }

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
		[Description("Additional parameters that should be passed when calling ffmpeg (by default, the vaules in global configuration is used)")]
		public string[] Parameters { get; set; } = Array.Empty<string>();

		[CommandOption("--use-copy|--allow-copy")]
		[Description("Use encoding copy when target and source uses same codec")]
		public bool UseEncodingCopy { get; set; }

		[CommandOption("--re-encode|--reencode")]
		[Description("Pure re-encode of the of the file name (allows re-using the same filename, without a output directory)")]
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

		public override ValidationResult Validate()
		{
			if (string.IsNullOrEmpty(OutputPath) && string.IsNullOrEmpty(OutputDir) && !ReEncode)
			{
				return ValidationResult.Error("A output path, or an output directory is required!");
			}

			if (Files.Length > 1 && !string.IsNullOrEmpty(OutputPath) && !ReEncode)
			{
				return ValidationResult.Error("A output path can not be used with several files!");
			}

			return base.Validate();
		}
	}
}
