namespace VideoConverter.Commands
{
	using System.Threading.Tasks;

	using Humanizer;

	using Spectre.Console;
	using Spectre.Console.Cli;

	using VideoConverter.Core.Assertions;
	using VideoConverter.Options;
	using VideoConverter.Storage.Repositories;

	public class QueueClearCommand : AsyncCommand<QueueClearOption>
	{
		private readonly IAnsiConsole console;
		private readonly QueueRepository queueRepo;

		public QueueClearCommand(QueueRepository queueRepo, IAnsiConsole console)
		{
			this.queueRepo = queueRepo;
			this.console = console;
		}

		public override async Task<int> ExecuteAsync(CommandContext context, QueueClearOption settings)
		{
			settings.AssertNotNull();

			var statusCount = await queueRepo.RemoveQueueItemsAsync(settings.Status).ConfigureAwait(false);

			await queueRepo.SaveChangesAsync().ConfigureAwait(false);

			console.MarkupLine("[aqua]We have removed {0}[/]", "item".ToQuantity(statusCount));

			return 0;
		}
	}
}
