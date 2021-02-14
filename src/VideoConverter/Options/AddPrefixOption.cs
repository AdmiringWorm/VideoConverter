namespace VideoConverter.Options
{
	using System.ComponentModel;
	using Spectre.Console.Cli;

	public sealed class AddPrefixOption : CommandSettings
	{
		[CommandArgument(0, "<PREFIX>")]
		[Description("The prefix to use")]
		public string Prefix { get; set; } = string.Empty;

		[CommandArgument(0, "<DIRECTORY_PATH>")]
		[Description("The directory path to use for the prefix")]
		public string DirectoryPath { get; set; } = string.Empty;
	}
}
