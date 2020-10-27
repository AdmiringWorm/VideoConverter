namespace VideoConverter.Options
{
    using System.ComponentModel;
    using Spectre.Cli;

    public class QueueListOption : QueueClearOption
    {
        [CommandOption("--count")]
        [Description]
        public bool CountOnly { get; set; }
    }
}
