namespace VideoConverter.Commands
{
	using System;
	using System.IO;
	using System.Threading.Tasks;

	using Spectre.Console;
	using Spectre.Console.Cli;

	using VideoConverter.Core.Assertions;
	using VideoConverter.Extensions;
	using VideoConverter.Options;
	using VideoConverter.Storage.Repositories;

	public class QueueShowCommand : AsyncCommand<QueueShowOption>
	{
		private readonly IAnsiConsole console;
		private readonly QueueRepository queueRepo;

		public QueueShowCommand(QueueRepository queueRepo, IAnsiConsole console)
		{
			this.queueRepo = queueRepo;
			this.console = console;
		}

		public override async Task<int> ExecuteAsync(CommandContext context, QueueShowOption settings)
		{
			settings.AssertNotNull();

			for (var i = 0; i < settings.Identifiers.Length; i++)
			{
				var item = await queueRepo.GetQueueItemAsync(settings.Identifiers[i]).ConfigureAwait(false);

				if (item is null)
				{
					throw new Exception($"We could not find any queue item with the id {settings.Identifiers[i]}");
				}

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
				{
					oldDir = Environment.CurrentDirectory;
				}

				var relative = Path.GetRelativePath(Environment.CurrentDirectory, oldDir);
				if (relative.Length < oldDir.Length)
				{
					oldDir = relative;
				}

				table.AddRow("Old Name".GetAnsiText(Color.Silver), oldName.EscapeMarkup().GetAnsiText(Color.DarkCyan));
				table.AddRow("Old Dir".GetAnsiText(Color.Silver), oldDir.EscapeMarkup().GetAnsiText(Color.DarkCyan));
				table.AddRow("Old Hash".GetAnsiText(Color.Silver), item.OldHash.GetAnsiText(Color.DarkKhaki));

				var newName = Path.GetFileName(item.OutputPath);
				var newDir = Path.GetDirectoryName(item.OutputPath);
				if (string.IsNullOrEmpty(newDir))
				{
					newDir = Environment.CurrentDirectory;
				}

				relative = Path.GetRelativePath(Environment.CurrentDirectory, newDir);
				if (relative.Length < newDir.Length)
				{
					newDir = relative;
				}

				table.AddRow("New Name".GetAnsiText(Color.Silver), newName.EscapeMarkup().GetAnsiText(Color.DarkCyan));
				table.AddRow("New Path".GetAnsiText(Color.Silver), newDir.EscapeMarkup().GetAnsiText(Color.DarkCyan));
				table.AddRow("New Hash".GetAnsiText(Color.Silver), item.NewHash.GetAnsiText(Color.DarkKhaki));
				table.AddRow("Status".GetAnsiText(Color.Silver), item.Status.GetAnsiText());

				console.RenderTable(table, string.Empty);

				if (!string.IsNullOrEmpty(item.StatusMessage))
				{
					var panel = new Panel(item.StatusMessage.EscapeMarkup())
						.NoBorder();
					panel.Header = new PanelHeader("[aqua]Status Message[/]", Justify.Center);
					console.Write(panel);
				}
			}

			return 0;
		}
	}
}
