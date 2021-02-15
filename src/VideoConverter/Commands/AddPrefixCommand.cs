namespace VideoConverter.Commands
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using Spectre.Console.Cli;
	using Spectre.Console;
	using VideoConverter.Core.Models;
	using VideoConverter.Core.Services;
	using VideoConverter.Options;
	using System.Diagnostics.CodeAnalysis;

	public sealed class AddPrefixCommand : Command<AddPrefixOption>
	{
		private readonly IConfigurationService configService;
		private readonly IAnsiConsole console;

		public AddPrefixCommand(IConfigurationService configService, IAnsiConsole console)
		{
			this.configService = configService;
			this.console = console;
		}

		public override int Execute([NotNull] CommandContext context, [NotNull] AddPrefixOption settings)
		{
			var config = this.configService.GetConfiguration();
			if (config.Prefixes is null)
				config.Prefixes = new List<PrefixConfiguration>();

			var prefixConfig = config.Prefixes.Find(
				p => string.Equals(p.Prefix, settings.Prefix, StringComparison.OrdinalIgnoreCase)
			);
			if (prefixConfig is null)
			{
				config.Prefixes.Add(
					new PrefixConfiguration
					{
						Prefix = settings.Prefix,
						Path = Path.GetFullPath(settings.DirectoryPath)
					}
				);
			}
			else
			{
				prefixConfig.Path = Path.GetFullPath(settings.DirectoryPath);
			}

			this.configService.SetConfiguration(config);

			this.console.MarkupLine("[green]Successfully added prefix [fuchsia]{0}[/] with path [fuchsia]{1}[/][/]",
				settings.Prefix.EscapeMarkup(),
				settings.DirectoryPath.EscapeMarkup());

			return 0;
		}
	}
}
