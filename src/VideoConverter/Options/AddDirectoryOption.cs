namespace VideoConverter.Options
{
	using System;
	using System.ComponentModel;
	using Spectre.Console;
	using Spectre.Console.Cli;

	public class AddDirectoryOption : AddBaseOptions
	{
		private string[] directories = Array.Empty<string>();

		[CommandArgument(0, "<DIRECTORY_PATH>")]
		[Description("The path to the directory containing video files. [bold]Multiple paths can be used[/].")]
		public string[] Directories
		{
			get => directories;
			set
			{
				if (value is null)
				{
					directories = Array.Empty<string>();
				}
				else
				{
					directories = value;
				}
			}
		}

		[CommandOption("-o|-d|--output|--dir <OUTPUT_DIRECTORY>")]
		[Description(
			"The base directory where outputs will be located.\n" +
			"[italic]Directories for TV Series/Movie and a Season directory will be relative to this path![/]"
		)]
		public string OutputDirectory { get; set; } = string.Empty;

		[CommandOption("-r|--recurse|--recursive")]
		[Description("Do a recursive search for files in all sub directories")]
		public bool RecursiveSearch { get; set; }

		public override ValidationResult Validate()
		{
			if (string.IsNullOrEmpty(OutputDirectory) && !ReEncode)
			{
				return ValidationResult.Error("A output directory is required!");
			}

			return base.Validate();
		}
	}
}
