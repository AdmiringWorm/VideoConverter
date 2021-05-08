namespace VideoConverter.Options
{
	using System;
	using System.ComponentModel;
	using Spectre.Console;
	using Spectre.Console.Cli;
	using VideoConverter.Storage.Models;

	public sealed class QueueResetOption : CommandSettings
	{
		private QueueStatus[] queueStatuses = Array.Empty<QueueStatus>();
		private int[] identifiers = Array.Empty<int>();

		[CommandArgument(0, "[QUEUE_STATUS]")]
		[Description("The queue statuses to reset the progress of (can be specificed multiple times)")]
		public QueueStatus[] QueueStatuses
		{
			get => queueStatuses;
			set
			{
				if (value is null)
				{
					queueStatuses = Array.Empty<QueueStatus>();
				}
				else
				{
					queueStatuses = value;
				}
			}
		}

		[CommandOption("--id <IDENTIFIER>")]
		[Description("The identefier of the item to reset the progress of (can be specified multiple times)")]
		public int[] Identifiers
		{
			get => identifiers;
			set
			{
				if (value is null)
				{
					identifiers = Array.Empty<int>();
				}
				else
				{
					identifiers = value;
				}
			}
		}

		public override ValidationResult Validate()
		{
			if (QueueStatuses.Length == 0 && Identifiers.Length == 0)
				return ValidationResult.Error("A status or an identifier must be specified!");

			return base.Validate();
		}
	}
}
