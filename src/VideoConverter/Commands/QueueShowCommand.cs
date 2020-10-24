using System.Drawing;
using System;
using System.IO;
namespace VideoConverter.Commands
{
    using Humanizer;
    using Spectre.Cli;
    using Spectre.Console;
    using VideoConverter.Extensions;
    using VideoConverter.Options;
    using VideoConverter.Storage.Repositories;

    public class QueueShowCommand : Command<QueueShowOption>
    {
        private readonly QueueRepository queueRepo;
        private readonly IAnsiConsole console;

        public QueueShowCommand(QueueRepository queueRepo, IAnsiConsole console)
        {
            this.queueRepo = queueRepo;
            this.console = console;
        }

        public override int Execute(CommandContext context, QueueShowOption settings)
        {
            var item = this.queueRepo.GetQueueItem(settings.Identifier);

            if (item is null)
                throw new Exception($"We could not find any queue item with the id {settings.Identifier}");

            var table = new Table()
                .NoBorder()
                .AddColumns(
                    new TableColumn("Key").RightAligned(),
                    new TableColumn("Value").LeftAligned()
                )
                .HideHeaders();

            table.AddRow("Identifier".GetAnsiText(Color.Silver), item.Id.GetAnsiText());
            table.AddRow("Audio Codec".GetAnsiText(Color.Silver), item.AudioCodec.GetAnsiText());
            table.AddRow("Video Codec".GetAnsiText(Color.Silver), item.VideoCodec.GetAnsiText());
            table.AddRow("Subtitle Codec".GetAnsiText(Color.Silver), item.SubtitleCodec.GetAnsiText());

            var oldName = Path.GetFileName(item.Path);
            var oldDir = Path.GetDirectoryName(item.Path);
            if (string.IsNullOrEmpty(oldDir))
                oldDir = Environment.CurrentDirectory;
            var relative = Path.GetRelativePath(Environment.CurrentDirectory, oldDir);
            if (relative.Length < oldDir.Length)
                oldDir = relative;

            table.AddRow("Old Name".GetAnsiText(Color.Silver), oldName.SafeMarkup().GetAnsiText(Color.DarkCyan));
            table.AddRow("Old Dir".GetAnsiText(Color.Silver), oldDir.SafeMarkup().GetAnsiText(Color.DarkCyan));

            var newName = Path.GetFileName(item.OutputPath);
            var newDir = Path.GetDirectoryName(item.OutputPath);
            if (string.IsNullOrEmpty(newDir))
                newDir = Environment.CurrentDirectory;
            relative = Path.GetRelativePath(Environment.CurrentDirectory, newDir);
            if (relative.Length < newDir.Length)
                newDir = relative;

            table.AddRow("New Name".GetAnsiText(Color.Silver), newName.SafeMarkup().GetAnsiText(Color.DarkCyan));
            table.AddRow("New Path".GetAnsiText(Color.Silver), newDir.SafeMarkup().GetAnsiText(Color.DarkCyan));
            table.AddRow("Status".GetAnsiText(Color.Silver), item.Status.GetAnsiText());

            this.console.RenderTable(table, string.Empty);

            if (!string.IsNullOrEmpty(item.StatusMessage))
            {
                var panel = new Panel(item.StatusMessage)
                    .NoBorder();
                panel.SetHeader("Status Message", Style.WithForeground(Color.Aqua), Justify.Center);
                this.console.Render(panel);
            }

            return 0;
        }
    }
}
