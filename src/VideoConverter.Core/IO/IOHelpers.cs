namespace VideoConverter.Core.IO
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

#pragma warning disable RS0030 // Do not used banned APIs

	public class IOHelpers : IIOHelpers
	{
		private static readonly string[] Extensions =
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
			"wmv"
		};

		public bool DirectoryExists(string directoryPath)
		{
			ArgumentException.ThrowIfNullOrEmpty(directoryPath);

			return Directory.Exists(directoryPath);
		}

		public void EnsureDirectory(string? directoryPath)
		{
			if (string.IsNullOrWhiteSpace(directoryPath) || DirectoryExists(directoryPath))
			{
				return;
			}

			Directory.CreateDirectory(directoryPath);
		}

		public IEnumerable<string> EnumerateDirectories(string directoryPath, string searchPattern)
		{
			var options = new EnumerationOptions
			{
				MatchCasing = MatchCasing.CaseInsensitive,
				IgnoreInaccessible = true,
				RecurseSubdirectories = false,
				ReturnSpecialDirectories = false
			};

			return EnumerateDirectories(directoryPath, searchPattern, options);
		}

		public IEnumerable<string> EnumerateDirectories(string directoryPath, string searchPattern, EnumerationOptions options)
		{
			ArgumentException.ThrowIfNullOrEmpty(directoryPath);
			ArgumentException.ThrowIfNullOrEmpty(searchPattern);
			ArgumentNullException.ThrowIfNull(options);

			return Directory.EnumerateDirectories(directoryPath, searchPattern, options);
		}

		public IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, EnumerationOptions options)
			=> Directory.EnumerateFiles(directoryPath, searchPattern, options);

		public IEnumerable<string> EnumerateMovieFiles(string directoryPath, bool recursive = false)
		{
			var options = new EnumerationOptions
			{
				MatchCasing = MatchCasing.CaseInsensitive,
				IgnoreInaccessible = true,
				RecurseSubdirectories = recursive,
				ReturnSpecialDirectories = false
			};

			return Extensions.SelectMany(e =>
				EnumerateFiles(directoryPath, "*." + e, options)
			);
		}

		public bool FileExists(string filePath)
		{
			ArgumentException.ThrowIfNullOrEmpty(filePath);

			return File.Exists(filePath);
		}

		public void FileMove(string sourcePath, string destinationPath)
		{
			if (!FileExists(sourcePath))
			{
				return;
			}

			File.Move(sourcePath, destinationPath, overwrite: true);
		}

		public FileStream FileOpenRead(string filePath)
			=> File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

		public FileStream FileOpenWrite(string filePath)
			=> File.Open(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

		public void FileRemove(string? filePath)
		{
			if (string.IsNullOrWhiteSpace(filePath) || !FileExists(filePath))
			{
				return;
			}

			File.Delete(filePath);
		}
	}
}

#pragma warning restore RS0030 // Do not used banned APIs
