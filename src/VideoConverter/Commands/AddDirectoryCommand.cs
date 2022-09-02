namespace VideoConverter.Commands
{
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;

	using Spectre.Console.Cli;

	using VideoConverter.Core.Assertions;
	using VideoConverter.Options;

	public class AddDirectoryCommand : AsyncCommand<AddDirectoryOption>
	{
		private readonly AddFileCommand fileCommand;

		public AddDirectoryCommand(AddFileCommand fileCommand)
		{
			this.fileCommand = fileCommand;
		}

		public override Task<int> ExecuteAsync(CommandContext context, AddDirectoryOption settings)
		{
			settings.AssertNotNull();

			var files = settings.Directories.SelectMany(d => FindVideoFiles(d, settings.RecursiveSearch)).OrderBy(f => f);

			var fileSettings = AddFileOption.FromDirectoryOptions(settings, files.ToArray());

			return fileCommand.ExecuteAsync(context, fileSettings);
		}

		internal static IEnumerable<string> FindVideoFiles(string directory, bool recursive)
		{
			var extensions = new[]
			{
				"asf",
				"avi",
				"dv",
				"gif",
				"m4v",
				"mk3d",
				"mkv",
				"mkv3d",
				"mp4",
				"mpeg",
				"mpg",
				"vob",
				"webm",
				"wmv",
			};

			EnumerationOptions options = new()
			{
				MatchCasing = MatchCasing.CaseInsensitive,
				IgnoreInaccessible = true,
				RecurseSubdirectories = recursive,
				ReturnSpecialDirectories = false
			};

			return extensions.SelectMany(e =>
				Directory.EnumerateFiles(directory, "*." + e, options)
			);
		}
	}
}
