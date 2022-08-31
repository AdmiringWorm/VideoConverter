namespace VideoConverter.Options
{
	using System.ComponentModel;

	using Spectre.Console.Cli;

	public class QueueListOption : QueueClearOption
	{
		[CommandOption("--count")]
		[Description]
		public bool CountOnly { get; set; }
	}
}
