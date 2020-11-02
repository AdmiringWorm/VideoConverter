namespace VideoConverter.Commands
{
    using System.Threading.Tasks;
    using Humanizer;
    using Spectre.Cli;
    using Spectre.Console;
    using VideoConverter.Options;
    using VideoConverter.Storage.Repositories;

    public class QueueClearCommand : AsyncCommand<QueueClearOption>
    {
        private readonly QueueRepository queueRepo;
        private readonly IAnsiConsole console;

        public QueueClearCommand(QueueRepository queueRepo, IAnsiConsole console)
        {
            this.queueRepo = queueRepo;
            this.console = console;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, QueueClearOption settings)
        {
            var statusCount = await this.queueRepo.RemoveQueueItemsAsync(settings.Status);

            await this.queueRepo.SaveChangesAsync();

            this.console.MarkupLine("[aqua]We have removed {0}[/]", "item".ToQuantity(statusCount));

            return 0;
        }
    }
}
