namespace VideoConverter.IntegrationTests.Services
{
	using System;
	using System.IO;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	using FluentAssertions;

	using NUnit.Framework;

	using VerifyNUnit;

	using VideoConverter.Core.Models;
	using VideoConverter.Core.Services;
	using VideoConverter.Tests;

	[Parallelizable(ParallelScope.Self)]
	public class XmlConfigurationServiceTests : TestBase
	{
		public XmlConfigurationServiceTests()
			: base(Program.CreateContainer())
		{
		}

		[Test]
		public void Should_Read_Configuration_From_File_Path()
		{
			var path = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid() + ".xml");
			var temp = Path.GetTempPath();
			var storagePath = Path.Combine(temp, "storage.db");
			var expected = new ConverterConfiguration
			{
				MapperDatabase = storagePath,
				AudioCodec = "opus"
			};
			// editorconfig-checker-disable
			File.WriteAllText(path, $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Configuration xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <IncludeFansubber>true</IncludeFansubber>
  <VideoCodec>hevc</VideoCodec>
  <AudioCodec>opus</AudioCodec>
  <SubtitleCodec>copy</SubtitleCodec>
  <WorkDirectory>{temp}</WorkDirectory>
  <FileType>Matroska</FileType>
  <MapperDatabase>{storagePath}</MapperDatabase>
  <ExtraEncodingParameters />
  <Prefixes />
</Configuration>");
			// editorconfig-checker-enable
			using var service = Resolve<XmlConfigurationService>(path);

			var actual = service.GetConfiguration();

			try
			{
				actual.Should().BeEquivalentTo(expected);
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Test]
		public async Task Should_Save_Configuration_To_File()
		{
			var path = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid() + ".xml");

			using var service = Resolve<XmlConfigurationService>(path);
			var config = new ConverterConfiguration();

			service.SetConfiguration(config);

			try
			{
				FileAssert.Exists(path);
				await Verifier.VerifyFile(path);
			}
			finally
			{
				try
				{
					File.Delete(path);
				}
				catch { }
			}
		}

		[Test]
		public void Should_Save_Configuration_With_Expected_Content()
		{
			var path = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid() + ".xml");
			// editorconfig-checker-disable
			var tmp = Path.GetTempPath();
			var expected = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Configuration xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <AudioCodec>libopus</AudioCodec>
  <ExtraEncodingParameters />
  <Fansubbers />
  <FileType>Matroska</FileType>
  <IncludeFansubber>true</IncludeFansubber>
  <Prefixes />
  <SubtitleCodec>copy</SubtitleCodec>
  <VideoCodec>hevc</VideoCodec>
  <WorkDirectory>{tmp}</WorkDirectory>
</Configuration>";

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				expected = expected.Replace("\n", "\r\n", StringComparison.Ordinal);
			}

			// editorconfig-checker-enable
			using var service = Resolve<XmlConfigurationService>(path);
			var config = new ConverterConfiguration();

			service.SetConfiguration(config);
			var actual = File.ReadAllText(path, Encoding.UTF8);

			try
			{
				actual.Should().Be(expected);
			}
			finally
			{
				try
				{
					File.Delete(path);
				}
				catch { }
			}
		}

		[Test]
		public void Should_Update_Configuration_When_File_Is_Changed()
		{
			var path = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid() + ".xml");
			var expected = new ConverterConfiguration
			{
				VideoCodec = "libx264",
				MapperDatabase = Path.Combine(
					Environment.GetFolderPath(
						Environment.SpecialFolder.LocalApplicationData),
					"VideoConverter",
					"storage.db"),
			};
			var temp = Path.GetTempPath();
			var config = new ConverterConfiguration();
			using var service = Resolve<XmlConfigurationService>(path);
			service.SetConfiguration(config);

			config.Should().NotBeEquivalentTo(expected);

			// editorconfig-checker-disable
			File.WriteAllText(path, $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Configuration xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <IncludeFansubber>true</IncludeFansubber>
  <VideoCodec>libx264</VideoCodec>
  <AudioCodec>libopus</AudioCodec>
  <SubtitleCodec>copy</SubtitleCodec>
  <WorkDirectory>{temp}</WorkDirectory>
  <FileType>Matroska</FileType>
  <ExtraEncodingParameters />
  <Prefixes />
</Configuration>");
			// editorconfig-checker-enable

			Thread.Sleep(TimeSpan.FromSeconds(2));

			try
			{
				config.Should().BeEquivalentTo(expected);
			}
			finally
			{
				File.Delete(path);
			}
		}
	}
}
