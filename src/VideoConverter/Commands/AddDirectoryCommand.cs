using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Cli;
using VideoConverter.Options;
using Humanizer;

namespace VideoConverter.Commands
{
	public class AddDirectoryCommand : AsyncCommand<AddDirectoryOption>
	{
		private readonly AddFileCommand fileCommand;

		public AddDirectoryCommand(AddFileCommand fileCommand)
		{
			this.fileCommand = fileCommand;
		}

		public override Task<int> ExecuteAsync(CommandContext context, AddDirectoryOption settings)
		{
			if (settings is null)
				throw new ArgumentNullException(nameof(settings));

			var files = settings.Directories.SelectMany(d => FindVideoFiles(d, settings.RecursiveSearch)).OrderBy(f => f);

			var fileSettings = new AddFileOption
			{
				AudioCodec = settings.AudioCodec,
				FileExtension = settings.FileExtension,
				Files = files.ToArray(),
				IgnoreDuplicates = settings.IgnoreDuplicates,
				IgnoreStatuses = settings.IgnoreStatuses,
				OutputDir = settings.OutputDirectory,
				Parameters = settings.Parameters,
				ReEncode = settings.ReEncode,
				RemoveDuplicates = settings.RemoveDuplicates,
				Repeat = settings.Repeat,
				StereoMode = settings.StereoMode,
				SubtitleCodec = settings.SubtitleCodec,
				UseEncodingCopy = settings.UseEncodingCopy,
				VideoCodec = settings.VideoCodec,
			};

			return fileCommand.ExecuteAsync(context, fileSettings);
		}

		private static IEnumerable<string> FindVideoFiles(string directory, bool recursive)
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

			EnumerationOptions options = new EnumerationOptions
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
