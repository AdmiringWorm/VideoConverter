using System.Linq;
namespace VideoConverter.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Spectre.Cli;
    using Spectre.Console;
    using VideoConverter.Options;
    using VideoConverter.Storage.Models;
    using VideoConverter.Storage.Repositories;

    public sealed class AddPrefixCommand : Command<AddPrefixOption>
    {
        private readonly ConfigurationRepository configRepo;
        private readonly IAnsiConsole console;

        public AddPrefixCommand(ConfigurationRepository configRepo, IAnsiConsole console)
        {
            this.configRepo = configRepo;
            this.console = console;
        }

        public override int Execute(CommandContext context, AddPrefixOption settings)
        {
            var config = this.configRepo.GetConfiguration();
            if (config.Prefixes is null)
                config.Prefixes = new List<PrefixConfiguration>();

            var prefixConfig = config.Prefixes.FirstOrDefault(p => string.Compare(p.Prefix, settings.Prefix, StringComparison.OrdinalIgnoreCase) == 0);
            if (prefixConfig is null)
                config.Prefixes.Add(new PrefixConfiguration { Prefix = settings.Prefix, Path = Path.GetFullPath(settings.DirectoryPath) });
            else
                prefixConfig.Path = Path.GetFullPath(settings.DirectoryPath);

            this.configRepo.SaveConfiguration(config);

            this.console.MarkupLine("[green]Successfully added prefix [fuchsia]{0}[/] with path [fuchsia]{1}[/][/]",
                settings.Prefix.EscapeMarkup(),
                settings.DirectoryPath.EscapeMarkup());

            return 0;
        }
    }
}
