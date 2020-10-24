namespace VideoConverter.Options
{
    using System.ComponentModel;
    using Spectre.Cli;

    public class QueueShowOption : CommandSettings
    {
        [CommandArgument(0, "<IDENTIFIER>")]
        [Description("The identifier of the queue item to use")]
        public int Identifier { get; set; }
    }
}
