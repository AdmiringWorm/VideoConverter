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
		private readonly Core.Models.ConverterConfiguration configuration;

		public QueueRepositoryTests()
		{
			configuration = new Core.Models.ConverterConfiguration();
		}

		[TestCase("e684fc2b2ddd6bbf78bb371c5793eef4e977123a")]
		[TestCase("E684FC2B2DDD6BBF78BB371C5793EEF4E977123A")]
		[TestCase("6f0e875894c90eaea3a13f12a2235ca4218a1a3f")]
		[TestCase("6F0E875894C90EAEA3A13F12A2235CA4218A1A3F")]
		public async Task CanFindFileByHashAndFileIsCompleted(string hash)
		{
			var exists = await repository.FileExistsAsync("something", hash);

			exists.Should().BeTrue(because: "We expect the file being in the completed queue status!");
		}

		[TestCase("{current}/some-file-to-complet.mp4")]
		[TestCase("{CURRENT}/SOME-FILE-TO-COMPLET.MP4")]
		public async Task CanFindFileByPathWhenHashIsNull(string path)
		{
			var exists = await repository.FileExistsAsync(path, null);

			exists.Should().BeTrue();
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
		public async Task CanNotFindFileByHashEvenIfPathMatches()
		{
			var exists = await repository.FileExistsAsync("{current}/some-file-to-complet.mp4", "euoueao");

			exists.Should().BeFalse();
		}

		[TestCase("4e7b7e44ca6229e12303a752e5ce11598b81ebd4")]
		[TestCase("4E7B7E44CA6229E12303A752E5CE11598B81EBD4")]
		[TestCase("14ab1abf0a5400077ab014c3480778dbbbae61ca")]
		[TestCase("14AB1ABF0A5400077AB014C3480778DBBBAE61CA")]
		[TestCase("7947A63C055BC10CE00BE1A024EE278D387A5668")]
		[TestCase("7947a63c055bc10ce00be1a024ee278d387a5668")]
		[TestCase("5C2FAE6CE00BC4FD62B89A5DB4E71635F0EF6E6D")]
		[TestCase("5c2fae6ce00bc4fd62b89a5db4e71635f0ef6e6d")]
		[TestCase("1D34CBD648CA9E250578640ACFA0D11D9687CD73")]
		[TestCase("1d34cbd648ca9e250578640acfa0d11d9687cd73")]
		[TestCase("78A02226D06B7DF94565700876C171214FF73252")]
		[TestCase("78a02226d06b7df94565700876c171214ff73252")]
		public async Task CanNotFindFileByHashInNonCompletedStatuses(string hash)
		{
			var exists = await repository.FileExistsAsync("something", hash);

			exists.Should().BeFalse(because: "We expect a file not being in the completed state to behave as not existing!");
		}

		[TestCase("{current}/test-file.webm")]
		[TestCase("{current}/some-file2.mp4")]
		[TestCase("{tmp}/test-encoding.mp4")]
		public async Task CanNotFindFileByPathWhenHashIsNullAndFileIsNotCompleted(string path)
		{
			var exists = await repository.FileExistsAsync(path, null);

			exists.Should().BeFalse();
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
