namespace VideoConverter.Commands
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	using Spectre.Console;
	using Spectre.Console.Cli;

	using VideoConverter.Core.Assertions;
	using VideoConverter.Options;
	using VideoConverter.Storage.Models;
	using VideoConverter.Storage.Repositories;

	public class QueueRemoveCommand : AsyncCommand<QueueRemoveOption>
	{
		private readonly IAnsiConsole console;
		private readonly QueueRepository queueRepo;

		public QueueRemoveCommand(QueueRepository queueRepo, IAnsiConsole console)
		{
			this.queueRepo = queueRepo;
			this.console = console;
		}

		public override async Task<int> ExecuteAsync(CommandContext context, QueueRemoveOption settings)
		{
			settings.AssertNotNull();

			var removed = new List<int>();

			for (var i = 0; i < settings.Identifiers.Length; i++)
			{
				var item = await queueRepo.GetQueueItemAsync(settings.Identifiers[i]).ConfigureAwait(false);

				if (item is null)
				{
					await queueRepo.AbortChangesAsync().ConfigureAwait(false);
					throw new Exception($"We could not find any item with the id {settings.Identifiers[i]}!");
				}

				if (item.Status == QueueStatus.Encoding)
				{
					throw new Exception(
						$"We were unable to remove the item with the id {settings.Identifiers[i]}! It is already being encoded!"
					);
				}

				await queueRepo.RemoveQueueItemAsync(item.Id).ConfigureAwait(false);

				removed.Add(settings.Identifiers[i]);
			}

			await queueRepo.SaveChangesAsync().ConfigureAwait(false);

			foreach (var index in removed)
			{
				console.MarkupLine(
					"[darkcyan]We successfully removed the queue item with the identifier {0} from the queue![/]",
					index
				);
			}

			return 0;
		}
	}
}
