namespace VideoConverter.IntegrationTests.Services
{
	using System;
	using System.IO;

	using FluentAssertions;

	using NUnit.Framework;

	using VideoConverter.Core.Services;
	using VideoConverter.Tests;

	public class ChecksumHashProviderTests : TestBase<IHashProvider>
	{
		public ChecksumHashProviderTests()
			: base(Program.CreateContainer())
		{
		}

		[Test]
		public void Should_Calculate_Expected_Checksum_From_Blank_File_Path()
		{
			const string expected = "DA39A3EE5E6B4B0D3255BFEF95601890AFD80709";
			var testFile = Path.GetTempFileName();

			try
			{
				var actual = Service.ComputeHash(testFile);
				actual.Should().Be(expected);
			}
			finally
			{
				File.Delete(testFile);
			}
		}

		[Test]
		public void Should_Calculate_Expected_Checksum_From_File_Path()
		{
			const string expected = "FB5A1DA0421EDB02BA9D73C1A711445691046FDD";
			var testFile = CreateTestFile(52428800); // ~50 MB

			try
			{
				var actual = Service.ComputeHash(testFile);

				actual.Should().Be(expected);
			}
			finally
			{
				File.Delete(testFile);
			}
		}

		[Test]
		public void Should_Calculate_Expected_Checksum_From_Large_File_Path()
		{
			const string expected = "7FABFF96C4AE5491927AB5CE7EB9C6BAFD6E846E";
			var testFile = CreateTestFile(int.MaxValue); // ~2 GB

			try
			{
				var actual = Service.ComputeHash(testFile);

				actual.Should().Be(expected);
			}
			finally
			{
				File.Delete(testFile);
			}
		}

		[Test]
		public void Should_Calculate_Expected_Checksum_From_Semi_Large_File_Path()
		{
			const string expected = "2279EF95D5A34B9A1DC5EBB9CA72E766FE09FDD8";
			var testFile = CreateTestFile(int.MaxValue / 2); // ~1 GB

			try
			{
				var actual = Service.ComputeHash(testFile);

				actual.Should().Be(expected);
			}
			finally
			{
				File.Delete(testFile);
			}
		}

		private static string CreateTestFile(int fileSize)
		{
			const int SEED = 50_000;

			var filePath = Path.GetTempFileName();

			Span<byte> testBytes = stackalloc byte[4096];
			using var stream = new FileStream(filePath, FileMode.OpenOrCreate);
			var i = 0;
			var writtenBytes = 0L;

			while (writtenBytes < fileSize)
			{
				var rnd = new Random(SEED + i);
				rnd.NextBytes(testBytes);
				stream.Write(testBytes);
				i++;
				writtenBytes += testBytes.Length;
			}

			stream.Flush();

			return filePath;
		}
	}
}
