namespace VideoConverter.Storage.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;

	using VideoConverter.Storage.Database;
	using VideoConverter.Storage.Models;
	using VideoConverter.Storage.Repositories;

	using Configuration = Core.Models.Configuration;

	public static class DatabaseFactoryHelper
	{
		private static Configuration configuration;

		public static async Task CleanTestFactory(DatabaseFactory factory)
		{
			await factory.RollbackTransactionAsync();
			factory.Dispose();

			if (configuration is not null && File.Exists(configuration.MapperDatabase))
			{
				File.Delete(configuration.MapperDatabase);
				configuration = null;
			}
		}

		public static async Task<DatabaseFactory> CreateTestFactory()
		{
			if (configuration is null)
			{
				var path = Path.Combine(
					Path.GetTempPath(),
					Guid.NewGuid() + ".tmp-db");

				configuration = new Configuration
				{
					MapperDatabase = path
				};
			}

			var factory = new DatabaseFactory(configuration);

			await CreateTestData(factory);

			return factory;
		}

		private static async Task CreateTestData(DatabaseFactory factory)
		{
			var episodeCriteria = new[]
			{
				new EpisodeCriteria
				{
					NewEpisode = 2,
					NewName = "New Test Name",
					NewSeason = 2,
					OldEpisode = 45,
					OldSeason = 1
				}
			};

			var criterias = new[]
			{
				new Criteria<EpisodeCriteria>
				{
					Name = "Mr. Robot",
					Criterias = new[]
					{
						new EpisodeCriteria
						{
							NewEpisode = 2,
							NewName = "The absolutely best Robot",
							NewSeason = 2,
							OldEpisode = 45,
							OldSeason = 1
						}
					}.ToList()
				},
				new Criteria<EpisodeCriteria>
				{
					Name = "One Piece",
					Criterias = new[]
					{
						new EpisodeCriteria
						{
							NewSeason = 2,
							OldSeason = 1,
							OldEpisode = 62
						},
						new EpisodeCriteria
						{
							NewSeason = 3,
							OldSeason = 1,
							OldEpisode = 78
						}
					}.ToList()
				},
				new Criteria<EpisodeCriteria>
				{
					Name = "Sherlock (2010-2017)",
					Criterias = new []
					{
						new EpisodeCriteria
						{
							NewName = "Sherlock"
						}
					}.ToList()
				}
			};

			var episodeCol = factory.GetCollection<EpisodeCriteria>();
			await episodeCol.InsertBulkAsync(criterias.SelectMany(c => c.Criterias));

			var criteriaCol = factory.GetCollection<Criteria<EpisodeCriteria>>(EpisodeCriteriaRepository.TABLE_NAME);
			await criteriaCol.InsertBulkAsync(criterias);

			var queueItems = new[]
			{
				new FileQueue
				{
					AudioCodec = "copy",
					OutputPath = "{tmp}/test-completed.mp4",
					Path = "{current}/some-file-to-complet.mp4",
					Status = QueueStatus.Completed,
					StereoMode = Core.Models.StereoScopicMode.BottomTop,
					SkipThumbnails = true,
					StatusMessage = "This file was completed successfully",
					Streams = new List<int>{3,2, 7},
					SubtitleCodec = "copy",
					VideoCodec = "libx264",
					NewHash = "e684fc2b2ddd6bbf78bb371c5793eef4e977123a",
					OldHash = "6f0e875894c90eaea3a13f12a2235ca4218a1a3f"
				},
				new FileQueue
				{
					Status = QueueStatus.Failed,
					AudioCodec = "libopus",
					OutputPath = "{tmp}/",
					Path = "{current}/test-file.webm"
				},
				new FileQueue
				{
					AudioCodec = "libopus",
					OutputPath = "{tmp}/test-pending.mkv",
					Path = "{current}/some-file.mp4",
					Status = QueueStatus.Pending,
					SubtitleCodec = "copy",
					VideoCodec = "hevc"
				},
				new FileQueue
				{
					AudioCodec = "copy",
					OutputPath = "{tmp}/test-encoding.mp4",
					Path = "{current}/some-file.mkv",
					Status = QueueStatus.Encoding,
					SubtitleCodec = "copy",
					VideoCodec = "copy"
				}
			};

			var queueCol = factory.GetCollection<FileQueue>(QueueRepository.TABLE_NAME);
			await queueCol.InsertBulkAsync(queueItems);

			await factory.CommitTransactionAsync();
		}
	}
}
