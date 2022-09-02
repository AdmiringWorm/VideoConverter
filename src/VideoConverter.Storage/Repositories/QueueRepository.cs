namespace VideoConverter.Storage.Repositories
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;

	using LiteDB;
	using LiteDB.Async;

	using VideoConverter.Core.Assertions;
	using VideoConverter.Core.Models;
	using VideoConverter.Storage.Database;
	using VideoConverter.Storage.Models;

	public class QueueRepository
	{
		private const string TABLE_NAME = "queue";
		private readonly Configuration config;
		private readonly DatabaseFactory dbFactory;

		public QueueRepository(DatabaseFactory dbFactory, Configuration config)
		{
			this.config = config;
			this.dbFactory = dbFactory;
		}

		public Task AbortChangesAsync()
		{
			return dbFactory.RollbackTransactionAsync();
		}

		public async Task<bool> AddToQueueAsync(FileQueue queueItem)
		{
			queueItem.AssertNotNull();

			var queueCol = dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			var path = ReplaceWithPrefix(queueItem.Path);
			var nextQueue = await queueCol
				.Query()
				.Where(q => q.Path == path)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);
			await dbFactory.EnsureTransactionAsync().ConfigureAwait(false);

			if (nextQueue is not null)
			{
				if (nextQueue.Status == QueueStatus.Encoding)
				{
					return false;
				}
			}
			else
			{
				nextQueue = new FileQueue();
			}

			nextQueue.AudioCodec = queueItem.AudioCodec;
			nextQueue.InputParameters = queueItem.InputParameters;
			nextQueue.NewHash = queueItem.NewHash;
			nextQueue.OldHash = queueItem.OldHash;
			nextQueue.OutputPath = ReplaceWithPrefix(queueItem.OutputPath);
			nextQueue.Parameters = queueItem.Parameters;
			nextQueue.Path = path;
			nextQueue.Status = queueItem.Status;
			nextQueue.StatusMessage = queueItem.StatusMessage;
			nextQueue.StereoMode = queueItem.StereoMode;
			nextQueue.Streams = queueItem.Streams;
			nextQueue.SubtitleCodec = queueItem.SubtitleCodec;
			nextQueue.VideoCodec = queueItem.VideoCodec;

			await queueCol.UpsertAsync(nextQueue).ConfigureAwait(false);
			await dbFactory.CreateCheckpointAsync().ConfigureAwait(false);

			return true;
		}

		public Task<bool> FileExistsAsync(string path, string? hash)
		{
			var col = dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			var prefixedPath = ReplaceWithPrefix(path);

			if (hash is null)
			{
				return col.ExistsAsync(c => c.Path == prefixedPath);
			}
			else
			{
				return col.ExistsAsync(c => (c.Path != prefixedPath && c.OldHash == hash) || c.NewHash == hash);
			}
		}

		public async Task<FileQueue?> GetNextQueueItemAsync()
		{
			var queueCol = dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			var queueItem = await queueCol
				.Query().Where(q => q.Status == QueueStatus.Pending).FirstOrDefaultAsync().ConfigureAwait(false);

			if (queueItem is not null)
			{
				await dbFactory.EnsureTransactionAsync().ConfigureAwait(false);
				queueItem.Status = QueueStatus.Encoding;
				await queueCol.UpdateAsync(queueItem).ConfigureAwait(false);
				await dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
			}
			else
			{
				return null;
			}

			return ReplacePrefixes(queueItem);
		}

		public async Task<int> GetPendingQueueCountAsync()
		{
			var col = dbFactory.GetCollection<FileQueue>(TABLE_NAME);

			var result = await col.CountAsync(c => c.Status == QueueStatus.Pending).ConfigureAwait(false);

			return result;
		}

		public async Task<FileQueue> GetQueueItemAsync(int identifier)
		{
			var col = dbFactory.GetCollection<FileQueue>(TABLE_NAME);

			return ReplacePrefixes(await col.FindByIdAsync(identifier).ConfigureAwait(false))!;
		}

		public async Task<FileQueue?> GetQueueItemAsync(string path)
		{
			var col = dbFactory.GetCollection<FileQueue>(TABLE_NAME);

			var prefixedPath = ReplaceWithPrefix(path);

			var queue = await col
				.Query()
				.Where(q => q.Path == prefixedPath || q.OutputPath == prefixedPath)
				.FirstOrDefaultAsync()
				.ConfigureAwait(false);

			return ReplacePrefixes(queue);
		}

		public Task<long> GetQueueItemCountAsync(QueueStatus? status)
		{
			var col = dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			if (status is null)
			{
				return col.LongCountAsync();
			}
			else
			{
				return col.LongCountAsync(c => c.Status == status);
			}
		}

		public async IAsyncEnumerable<FileQueue> GetQueueItemsAsync(QueueStatus? status)
		{
			var queueCol = dbFactory.GetCollection<FileQueue>(TABLE_NAME);

			var items = status is null
				? await queueCol.FindAllAsync().ConfigureAwait(false)
				: await queueCol.FindAsync(q => q.Status == status).ConfigureAwait(false);

			foreach (var item in items)
			{
				yield return ReplacePrefixes(item)!;
			}
		}

		public async Task RemoveQueueItemAsync(int id)
		{
			var col = dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			await dbFactory.EnsureTransactionAsync().ConfigureAwait(false);

			await col.DeleteAsync(id).ConfigureAwait(false);

			await dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
		}

		public async Task<int> RemoveQueueItemsAsync(QueueStatus? status)
		{
			var col = dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			await dbFactory.EnsureTransactionAsync().ConfigureAwait(false);

			if (status is null)
			{
				return await col.DeleteManyAsync(c =>
					c.Status == QueueStatus.Completed ||
					c.Status == QueueStatus.Failed ||
					c.Status == QueueStatus.Pending)
						.ConfigureAwait(false);
			}
			else
			{
				return await col.DeleteManyAsync(c => c.Status == status)
					.ConfigureAwait(false);
			}
		}

		public async Task ResetFailedQueueAsync()
		{
			var queueCol = dbFactory.GetCollection<FileQueue>(TABLE_NAME);

			var items = await queueCol.FindAsync(q => q.Status == QueueStatus.Failed).ConfigureAwait(false);

			await dbFactory.EnsureTransactionAsync().ConfigureAwait(false);

			await queueCol.UpdateAsync(items.Select(i => SetPendingStatus(i))).ConfigureAwait(false);

			await dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
		}

		public Task SaveChangesAsync()
		{
			return dbFactory.CommitTransactionAsync();
		}

		public async Task UpdateQueueAsync(FileQueue queue)
		{
			queue.AssertNotNull();

			var col = dbFactory.GetCollection<FileQueue>(TABLE_NAME);

			await dbFactory.EnsureTransactionAsync().ConfigureAwait(false);

			var foundQueue = await col.FindByIdAsync(queue.Id).ConfigureAwait(false);

			foundQueue.AudioCodec = queue.AudioCodec;
			foundQueue.InputParameters = queue.InputParameters;
			foundQueue.NewHash = queue.NewHash;
			foundQueue.OldHash = queue.OldHash;
			foundQueue.OutputPath = ReplaceWithPrefix(foundQueue.OutputPath);
			foundQueue.Parameters = queue.Parameters;
			foundQueue.Path = ReplaceWithPrefix(foundQueue.Path);
			foundQueue.Status = queue.Status;
			foundQueue.StatusMessage = queue.StatusMessage;
			foundQueue.Streams = queue.Streams;
			foundQueue.SubtitleCodec = queue.SubtitleCodec;
			foundQueue.VideoCodec = queue.VideoCodec;

			await col.UpdateAsync(foundQueue).ConfigureAwait(false);

			await dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
		}

		public async Task UpdateQueueStatusAsync(string path, QueueStatus status, string? statusMessage = null)
		{
			var id = await FirstOrDefaultQueueItemIdAsync(path).ConfigureAwait(false);

			if (id <= 0)
			{
				return;
			}

			await UpdateQueueStatusAsync(id, status, statusMessage).ConfigureAwait(false);
		}

		public async Task UpdateQueueStatusAsync(string path, QueueStatus status, Exception exception)
		{
			var id = await FirstOrDefaultQueueItemIdAsync(path).ConfigureAwait(false);

			if (id <= 0)
			{
				return;
			}

			await UpdateQueueStatusAsync(id, status, exception).ConfigureAwait(false);
		}

		public async Task UpdateQueueStatusAsync(int id, QueueStatus status, string? statusMessage = null)
		{
			var queueCol = dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			var queueItem = await queueCol.FindByIdAsync(id).ConfigureAwait(false);

			queueItem.Status = status;
			queueItem.StatusMessage = statusMessage;

			await UpdateQueueStatusAsync(queueItem, queueCol).ConfigureAwait(false);
		}

		public Task UpdateQueueStatusAsync(int id, QueueStatus status, Exception exception)
		{
			var statusMessage = exception is not null
				? exception.Message + "\n\n" + exception.StackTrace
				: string.Empty;
			return UpdateQueueStatusAsync(id, status, statusMessage);
		}

		private static FileQueue SetPendingStatus(FileQueue queue)
		{
			queue.Status = QueueStatus.Pending;
			queue.StatusMessage = null;
			return queue;
		}

		private Task<int> FirstOrDefaultQueueItemIdAsync(string path)
		{
			var queueCol = dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			var prefixedPath = ReplaceWithPrefix(path);

			return queueCol
				.Query()
				.Where(q => q.Path == prefixedPath)
				.Select(q => q.Id)
				.FirstOrDefaultAsync();
		}

		private FileQueue? ReplacePrefixes(FileQueue? fileQueue)
		{
			if (fileQueue is null)
			{
				return null;
			}

			fileQueue.Path = ReplacePrefixes(fileQueue.Path);
			fileQueue.OutputPath = ReplacePrefixes(fileQueue.OutputPath);
			return fileQueue;
		}

		private string ReplacePrefixes(string? prefixedPath)
		{
			if (prefixedPath is null)
			{
				return string.Empty;
			}

			foreach (var prefix in config.Prefixes)
			{
				if (prefixedPath.StartsWith($"{{{prefix.Prefix.TrimEnd('/')}}}", StringComparison.OrdinalIgnoreCase))

				{
					return prefixedPath.Replace(
						$"{{{prefix.Prefix.TrimEnd('/')}}}",
						prefix.Path,
						StringComparison.OrdinalIgnoreCase
					);
				}
			}

			return prefixedPath;
		}

		private string ReplaceWithPrefix(string? path)
		{
			if (path is null)
			{
				return string.Empty;
			}

			var foundPaths = new List<string>();

			foreach (var prefix in config.Prefixes)
			{
				if (path.StartsWith(prefix.Path, StringComparison.OrdinalIgnoreCase))
				{
					foundPaths.Add(path.Replace(prefix.Path, $"{{{prefix.Prefix.TrimEnd('/')}}}", StringComparison.OrdinalIgnoreCase));
				}
			}

			if (foundPaths.Count == 0)
			{
				return path;
			}

			return foundPaths.OrderBy(f => f.Length).First();
		}

		private async Task UpdateQueueStatusAsync(FileQueue queueItem, ILiteCollectionAsync<FileQueue> queueCol)
		{
			await dbFactory.EnsureTransactionAsync().ConfigureAwait(false);

			await queueCol.UpdateAsync(queueItem).ConfigureAwait(false);

			await dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
		}
	}
}
