namespace VideoConverter.Core.Tests.Services
{
	using System;
	using System.IO;

	using FluentAssertions;

	using NUnit.Framework;

	using VideoConverter.Core.Services;
	using VideoConverter.Tests;

	public class ChecksumHashProviderTests : TestBase
	{
		public ChecksumHashProviderTests()
		{
			Register<ChecksumHashProvider>();
		}

		[Test]
		public void Should_Calculate_Expected_Sha1_Checksum()
		{
			const string expected = "0B790A39EFC7200A4C9F49421A489FCC02925F15";

			var input = @"
Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.
Feugiat nisl pretium fusce id velit. Elit ut aliquam purus sit amet luctus venenatis.
Pharetra vel turpis nunc eget lorem dolor sed.
Sed odio morbi quis commodo odio aenean sed. Aenean sed adipiscing diam donec adipiscing. Id diam vel quam elementum pulvinar
etiam non quam. Egestas purus viverra accumsan in nisl nisi.
Leo duis ut diam quam nulla porttitor massa. Consectetur a erat nam at. Urna nunc id cursus metus aliquam eleifend mi in.
In tellus integer feugiat scelerisque. Elit ut aliquam purus sit amet.

Facilisi cras fermentum odio eu. Massa placerat duis ultricies lacus sed turpis tincidunt id aliquet.
Faucibus pulvinar elementum integer
enim neque volutpat ac tincidunt vitae. Turpis cursus in hac habitasse platea dictumst quisque sagittis.
Feugiat in ante metus dictum at
tempor. Cursus sit amet dictum sit amet. At erat pellentesque adipiscing commodo elit at.
Dolor sit amet consectetur adipiscing elit
duis tristique sollicitudin. Nunc aliquet bibendum enim facilisis gravida neque. Faucibus a pellentesque sit amet.
Elementum nisi quis eleifend quam. Urna duis convallis convallis tellus. At tempor commodo ullamcorper a lacus vestibulum.
Eu non diam phasellus vestibulum lorem sed. Libero justo laoreet sit amet cursus.

Quam nulla porttitor massa id neque aliquam vestibulum. Orci ac auctor augue mauris augue neque gravida.
Volutpat blandit aliquam etiam
erat velit scelerisque. Vitae et leo duis ut. Tortor id aliquet lectus proin nibh.
Posuere morbi leo urna molestie at elementum eu.
Eget arcu dictum varius duis at consectetur lorem donec. Ultricies integer quis auctor elit sed vulputate mi.
Senectus et netus et
malesuada. Ante metus dictum at tempor commodo ullamcorper a lacus vestibulum. Cursus euismod quis viverra nibh.
Id faucibus nisl tincidunt eget nullam non nisi est. Dignissim convallis aenean et tortor at risus viverra.
Semper risus in hendrerit
gravida rutrum quisque non tellus orci. At augue eget arcu dictum varius duis. Rutrum quisque non tellus orci ac auctor.
Egestas purus viverra accumsan in nisl nisi scelerisque eu. Lacus sed viverra tellus in hac habitasse platea dictumst
vestibulum.
Pharetra pharetra massa massa ultricies mi quis hendrerit dolor.

Eget lorem dolor sed viverra ipsum nunc aliquet. Vel pharetra vel turpis nunc eget lorem. Mauris cursus mattis molestie a
iaculis at erat pellentesque. Accumsan lacus vel facilisis volutpat est. Ullamcorper velit sed ullamcorper morbi tincidunt
ornare massa eget egestas. Quam adipiscing vitae proin sagittis nisl rhoncus mattis rhoncus urna. Gravida in fermentum et
sollicitudin ac orci phasellus egestas. Tellus molestie nunc non blandit massa enim nec dui. Sit amet venenatis urna cursus eget nunc
scelerisque. Iaculis urna id volutpat lacus laoreet. Eros donec ac odio tempor orci dapibus ultrices in. Tellus rutrum tellus
pellentesque eu tincidunt tortor aliquam nulla facilisi. Nec sagittis aliquam malesuada bibendum arcu vitae.
Venenatis urna cursus eget nunc scelerisque viverra mauris in aliquam. Urna nunc id cursus metus aliquam eleifend.
Aliquam purus sit amet luctus venenatis lectus magna fringilla. Elementum facilisis leo vel fringilla est ullamcorper eget
nulla.

In nulla posuere sollicitudin aliquam ultrices sagittis. Pulvinar elementum integer enim neque volutpat ac tincidunt vitae.
Suspendisse in est ante in nibh mauris cursus mattis. Maecenas sed enim ut sem viverra aliquet eget sit amet.
Ipsum faucibus vitae aliquet nec. Quisque non tellus orci ac. Sit amet porttitor eget dolor morbi non arcu risus quis.
Ultrices vitae auctor eu augue ut lectus arcu. Massa enim nec dui nunc. Donec enim diam vulputate ut.
Morbi tristique senectus et netus et malesuada.
".ReplaceLineEndings("\n");
			using var stream = input.ToStream();

			var provider = Resolve<ChecksumHashProvider>();

			var actual = provider.ComputeHash(stream);

			actual.Should().NotBeNullOrEmpty().And.Be(expected);
		}

		[Test]
		public void Should_Calculate_Expected_Sha1_Checksum_On_Empty_String()
		{
			const string expected = "DA39A3EE5E6B4B0D3255BFEF95601890AFD80709";
			using var stream = string.Empty.ToStream();

			var provider = Resolve<ChecksumHashProvider>();

			var actual = provider.ComputeHash(stream);

			actual.Should().Be(expected);
		}

		[Test]
		public void Throws_Argument_Null_Exception_On_Null_Path()
		{
			var provider = Resolve<ChecksumHashProvider>();
			var act = () => provider.ComputeHash((string)null);

			act.Should().Throw<ArgumentNullException>()
				.WithParameterName("filePath")
				.WithMessage("Value cannot be null. (Parameter 'filePath')");
		}

		[Test]
		public void Throws_Argument_Null_Exception_On_Null_Stream()
		{
			var provider = Resolve<ChecksumHashProvider>();
			var act = () => provider.ComputeHash((Stream)null);

			act.Should().Throw<ArgumentNullException>()
				.WithParameterName("fileStream")
				.WithMessage("Value cannot be null. (Parameter 'fileStream')");
		}
	}
}
