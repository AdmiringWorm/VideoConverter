namespace VideoConverter.Commands
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;

	using Spectre.Console.Cli;

	using VideoConverter.Core.Assertions;
	using VideoConverter.Core.IO;
	using VideoConverter.Options;

	public class AddDirectoryCommand : AsyncCommand<AddDirectoryOption>
	{
		private readonly AddFileCommand fileCommand;
		private readonly IIOHelpers ioHelpers;

		public AddDirectoryCommand(AddFileCommand fileCommand, IIOHelpers ioHelpers)
		{
			this.fileCommand = fileCommand;
			this.ioHelpers = ioHelpers;
		}

		public override Task<int> ExecuteAsync(CommandContext context, AddDirectoryOption settings)
		{
			settings.AssertNotNull();

			var files = settings.Directories.SelectMany(d => ioHelpers.EnumerateMovieFiles(d, settings.RecursiveSearch)).Order(StringComparer.OrdinalIgnoreCase);

			var fileSettings = AddFileOption.FromDirectoryOptions(settings, files.ToArray());

			return fileCommand.ExecuteAsync(context, fileSettings);
		}
	}
}
