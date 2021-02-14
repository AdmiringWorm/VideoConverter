namespace VideoConverter.Options
{
	using System.ComponentModel;
	using VideoConverter.Storage.Models;
	using Spectre.Console.Cli;

	public class QueueClearOption : CommandSettings
	{
		[CommandArgument(0, "[QUEUE_STATUS]")]
		[Description(
			"Optional argument for which status to use from the queue, or all statuses when no queue status is specified."
		)]
		public QueueStatus? Status { get; set; }
	}
}
