namespace VideoConverter.Core.Services
{
	using System;
	using System.IO;
	using System.Security.Cryptography;

	using VideoConverter.Core.IO;

	public sealed class ChecksumHashProvider : IHashProvider
	{
		private readonly IIOHelpers ioHelpers;

		public ChecksumHashProvider(IIOHelpers ioHelpers)
		{
			this.ioHelpers = ioHelpers;
		}

		public string ComputeHash(string filePath)
		{
			ArgumentNullException.ThrowIfNull(filePath);

			using var fileStream = ioHelpers.FileOpenRead(filePath);

			return ComputeHash(fileStream);
		}

		public string ComputeHash(Stream fileStream)
		{
			ArgumentNullException.ThrowIfNull(fileStream);

#pragma warning disable CA5350
			using var algorithm = SHA1.Create();
#pragma warning restore CA5350

			var hashBytes = algorithm.ComputeHash(fileStream);

			return Convert.ToHexString(hashBytes);
		}
	}
}
