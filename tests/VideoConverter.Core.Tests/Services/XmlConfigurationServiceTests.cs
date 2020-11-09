using System.Threading;
using System.Text;
using System;
using System.IO;
namespace VideoConverter.Core.Tests.Services
{
	using FluentAssertions;
	using NUnit.Framework;
	using VideoConverter.Core.Models;
	using VideoConverter.Core.Services;

	public class XmlConfigurationServiceTests
	{
		[Test]
		public void Should_Save_Configuration_To_File()
		{
			var path = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid() + ".xml");

			using var service = new XmlConfigurationService(path);
			var config = new Configuration();

			service.SetConfiguration(config);

			try
			{
				FileAssert.Exists(path);
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Test]
		public void Should_Save_Configuration_With_Expected_Content()
		{
			var path = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid() + ".xml");
			const string expected = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Configuration xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <IncludeFansubber>true</IncludeFansubber>
  <VideoCodec>hevc</VideoCodec>
  <AudioCodec>libopus</AudioCodec>
  <SubtitleCodec>copy</SubtitleCodec>
  <WorkDirectory>/tmp/</WorkDirectory>
  <FileType>Matroska</FileType>
  <ExtraEncodingParameters />
  <Prefixes />
</Configuration>";
			using var service = new XmlConfigurationService(path);
			var config = new Configuration();

			service.SetConfiguration(config);
			var actual = File.ReadAllText(path, Encoding.UTF8);

			try
			{
				actual.Should().Be(expected);
			}
			finally
			{
				File.Delete(path);
			}
		}

		[Test]
		public void Should_Get_Default_Configuration_When_File_Do_Not_Exist()
		{
			var path = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid() + ".xml");
			var expected = new Configuration
			{
				MapperDatabase = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoConverter", "storage.db"),
			};
			using var service = new XmlConfigurationService(path);

			var actual = service.GetConfiguration();

			actual.Should().BeEquivalentTo(expected);
		}

		[Test]
		public void Should_Read_Configuration_From_File_Path()
		{
			var path = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid() + ".xml");
			var expected = new Configuration
			{
				MapperDatabase = "/tmp/storage.db",
				AudioCodec = "opus"
			};
			File.WriteAllText(path, @"<?xml version=""1.0"" encoding=""utf-8""?>
<Configuration xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <IncludeFansubber>true</IncludeFansubber>
  <VideoCodec>hevc</VideoCodec>
  <AudioCodec>opus</AudioCodec>
  <SubtitleCodec>copy</SubtitleCodec>
  <WorkDirectory>/tmp/</WorkDirectory>
  <FileType>Matroska</FileType>
  <MapperDatabase>/tmp/storage.db</MapperDatabase>
  <ExtraEncodingParameters />
  <Prefixes />
</Configuration>");
			using var service = new XmlConfigurationService(path);

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
		public void Should_Update_Configuration_When_File_Is_Changed()
		{
			var path = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid() + ".xml");
			var expected = new Configuration
			{
				VideoCodec = "libx264",
				MapperDatabase = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoConverter", "storage.db"),
			};
			var config = new Configuration();
			using var service = new XmlConfigurationService(path);
			service.SetConfiguration(config);

			config.Should().NotBeEquivalentTo(expected);

			File.WriteAllText(path, @"<?xml version=""1.0"" encoding=""utf-8""?>
<Configuration xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
  <IncludeFansubber>true</IncludeFansubber>
  <VideoCodec>libx264</VideoCodec>
  <AudioCodec>libopus</AudioCodec>
  <SubtitleCodec>copy</SubtitleCodec>
  <WorkDirectory>/tmp/</WorkDirectory>
  <FileType>Matroska</FileType>
  <ExtraEncodingParameters />
  <Prefixes />
</Configuration>");

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
