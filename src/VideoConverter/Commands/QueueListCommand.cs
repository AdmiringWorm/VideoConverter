using System.IO;
namespace VideoConverter.Commands
{
    using System.Linq;
    using Humanizer;
    using Spectre.Cli;
    using Spectre.Console;
    using VideoConverter.Extensions;
    using VideoConverter.Options;
    using VideoConverter.Storage.Repositories;

    public class QueueListCommand : Command<QueueListOption>
    {
        private readonly QueueRepository queueRepo;
        private readonly IAnsiConsole console;

        public QueueListCommand(QueueRepository queueRepo, IAnsiConsole console)
        {
            this.queueRepo = queueRepo;
            this.console = console;
        }

        public override int Execute(CommandContext context, QueueListOption settings)
        {
            if (settings.CountOnly)
            {
                var itemCount = this.queueRepo.GetQueueItemCount(settings.Status);

                if (settings.Status is null)
                {
                    this.console.MarkupLine("[aqua]There are {0} items in total in the queue![/]", itemCount);
                }
                else
                {
                    this.console.MarkupLine("[aqua]There are {0} items in the {1} queue![/]", itemCount, settings.Status);
                }
            }
            else
            {
                var items = this.queueRepo.GetQueueItems(settings.Status).ToList();

                var table = new Table()
                    .SetDefaults()
                    .AddColumns(
                        new TableColumn("ID").RightAligned(),
                        new TableColumn("Status").Centered(),
                        new TableColumn("New Name")
                    );

                foreach (var item in items)
                {
                    var statusMessage = item.StatusMessage?.Length > 20 ? item.StatusMessage.Substring(0, 20) : item.StatusMessage;
                    table.AddColorRow(item.Id, item.Status, Path.GetFileName(item.OutputPath));
                }

                this.console.RenderTable(table, $"Found {"item".ToQuantity(items.Count)}");
            }

            return 0;
        }
    }
}
