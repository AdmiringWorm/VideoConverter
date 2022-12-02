namespace VideoConverter.Core.Tests.Services
{
	using System;
	using System.IO;

	using DryIoc;

	using FluentAssertions;

	using NUnit.Framework;

	using VideoConverter.Core.IO;
	using VideoConverter.Core.Models;
	using VideoConverter.Core.Services;
	using VideoConverter.Tests;

	public class XmlConfigurationServiceTests : TestBase
	{
		[Test]
		public void Should_Get_Default_Configuration_When_File_Do_Not_Exist()
		{
			var path = Path.Combine(Environment.CurrentDirectory, Guid.NewGuid() + ".xml");

			Mock<IIOHelpers>()
				.Setup(h => h.FileExists(path))
				.Returns(false);

			var expected = new ConverterConfiguration
			{
				MapperDatabase = Path.Combine(
					Environment.GetFolderPath(
						Environment.SpecialFolder.LocalApplicationData),
					"VideoConverter",
					"storage.db"),
			};
			using var service = Resolve<XmlConfigurationService>(path);

			var actual = service.GetConfiguration();

			actual.Should().BeEquivalentTo(expected);
		}

		protected override void SetupContainer(IContainer container)
		{
			container.Register<XmlConfigurationService>(Reuse.ScopedOrSingleton);
		}
	}
}
