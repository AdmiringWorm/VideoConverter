namespace VideoConverter.Options
{
	using System;
	using System.ComponentModel;
	using Spectre.Console;
	using Spectre.Console.Cli;

	public class AddFileOption : AddBaseOptions
	{
		private string[] files = Array.Empty<string>();

		[CommandArgument(0, "<FILE_PATH>")]
		[Description("The path to the file to add to the queue. [bold]Multiple paths can be used[/]")]
		public string[] Files
		{
			get => files;
			set
			{
				if (value is null)
				{
					files = Array.Empty<string>();
				}
				else
				{
					files = value;
				}
			}
		}

		[CommandOption("-d|--dir <OUTPUT_DIRECTORY>")]
		[Description(
			"The base directory where outputs will be located.\n" +
			"[italic]Directories for TV Series/Movie and a Season directory will be relative to this path![/]"
		)]
		public string? OutputDir { get; set; }

		[CommandOption("-o|--output <OUTPUT_PATH>")]
		[Description("The path to the location where the encoded file will be located")]
		public string? OutputPath { get; set; }

		public static AddFileOption FromDirectoryOptions(AddDirectoryOption directoryOptions, params string[] files)
		{
			var fileOptions = new AddFileOption
			{
				AudioCodec = directoryOptions.AudioCodec,
				FileExtension = directoryOptions.FileExtension,
				Files = files,
				IgnoreDuplicates = directoryOptions.IgnoreDuplicates,
				IgnoreStatuses = directoryOptions.IgnoreStatuses,
				OutputDir = directoryOptions.OutputDirectory,
				Parameters = directoryOptions.Parameters,
				ReEncode = directoryOptions.ReEncode,
				RemoveDuplicates = directoryOptions.RemoveDuplicates,
				Repeat = directoryOptions.Repeat,
				StereoMode = directoryOptions.StereoMode,
				SubtitleCodec = directoryOptions.SubtitleCodec,
				UseEncodingCopy = directoryOptions.UseEncodingCopy,
				VideoCodec = directoryOptions.VideoCodec,
			};

			return fileOptions;
		}

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
