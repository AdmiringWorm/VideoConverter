namespace VideoConverter.Options
{
	using System;
	using System.ComponentModel;

	using Spectre.Console;
	using Spectre.Console.Cli;

	using VideoConverter.Storage.Models;

	public sealed class QueueResetOption : CommandSettings
	{
		private int[] identifiers = Array.Empty<int>();
		private QueueStatus[] queueStatuses = Array.Empty<QueueStatus>();

		[CommandOption("--id <IDENTIFIER>")]
		[Description("The identefier of the item to reset the progress of (can be specified multiple times)")]
		public int[] Identifiers
		{
			get => identifiers;
			set
			{
				identifiers = value is null ? Array.Empty<int>() : value;
			}
		}

		[CommandArgument(0, "[QUEUE_STATUS]")]
		[Description("The queue statuses to reset the progress of (can be specificed multiple times)")]
		public QueueStatus[] QueueStatuses
		{
			get => queueStatuses;
			set
			{
				queueStatuses = value is null ? Array.Empty<QueueStatus>() : value;
			}
		}

		public override ValidationResult Validate()
		{
			if (QueueStatuses.Length == 0 && Identifiers.Length == 0)
			{
				return ValidationResult.Error("A status or an identifier must be specified!");
			}

			return base.Validate();
		}
	}
}
