namespace VideoConverter.Core.Tests.Models
{
	using System.Collections.Generic;
	using System.Threading.Tasks;

	using NUnit.Framework;

	using VideoConverter.Core.Models;

	using static VerifyNUnit.Verifier;

	public class ConfigurationTests
	{
		[Test]
		public async Task CanResetEntireClass()
		{
			var config = new Configuration
			{
				AudioCodec = "testing",
				ExtraEncodingParameters = "Some Extra",
				Fansubbers = new List<FansubberConfiguration>
				{
					new FansubberConfiguration
					{
						IgnoreOnDuplicates = true,
						Name = "Some Fansubber"
					}
				},
				FileType = "MPEG4",
				IncludeFansubber = false,
				MapperDatabase = "C:\testing",
				Prefixes = new List<PrefixConfiguration>
				{
					new PrefixConfiguration
					{
						Path = "Some-Path",
						Prefix = "prefix"
					}
				},
				SubtitleCodec = "test2",
				VideoCodec = "test3",
				WorkDirectory = "C:\\worlk-dir"
			};

			config.Reset();

			await Verify(config);
		}

		[Test]
		public async Task NewInstanceAndResettingClassShouldBeTheSame()
		{
			var newConfig = new Configuration();
			var resetConfig = new Configuration();
			resetConfig.Reset();

			await Verify(new
			{
				newConfig,
				resetConfig
			});
		}
	}
}
