namespace VideoConverter.Options
{
	using System.ComponentModel;

	using Spectre.Console.Cli;

	using VideoConverter.Storage.Models;

	public class QueueClearOption : CommandSettings
	{
		[CommandArgument(0, "[QUEUE_STATUS]")]
		[Description(
			"Optional argument for which status to use from the queue, or all statuses when no queue status is specified."
		)]
		public QueueStatus? Status { get; set; }
	}
}
