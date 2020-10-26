namespace VideoConverter.Options
{
    using System.ComponentModel;
    using Spectre.Cli;

    public class AddDirectoryOption : CommandSettings
    {
        [CommandArgument(0, "<DIRECTORY_PATH>")]
        [Description("The path to the directory containing video files. [bold]Multiple paths can be used[/].\n[italic]Only .mkv and .mp4 files will be collected[/]")]
        public string[] Directories { get; set; } = new string[0];

        [CommandOption("-o|-d|--output|--dir <OUTPUT_DIRECTORY>")]
        [Description("The base directory where outputs will be located.\n[italic]Directories for TV Series/Movie and a Season directory will be relative to this path![/]")]
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

        [CommandOption("--re-encode|--reencode")]
        [Description("Pure re-encode of the of the file name (allows re-using the same filename, without a output directory)")]
        public bool ReEncode { get; set; }

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
