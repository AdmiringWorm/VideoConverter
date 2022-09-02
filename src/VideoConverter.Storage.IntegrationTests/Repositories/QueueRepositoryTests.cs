namespace VideoConverter.Storage.IntegrationTests.Repositories
{
	using System.Collections.Generic;
	using System.Threading.Tasks;

	using FluentAssertions;

	using NUnit.Framework;

	using VideoConverter.Storage.Database;
	using VideoConverter.Storage.Models;
	using VideoConverter.Storage.Repositories;

	using static VerifyNUnit.Verifier;

	public class QueueRepositoryTests : BaseRepositoryTests<QueueRepository>
	{
		private readonly Core.Models.Configuration configuration;

		public QueueRepositoryTests()
		{
			configuration = new Core.Models.Configuration();
		}

		[Test]
		public async Task CanGetAllItemsByStatus([Values] QueueStatus? status)
		{
			var items = new List<FileQueue>();

			await foreach (var item in repository.GetQueueItemsAsync(status))
			{
				items.Add(item);
			}

			await Verify(items);
		}

		[Test]
		public async Task CanGetItemByIdentifier([Values(1, 2, 3, 4)] int id)
		{
			var item = await repository.GetQueueItemAsync(id);

			await Verify(item);
		}

		[TestCase("my-path/some-file-to-complet.mp4")]
		[TestCase("my-path/test-file.webm")]
		[TestCase("my-path/some-file.mp4")]
		[TestCase("my-path/some-file.mkv")]
		public async Task CanGetItemByPath(string path)
		{
			var item = await repository.GetQueueItemAsync(path);

			await Verify(item);
		}

		[Test]
		public async Task CanGetItemCountByStatus([Values] QueueStatus? status)
		{
			var count = await repository.GetQueueItemCountAsync(status);

			await Verify(count);
		}

		[Test]
		public async Task CanGetNextItemInTheQueue()
		{
			var pendingCount = await repository.GetPendingQueueCountAsync();
			pendingCount.Should().Be(1);

			var item = await repository.GetNextQueueItemAsync();

			pendingCount = await repository.GetPendingQueueCountAsync();
			pendingCount.Should().Be(0);

			await Verify(item);
		}

		[Test]
		public async Task CanRemoveItemByIdentifier()
		{
			var item = await repository.GetQueueItemAsync(1);
			item.Should().NotBeNull();

			await repository.RemoveQueueItemAsync(1);

			item = await repository.GetQueueItemAsync(1);

			item.Should().BeNull();
		}

		[Test]
		public async Task CanRemoveItemsByStatus([Values] QueueStatus? status)
		{
			var items = new List<FileQueue>();

			await foreach (var item in repository.GetQueueItemsAsync(status))
			{
				items.Add(item);
			}

			items.Should().NotBeEmpty();

			await repository.RemoveQueueItemsAsync(status);
			await repository.SaveChangesAsync();

			items.Clear();

			await foreach (var item in repository.GetQueueItemsAsync(status))
			{
				items.Add(item);
			}

			if (status is null)
			{
				items.Should().HaveCount(1);
			}
			else
			{
				items.Should().BeEmpty();
			}
		}

		[SetUp]
		public void ClearConfiguration()
		{
			configuration.Reset();
			configuration.Prefixes.AddRange(new[]
			{
				new Core.Models.PrefixConfiguration
				{
					Path = "my-path",
					Prefix = "current"
				},
				new Core.Models.PrefixConfiguration
				{
					Path = "my-temp",
					Prefix = "tmp"
				}
			});
			configuration.WorkDirectory = "tmp";
		}

		[Test]
		public async Task ResettingFailingQueueMarksAllFilesAsPending()
		{
			await repository.ResetFailedQueueAsync();
			await repository.SaveChangesAsync();

			var failedItems = new List<FileQueue>();
			var pendingItems = new List<FileQueue>();

			await foreach (var item in repository.GetQueueItemsAsync(QueueStatus.Failed))
			{
				failedItems.Add(item);
			}

			await foreach (var item in repository.GetQueueItemsAsync(QueueStatus.Pending))
			{
				pendingItems.Add(item);
			}

			failedItems.Should().BeEmpty();
			pendingItems.Should().HaveCount(2);
		}

		protected override QueueRepository CreateRepository(DatabaseFactory factory)
		{
			return new QueueRepository(factory, configuration);
		}
	}
}
