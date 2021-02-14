namespace VideoConverter.Options
{
	using System.ComponentModel;
	using Spectre.Console;
	using Spectre.Console.Cli;

	public class AddCriteriaOption : CommandSettings
	{
		[CommandArgument(0, "<FILE_PATH>")]
		[Description("The path or name of the file to use as a criteria. [bold]The file do not need to exist[/]")]
		public string FilePath { get; set; } = string.Empty;

		[CommandOption("--series <SERIES_NAME>")]
		[Description("The name of the series to map the to.")]
		public string? SeriesName { get; set; }

		[CommandOption("--season <SEASON_NUMBER>")]
		[Description("The season number to map to.")]
		public int? SeasonNumber { get; set; }

		[CommandOption("--episode <EPISODE_NUMBER>")]
		[Description("The episode number to map to. [bold]Will not be taken into if a season is not specified, and there is no season in the file name[/]")]
		public int? EpisodeNumber { get; set; }

		public override ValidationResult Validate()
		{
			if (string.IsNullOrEmpty(SeriesName) && SeasonNumber is null && EpisodeNumber is null)
				return ValidationResult.Error("You must specify either a series name or a season number (optionally also a episode number)");

			return base.Validate();
		}
	}
}
