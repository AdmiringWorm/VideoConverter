namespace VideoConverter.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Spectre.Cli;
    using Spectre.Console;
    using VideoConverter.Options;
    using VideoConverter.Storage.Models;
    using VideoConverter.Storage.Repositories;

    public class QueueRemoveCommand : AsyncCommand<QueueRemoveOption>
    {
        private readonly QueueRepository queueRepo;
        private readonly IAnsiConsole console;

        public QueueRemoveCommand(QueueRepository queueRepo, IAnsiConsole console)
        {
            this.queueRepo = queueRepo;
            this.console = console;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, QueueRemoveOption settings)
        {
            var removed = new List<int>();

            for (int i = 0; i < settings.Identifiers.Length; i++)
            {
                var item = await this.queueRepo.GetQueueItemAsync(settings.Identifiers[i]);

                if (item is null)
                {
                    await this.queueRepo.AbortChangesAsync();
                    throw new Exception($"We could not find any item with the id {settings.Identifiers[i]}!");
                }

                if (item.Status == QueueStatus.Encoding)
                    throw new Exception($"We were unable to remove the item with the id {settings.Identifiers[i]}! It is already being encoded!");

                await this.queueRepo.RemoveQueueItemAsync(item.Id);

                removed.Add(settings.Identifiers[i]);
            }

            await this.queueRepo.SaveChangesAsync();

            foreach (var index in removed)
            {
                this.console.MarkupLine("[darkcyan]We successfully removed the queue item with the identifier {0} from the queue![/]", index);
            }

            return 0;
        }
    }
}
