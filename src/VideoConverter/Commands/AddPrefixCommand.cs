using System.Linq;
namespace VideoConverter.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Spectre.Cli;
    using Spectre.Console;
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

        public override int Execute(CommandContext context, AddPrefixOption settings)
        {
            var config = this.configService.GetConfiguration();
            if (config.Prefixes is null)
                config.Prefixes = new List<PrefixConfiguration>();

            var prefixConfig = config.Prefixes.FirstOrDefault(p => string.Compare(p.Prefix, settings.Prefix, StringComparison.OrdinalIgnoreCase) == 0);
            if (prefixConfig is null)
                config.Prefixes.Add(new PrefixConfiguration { Prefix = settings.Prefix, Path = Path.GetFullPath(settings.DirectoryPath) });
            else
                prefixConfig.Path = Path.GetFullPath(settings.DirectoryPath);

            this.configService.SetConfiguration(config);

            this.console.MarkupLine("[green]Successfully added prefix [fuchsia]{0}[/] with path [fuchsia]{1}[/][/]",
                settings.Prefix.EscapeMarkup(),
                settings.DirectoryPath.EscapeMarkup());

            return 0;
        }
    }
}
