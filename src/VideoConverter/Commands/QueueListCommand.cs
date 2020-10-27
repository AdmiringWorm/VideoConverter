namespace VideoConverter.Commands
{
    using System;
    using System.IO;
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


                try
                {
                    var items = this.queueRepo.GetQueueItems(settings.Status).ToList();

                    foreach (var item in items)
                    {
                        this.console.Markup("[fuchsia] {0}>[/] ({1})  ", item.Id, item.Status.GetAnsiTextString());
                        this.console.WriteLine(Path.GetFileName(item.OutputPath), new Style(Color.DarkCyan));
                    }
                }
                catch (Exception ex)
                {
                    this.console.WriteException(ex);
                }
            }

            return 0;
        }
    }
}
