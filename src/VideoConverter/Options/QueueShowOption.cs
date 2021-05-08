namespace VideoConverter.Options
{
	using System;
	using System.ComponentModel;
	using Spectre.Console.Cli;

	public class QueueShowOption : CommandSettings
	{
		private int[] identifiers = Array.Empty<int>();

		[CommandArgument(0, "<IDENTIFIER>")]
		[Description("The identifier of the queue item to use")]
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
	}
}
