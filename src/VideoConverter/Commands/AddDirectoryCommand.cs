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
            var files = settings.Directories.SelectMany(d => FindVideoFiles(d, settings.RecursiveSearch)).OrderBy(f => f);

            var fileSettings = new AddFileOption
            {
                Files = files.ToArray(),
                OutputDir = settings.OutputDirectory,
                VideoCodec = settings.VideoCodec,
                AudioCodec = settings.AudioCodec,
                SubtitleCodec = settings.SubtitleCodec,
                ReEncode = settings.ReEncode,
                RemoveDuplicates = settings.RemoveDuplicates,
                IgnoreDuplicates = settings.IgnoreDuplicates,
                IgnoreStatuses = settings.IgnoreStatuses,
                FileExtension = settings.FileExtension,
            };

            return fileCommand.ExecuteAsync(context, fileSettings);
        }

        private IEnumerable<string> FindVideoFiles(string directory, bool recursive)
        {
            var extensions = new[]
            {
                "mkv",
                "mp4",
                "avi",
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
