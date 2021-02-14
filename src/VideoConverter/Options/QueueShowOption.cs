namespace VideoConverter.Options
{
	using System;
	using System.ComponentModel;
	using Spectre.Console.Cli;

	public class QueueShowOption : CommandSettings
	{
		[CommandArgument(0, "<IDENTIFIER>")]
		[Description("The identifier of the queue item to use")]
		public int[] Identifiers { get; set; } = Array.Empty<int>();
	}
}
