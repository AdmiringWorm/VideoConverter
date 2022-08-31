using System.ComponentModel;

using Spectre.Console.Cli;

namespace VideoConverter.Options
{
	public class ConfigOption : CommandSettings
	{
		[CommandArgument(0, "[CONFIG_NAME]")]
		[Description("The name of the configuration value to update. [bold]Leave empty to list all configurations[/]")]
		public string Name { get; set; } = string.Empty;

		[CommandArgument(1, "[CONFIG_VALUE]")]
		[Description("The value of the configuration value to update. [bold]Leave empty to see the value instead[/]")]
		public string? Value { get; set; }
	}
}
