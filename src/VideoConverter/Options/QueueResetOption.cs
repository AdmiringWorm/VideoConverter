namespace VideoConverter.Options
{
	using System;
	using System.ComponentModel;
	using Spectre.Cli;
	using VideoConverter.Storage.Models;

	public sealed class QueueResetOption : CommandSettings
	{
		[CommandArgument(0, "[QUEUE_STATUS]")]
		[Description("The queue statuses to reset the progress of (can be specificed multiple times)")]
		public QueueStatus[] QueueStatuses { get; set; } = Array.Empty<QueueStatus>();

		[CommandOption("--id <IDENTIFIER>")]
		[Description("The identefier of the item to reset the progress of (can be specified multiple times)")]
		public int[] Identifieres { get; set; } = Array.Empty<int>();

		public override ValidationResult Validate()
		{
			if (QueueStatuses.Length == 0 && Identifieres.Length == 0)
				return ValidationResult.Error("A status or an identifier must be specified!");

			return base.Validate();
		}
	}
}
