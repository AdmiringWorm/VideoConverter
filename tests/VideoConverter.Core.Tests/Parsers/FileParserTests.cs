namespace VideoConverter.Core.Tests.Parsers
{
	using FluentAssertions;
	using NUnit.Framework;
	using VideoConverter.Core.Models;
	using VideoConverter.Core.Parsers;

	public class FileParserTests
	{
		// editorconfig-checker-disable
		[TestCase("[HorribleSubs] Darwin's Game - 5 [1080p].mkv", "Darwin's Game", null, 5, "Matroska")]
		[TestCase("[HorribleSubs] Haikyuu!! S1 - 3 [1080p].mkv", "Haikyuu!!", 1, 3, "Matroska")]
		[TestCase("[Judas] Re Zero kara Hajimeru Isekai Seikatsu - S02E2.mkv", "Re Zero kara Hajimeru Isekai Seikatsu", 2, 2, "Matroska")]
		[TestCase("[HorribleSubs] Koi to Producer - Evol x Love - 10 [1080p].mkv", "Koi to Producer - Evol x Love", null, 10, "Matroska")]
		[TestCase("Arte - S01E12 - Apprentice.mkv", "Arte", 1, 12, "Matroska")]
		[TestCase("[Erai-raws] Kami-tachi ni Hirowareta Otoko - 02 [1080p].mkv", "Kami-tachi ni Hirowareta Otoko", null, 2, "Matroska")]
		[TestCase("[Erai-raws] Keishichou Tokumubu Tokushu Kyouakuhan Taisakushitsu Dainanaka - Tokunana - OVA [1080p].mkv", "Keishichou Tokumubu Tokushu Kyouakuhan Taisakushitsu Dainanaka - Tokunana", 0, 0, "Matroska")]
		[TestCase("Food Wars! S2 - 08.mkv", "Food Wars!", 2, 8, "Matroska")]
		[TestCase("[Cleo]One_Punch_Man_2nd_Season_-_12_(Dual Audio_10bit_1080p_x265).mkv", "One Punch Man 2nd Season", null, 12, "Matroska")]
		[TestCase("Tsukiuta. The Animation - 06 (1280x720 HEVC2 AAC).mp4", "Tsukiuta. The Animation", null, 6, "MPEG-4")]
		[TestCase("[Judas] Granblue Fantasy - OAV 02.mkv", "Granblue Fantasy", 0, 2, "Matroska")]
		[TestCase("[GSK_kun] Kaguya-sama Love Is War 08 [BDRip 1920x1080 HEVC FLAC] [84C8F965].mkv", "Kaguya-sama Love Is War", null, 8, "Matroska")]
		[TestCase("Clannad 1x05.mkv", "Clannad", "1", "5", "Matroska")]
		[TestCase("K-ON! S01E12 Light Music!.mkv", "K-ON!", 1, 12, "Matroska")]
		[TestCase("Death Note - 01x29 - Father.mkv", "Death Note", 1, 29, "Matroska")]
		[TestCase("[YuiSubs] Isekai Shokudou S2 - 01  (NVENC H.265 1080p).mkv", "Isekai Shokudou", 2, 1, "Matroska")]
		[TestCase("[YuiSubs] Sekai Saikou no Ansatsusha, Isekai Kizoku ni Tensei suru - 03  (NVENC H.265 1080p).mkv", "Sekai Saikou no Ansatsusha, Isekai Kizoku ni Tensei suru", null, 3, "Matroska")]
		// editorconfig-checker-enable
		public void ShouldParseCorrectEpisodeData(
			string fileName,
			string expectedSeries,
			int? expectedSeason,
			int expectedEpisode,
			string container
		)
		{
			var result = FileParser.ParseEpisode(fileName);

			result.Should().NotBeNull();
			result.FileName.Should().Be(fileName);
			result.Series.Should().Be(expectedSeries);
			result.SeasonNumber.Should().Be(expectedSeason);
			result.EpisodeNumber.Should().Be(expectedEpisode);
			result.Container.Should().Be(container);
		}

		[Test]
		public void ShouldSetExpectedValues()
		{
			const string testName = "[Anime Time] Utawarerumono - S01e02 - Ruler Of The Wild Forest.mkv";
			var expected = new EpisodeData(testName, "Utawarerumono", 2, "Matroska")
			{
				SeasonNumber = 1,
				EpisodeName = "Ruler Of The Wild Forest",
				Fansubber = "Anime Time",
			};

			var actual = FileParser.ParseEpisode(testName);

			actual.Should().NotBeNull()
				.And.Be(expected);
		}
	}
}
