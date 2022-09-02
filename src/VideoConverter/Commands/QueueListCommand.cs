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

	public class QueueListCommand : AsyncCommand<QueueListOption>
	{
		private readonly IAnsiConsole console;
		private readonly QueueRepository queueRepo;

		public QueueListCommand(QueueRepository queueRepo, IAnsiConsole console)
		{
			this.queueRepo = queueRepo;
			this.console = console;
		}

		public override async Task<int> ExecuteAsync(CommandContext context, QueueListOption settings)
		{
			settings.AssertNotNull();

			if (settings.CountOnly)
			{
				var itemCount = await queueRepo.GetQueueItemCountAsync(settings.Status).ConfigureAwait(false);

				if (settings.Status is null)
				{
					console.MarkupLine("[aqua]There are {0} items in total in the queue![/]", itemCount);
				}
				else
				{
					console.MarkupLine("[aqua]There are {0} items in the {1} queue![/]", itemCount, settings.Status);
				}
			}
			else
			{
				try
				{
					var items = queueRepo.GetQueueItemsAsync(settings.Status);

					await foreach (var item in items)
					{
						console.Markup("[fuchsia] {0}>[/] ({1})  ", item.Id, item.Status.GetAnsiTextString());
						console.WriteLine(Path.GetFileName(item.OutputPath), new Style(Color.DarkCyan));
					}
				}
				catch (Exception ex)
				{
					console.WriteException(ex);
					throw;
				}
			}

			return 0;
		}
	}
}
