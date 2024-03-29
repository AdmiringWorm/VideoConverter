namespace VideoConverter.Options
{
	using System;
	using System.ComponentModel;
	using System.Globalization;

	using Spectre.Console;
	using Spectre.Console.Cli;

	using VideoConverter.Core.Models;
	using VideoConverter.Storage.Models;

	public abstract class AddBaseOptions : CommandSettings
	{
		private QueueStatus[] ignoreStatuses = new[] { QueueStatus.Encoding };
		private string[] parameters = Array.Empty<string>();

		[CommandOption("--acodec <CODEC>")]
		[Description("The audio codec to use for the added files, useful to override global configuration.")]
		public string? AudioCodec { get; set; }

		[CommandOption("--extension")]
		[Description("The file extension to use when encoding files")]
		public string? FileExtension { get; set; }

		[CommandOption("--ignore-duplicates")]
		[Description("Ignore any duplicate files that have already been added to the queue")]
		public bool IgnoreDuplicates { get; set; }

		[CommandOption("--ignore")]
		[Description("The statuses that should be ignored when adding new queue items")]
		public QueueStatus[] IgnoreStatuses
		{
			get => ignoreStatuses;
			set
			{
				ignoreStatuses = value is null || value.Length == 0
					? (new[] { QueueStatus.Encoding })
					: value;
			}
		}

		[CommandOption("--parameters <PARAMETERS>")]
		[Description(
			"Additional parameters that should be passed when calling ffmpeg " +
			"(by default, the vaules in global configuration is used)"
		)]
		public string[] Parameters
		{
			get => parameters;
			set
			{
				parameters = value ?? Array.Empty<string>();
			}
		}

		[CommandOption("--re-encode|--reencode")]
		[Description(
			"Pure re-encode of the of the file name (allows re-using the same filename, without a output directory)"
		)]
		public bool ReEncode { get; set; }

		[CommandOption("--remove-duplicates")]
		[Description("Remove any duplicate files that have already been added to the queue")]
		public bool RemoveDuplicates { get; set; }

		[CommandOption("--repeat")]
		[Description(
			"Repeat the movie until it reaches a certain threshold (value can be a timespan, or a number of repeated loops)"
		)]
		public string? Repeat { get; set; }

		[CommandOption("--skip-thumbnails|--no-thumbnails")]
		[Description("After conversion, do not create any thumbnails together with the video")]
		public bool SkipThumbnails { get; set; }

		[CommandOption("--stereo-mode|--stereo")]
		[Description("The movie is in 3D with the following stereoscopic view")]
		public StereoScopicMode StereoMode { get; set; }

		[CommandOption("--scodec <CODEC>")]
		[Description("The subtitle codec to use for the added files, useful to override global configuration.")]
		public string? SubtitleCodec { get; set; }

		[CommandOption("--use-copy|--allow-copy")]
		[Description("Use encoding copy when target and source uses same codec")]
		public bool UseEncodingCopy { get; set; }

		[CommandOption("--vcodec <CODEC>")]
		[Description("The video codec to use for the added files, useful to override global configuration.")]
		public string? VideoCodec { get; set; }

		public override ValidationResult Validate()
		{
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
