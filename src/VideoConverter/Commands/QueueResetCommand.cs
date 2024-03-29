namespace VideoConverter.Commands
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;

	using Spectre.Console;
	using Spectre.Console.Cli;

	using VideoConverter.Core.Assertions;
	using VideoConverter.Options;
	using VideoConverter.Storage.Models;
	using VideoConverter.Storage.Repositories;

	public sealed class QueueResetCommand : AsyncCommand<QueueResetOption>
	{
		private readonly IAnsiConsole console;
		private readonly QueueRepository queueRepo;

		public QueueResetCommand(QueueRepository queueRepo, IAnsiConsole console)
		{
			this.queueRepo = queueRepo;
			this.console = console;
		}

		public override async Task<int> ExecuteAsync(CommandContext context, QueueResetOption settings)
		{
			settings.AssertNotNull();

			foreach (var identifier in settings.Identifiers)
			{
				await queueRepo.UpdateQueueStatusAsync(identifier, QueueStatus.Pending).ConfigureAwait(false);
			}

			foreach (var status in settings.QueueStatuses)
			{
				await foreach (var item in queueRepo.GetQueueItemsAsync(status).ConfigureAwait(false))
				{
					if (settings.Identifiers.Contains(item.Id))
					{
						continue;
					}

					item.Status = QueueStatus.Pending;
					item.StatusMessage = string.Empty;
					item.NewHash = string.Empty;
					await queueRepo.UpdateQueueAsync(item).ConfigureAwait(false);
				}
			}

			await queueRepo.SaveChangesAsync().ConfigureAwait(false);

			console.MarkupLine("[aqua]The queue have been reset[/]");

			return 0;
		}
	}
}
