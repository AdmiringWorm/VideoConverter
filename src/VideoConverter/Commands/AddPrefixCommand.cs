namespace VideoConverter.Commands
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;

	using Spectre.Console;
	using Spectre.Console.Cli;

	using VideoConverter.Core.Models;
	using VideoConverter.Core.Services;
	using VideoConverter.Options;

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
			var config = configService.GetConfiguration();
			config.Prefixes ??= new List<PrefixConfiguration>();

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

			configService.SetConfiguration(config);

			console.MarkupLine("[green]Successfully added prefix [fuchsia]{0}[/] with path [fuchsia]{1}[/][/]",
				settings.Prefix.EscapeMarkup(),
				settings.DirectoryPath.EscapeMarkup());

			return 0;
		}
	}
}
