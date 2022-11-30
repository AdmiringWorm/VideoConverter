namespace VideoConverter.Core.IO
{
	using System.Collections.Generic;
	using System.IO;

	public interface IIOHelpers
	{
		bool DirectoryExists(string directoryPath);

		void EnsureDirectory(string? directoryPath);

		IEnumerable<string> EnumerateDirectories(string directoryPath, string searchPattern);

		IEnumerable<string> EnumerateDirectories(string directoryPath, string searchPattern, EnumerationOptions options);

		IEnumerable<string> EnumerateFiles(string directoryPath, string searchPattern, EnumerationOptions options);

		IEnumerable<string> EnumerateMovieFiles(string directoryPath, bool recursive = false);

		bool FileExists(string filePath);

		void FileMove(string sourcePath, string destinationPath);

		void FileRemove(string? filePath);
	}
}
