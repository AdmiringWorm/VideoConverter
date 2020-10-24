namespace VideoConverter.Commands
{
    using System;
    using Spectre.Cli;
    using Spectre.Console;
    using VideoConverter.Options;
    using VideoConverter.Storage.Models;
    using VideoConverter.Storage.Repositories;

    public class QueueRemoveCommand : Command<QueueRemoveOption>
    {
        private readonly QueueRepository queueRepo;
        private readonly IAnsiConsole console;

        public QueueRemoveCommand(QueueRepository queueRepo, IAnsiConsole console)
        {
            this.queueRepo = queueRepo;
            this.console = console;
        }

        public override int Execute(CommandContext context, QueueRemoveOption settings)
        {
            var item = this.queueRepo.GetQueueItem(settings.Identifier);

            if (item is null)
                throw new Exception($"We could not find any item with the id {settings.Identifier}!");

            if (item.Status == QueueStatus.Encoding)
                throw new Exception($"We were unable to remove the item with the id {settings.Identifier}! It is already being encoded!");

            this.queueRepo.RemoveQueueItem(item.Id);
            this.queueRepo.SaveChanges();

            this.console.MarkupLine("[darkcyan]We successfully removed the queue item with the identifier {0} from the queue![/]", settings.Identifier);

            return 0;
        }
    }
}
