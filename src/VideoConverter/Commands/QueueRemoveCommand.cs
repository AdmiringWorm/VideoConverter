namespace VideoConverter.Commands
{
    using System;
    using System.Collections.Generic;
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
            var removed = new List<int>();

            for (int i = 0; i < settings.Identifiers.Length; i++)
            {
                var item = this.queueRepo.GetQueueItem(settings.Identifiers[i]);

                if (item is null)
                {
                    this.queueRepo.AbortChanges();
                    throw new Exception($"We could not find any item with the id {settings.Identifiers[i]}!");
                }

                if (item.Status == QueueStatus.Encoding)
                    throw new Exception($"We were unable to remove the item with the id {settings.Identifiers[i]}! It is already being encoded!");

                this.queueRepo.RemoveQueueItem(item.Id);

                removed.Add(settings.Identifiers[i]);
            }

            this.queueRepo.SaveChanges();

            foreach (var index in removed)
            {
                this.console.MarkupLine("[darkcyan]We successfully removed the queue item with the identifier {0} from the queue![/]", index);
            }

            return 0;
        }
    }
}
