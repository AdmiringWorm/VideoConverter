namespace VideoConverter.Storage.IntegrationTests.Repositories
{
	using System.Collections.Generic;
	using System.Threading.Tasks;

	using FluentAssertions;

	using NUnit.Framework;

	using VerifyNUnit;

	using VideoConverter.Storage.Database;
	using VideoConverter.Storage.Models;
	using VideoConverter.Storage.Repositories;

	public class EpisodeCriteriaRepositoryTests : BaseRepositoryTests<EpisodeCriteriaRepository>
	{
		[Test]
		public async Task CanAbortChanges()
		{
			await repository.AddOrUpdateCriteriaAsync(new Core.Models.EpisodeCriteria
			{
				SeriesName = "Test Series",
				NewSeries = "New Test Series"
			});

			await repository.AbortChangesAsync();
			await factory.CommitTransactionAsync();

			{
				var col = factory.GetCollection<EpisodeCriteria>();
				var existingCriteria = await col.FindOneAsync(c => c.NewName == "New Test Series");
				existingCriteria.Should().BeNull();
			}
		}

		[Test]
		public async Task CanGetExistingEpisodeCriteria()
		{
			var robotCriterias = new List<Core.Models.EpisodeCriteria>();

			await foreach (var criteria in repository.GetEpisodeCriteriasAsync("Mr. Robot"))
			{
				robotCriterias.Add(criteria);
			}

			var onePieceCriterias = new List<Core.Models.EpisodeCriteria>();

			await foreach (var criteria in repository.GetEpisodeCriteriasAsync("One Piece"))
			{
				onePieceCriterias.Add(criteria);
			}

			robotCriterias.Should().HaveCount(1);
			onePieceCriterias.Should().HaveCount(2);

			await Verifier.Verify(new
			{
				RobotCriterias = robotCriterias,
				OnePieceCriteries = onePieceCriterias
			});
		}

		[Test]
		public async Task CanInsertNewCriteriaToExistingCriterias()
		{
			const string SERIES_NAME = "One Piece";

			await repository.AddOrUpdateCriteriaAsync(new Core.Models.EpisodeCriteria
			{
				Episode = 92,
				Season = 1,
				NewSeason = 4,
				SeriesName = SERIES_NAME
			});

			await repository.SaveChangesAsync();
			await VerifyCriterias(SERIES_NAME);
		}

		[Test]
		public async Task CanInsertNewCriteriaToExistingSeriesWithoutCriterias()
		{
			const string SERIES_NAME = "Breaking Bad";

			{
				var col = factory.GetCollection<Criteria<EpisodeCriteria>>(EpisodeCriteriaRepository.TABLE_NAME);
				await col.InsertAsync(new Criteria<EpisodeCriteria>
				{
					Name = SERIES_NAME,
					Criterias = new List<EpisodeCriteria>()
				});
				await factory.CommitTransactionAsync();
			}

			await repository.AddOrUpdateCriteriaAsync(new Core.Models.EpisodeCriteria
			{
				NewEpisode = 5,
				Episode = 4543,
				Season = 1,
				NewSeason = 45,
				SeriesName = "Breaking Bad"
			});
			await repository.SaveChangesAsync();
			await VerifyCriterias(SERIES_NAME);
		}

		// NOTE: This is currently a bug, it should not add any child criterias
		[Test]
		public async Task CanInsertNewCriteriaWithoutEpisodeCriteria()
		{
			const string SERIES_NAME = "Rick and Morty";

			await repository.AddOrUpdateCriteriaAsync(new Core.Models.EpisodeCriteria
			{
				SeriesName = SERIES_NAME
			});
			await repository.SaveChangesAsync();
			await VerifyCriterias(SERIES_NAME);
		}

		[Test]
		public async Task CanInsertNewEpisodeCriteriaForNewSeries()
		{
			const string SERIES_NAME = "Test Series";

			await repository.AddOrUpdateCriteriaAsync(new Core.Models.EpisodeCriteria
			{
				Episode = 45,
				NewEpisode = 2,
				NewSeason = 3,
				NewSeries = "New Test Series",
				Season = 1,
				SeriesName = SERIES_NAME
			});

			await repository.SaveChangesAsync();
			await VerifyCriterias(SERIES_NAME);
		}

		[Test, Ignore("This is currently not working as expected.")]
		public async Task CanReplaceExistingWithSameOldEpisodeAndOldSeasonCriteria()
		{
			const string SERIES_NAME = "Mr. Robot";

			await repository.AddOrUpdateCriteriaAsync(new Core.Models.EpisodeCriteria
			{
				NewEpisode = 1,
				NewSeason = 4,
				Episode = 45,
				Season = 1,
				SeriesName = SERIES_NAME
			});

			await repository.SaveChangesAsync();
			await VerifyCriterias(SERIES_NAME);
		}

		[Test]
		public async Task CanUpdateExistingCriteriaWhenEpisodeAndSeasonIsNull()
		{
			const string SERIES_NAME = "Sherlock (2010-2017)";

			List<Criteria<EpisodeCriteria>> oldCriterias;

			{
				var criteriasCol = factory.GetCollection<Criteria<EpisodeCriteria>>(EpisodeCriteriaRepository.TABLE_NAME);

				oldCriterias = await criteriasCol.Query()
					.Include(c => c.Criterias)
					.Where(c => c.Name == SERIES_NAME)
					.ToListAsync();
			}

			await repository.AddOrUpdateCriteriaAsync(new Core.Models.EpisodeCriteria
			{
				Episode = 34,
				Season = 1,
				NewEpisode = 3,
				NewSeason = 4,
				SeriesName = SERIES_NAME
			});
			await repository.SaveChangesAsync();
			await VerifyCriterias(SERIES_NAME, oldCriterias);
		}

		protected override EpisodeCriteriaRepository CreateRepository(DatabaseFactory factory)
		{
			return new EpisodeCriteriaRepository(factory);
		}

		private async Task VerifyCriterias(string SERIES_NAME, List<Criteria<EpisodeCriteria>> oldCriterias = null)
		{
			var criteriasCol = factory.GetCollection<Criteria<EpisodeCriteria>>(EpisodeCriteriaRepository.TABLE_NAME);

			var criterias = await criteriasCol.Query()
				.Include(c => c.Criterias)
				.Where(c => c.Name == SERIES_NAME)
				.ToListAsync();

			if (oldCriterias is null)
			{
				await Verifier.Verify(criterias);
			}
			else
			{
				await Verifier.Verify(new
				{
					OldCriterias = oldCriterias,
					NewCriterias = criterias
				});
			}
		}
	}
}
