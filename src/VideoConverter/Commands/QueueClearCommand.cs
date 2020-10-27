namespace VideoConverter.Commands
{
    using System;
    using Humanizer;
    using Spectre.Cli;
    using Spectre.Console;
    using VideoConverter.Options;
    using VideoConverter.Storage.Models;
    using VideoConverter.Storage.Repositories;

    public class QueueClearCommand : Command<QueueClearOption>
    {
        private readonly QueueRepository queueRepo;
        private readonly IAnsiConsole console;

        public QueueClearCommand(QueueRepository queueRepo, IAnsiConsole console)
        {
            this.queueRepo = queueRepo;
            this.console = console;
        }

        public override int Execute(CommandContext context, QueueClearOption settings)
        {
            var statusCount = this.queueRepo.RemoveQueueItems(settings.Status);

            this.queueRepo.SaveChanges();

            this.console.MarkupLine("[aqua]We have removed {0}[/]", "item".ToQuantity(statusCount));

            return 0;
        }
    }
}
