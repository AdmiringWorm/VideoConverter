namespace VideoConverter.Core.Services
{
	using System.IO;

	public interface IHashProvider
	{
		string ComputeHash(string filePath);

		string ComputeHash(Stream fileStream);
	}
}
