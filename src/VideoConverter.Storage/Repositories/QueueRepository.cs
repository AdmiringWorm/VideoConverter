namespace VideoConverter.Storage.Repositories
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using LiteDB;
	using VideoConverter.Core.Assertions;
	using VideoConverter.Core.Models;
	using VideoConverter.Storage.Database;
	using VideoConverter.Storage.Models;

	public class QueueRepository
	{
		private readonly DatabaseFactory dbFactory;
		private readonly Configuration config;
		private const string TABLE_NAME = "queue";

		public QueueRepository(DatabaseFactory dbFactory, Configuration config)
		{
			this.config = config;
			this.dbFactory = dbFactory;
		}

		public async Task<FileQueue> GetQueueItemAsync(int identifier)
		{
			var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);

			return ReplacePrefixes(await col.FindByIdAsync(identifier).ConfigureAwait(false))!;
		}

		public async Task<FileQueue?> GetQueueItemAsync(string path)
		{
			var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);

			var prefixedPath = ReplaceWithPrefix(path);

			var queue = await col.Query().Where(q => q.Path == prefixedPath || q.OutputPath == prefixedPath).FirstOrDefaultAsync().ConfigureAwait(false);

			return ReplacePrefixes(queue);
		}

		public async Task<int> RemoveQueueItemsAsync(QueueStatus? status)
		{
			var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			await this.dbFactory.EnsureTransactionAsync().ConfigureAwait(false);

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

		public async Task RemoveQueueItemAsync(int id)
		{
			var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			await this.dbFactory.EnsureTransactionAsync().ConfigureAwait(false);

			await col.DeleteAsync(id).ConfigureAwait(false);

			await this.dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
		}

		public Task<long> GetQueueItemCountAsync(QueueStatus? status)
		{
			var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			if (status is null)
				return col.LongCountAsync();
			else
				return col.LongCountAsync(c => c.Status == status);
		}

		public async Task<bool> AddToQueueAsync(FileQueue queueItem)
		{
			if (queueItem is null)
				throw new ArgumentNullException(nameof(queueItem));

			var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			string path = ReplaceWithPrefix(queueItem.Path);
			var nextQueue = await queueCol.Query().Where(q => q.Path == path).FirstOrDefaultAsync().ConfigureAwait(false);
			await this.dbFactory.EnsureTransactionAsync().ConfigureAwait(false);

			if (nextQueue is not null)
			{
				if (nextQueue.Status == QueueStatus.Encoding)
					return false;
			}
			else
			{
				nextQueue = new FileQueue();
			}

			nextQueue.AudioCodec = queueItem.AudioCodec;
			nextQueue.NewHash = queueItem.NewHash;
			nextQueue.OldHash = queueItem.OldHash;
			nextQueue.OutputPath = ReplaceWithPrefix(queueItem.OutputPath);
			nextQueue.Path = path;
			nextQueue.Status = queueItem.Status;
			nextQueue.StatusMessage = queueItem.StatusMessage;
			nextQueue.Streams = queueItem.Streams;
			nextQueue.SubtitleCodec = queueItem.SubtitleCodec;
			nextQueue.VideoCodec = queueItem.VideoCodec;

			await queueCol.UpsertAsync(nextQueue).ConfigureAwait(false);
			await this.dbFactory.CreateCheckpointAsync().ConfigureAwait(false);

			return true;
		}

		public Task<bool> FileExistsAsync(string path, string? hash)
		{
			var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			var prefixedPath = ReplaceWithPrefix(path);

			if (hash is null)
				return col.ExistsAsync(c => c.Path == prefixedPath);
			else
				return col.ExistsAsync(c => (c.Path != prefixedPath && c.OldHash == hash) || c.NewHash == hash);
		}

		public async Task ResetFailedQueueAsync()
		{
			var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);

			var items = await queueCol.FindAsync(q => q.Status == QueueStatus.Failed).ConfigureAwait(false);

			await this.dbFactory.EnsureTransactionAsync().ConfigureAwait(false);

			await queueCol.UpdateAsync(items.Select(i => SetPendingStatus(i))).ConfigureAwait(false);

			await this.dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
		}

		public async Task UpdateQueueStatusAsync(string path, QueueStatus status, string? statusMessage = null)
		{
			var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			var prefixedPath = ReplaceWithPrefix(path);
			var queueItem = await queueCol.Query().Where(q => q.Path == prefixedPath).FirstOrDefaultAsync().ConfigureAwait(false);
			if (queueItem is not null)
			{
				await this.dbFactory.EnsureTransactionAsync().ConfigureAwait(false);
				queueItem.Status = status;
				queueItem.StatusMessage = statusMessage;

				await queueCol.UpdateAsync(queueItem).ConfigureAwait(false);

				await this.dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
			}
		}

		public async Task UpdateQueueStatusAsync(string path, QueueStatus status, Exception exception)
		{
			var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			var prefixedPath = ReplaceWithPrefix(path);
			var queueItem = await queueCol.Query().Where(q => q.Path == prefixedPath).FirstOrDefaultAsync().ConfigureAwait(false);
			if (queueItem is not null)
			{
				await this.dbFactory.EnsureTransactionAsync().ConfigureAwait(false);
				queueItem.Status = status;
				if (exception is not null)
					queueItem.StatusMessage = exception.Message + "\n\n" + exception.StackTrace;
				else
					queueItem.StatusMessage = string.Empty;

				await this.dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
			}
		}

		public async Task UpdateQueueStatusAsync(int id, QueueStatus status, string? statusMessage = null)
		{
			var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			var queueItem = await queueCol.FindByIdAsync(id).ConfigureAwait(false);

			await this.dbFactory.EnsureTransactionAsync().ConfigureAwait(false);

			queueItem.Status = status;
			queueItem.StatusMessage = statusMessage;

			await queueCol.UpdateAsync(queueItem).ConfigureAwait(false);

			await this.dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
		}

		public async Task UpdateQueueStatusAsync(int id, QueueStatus status, Exception exception)
		{
			var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			var queueItem = await queueCol.FindByIdAsync(id).ConfigureAwait(false);

			await this.dbFactory.EnsureTransactionAsync().ConfigureAwait(false);

			queueItem.Status = status;
			if (exception is not null)
				queueItem.StatusMessage = exception.Message + "\n\n" + exception.StackTrace;
			else
				queueItem.StatusMessage = string.Empty;

			await queueCol.UpdateAsync(queueItem).ConfigureAwait(false);

			await this.dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
		}

		public Task<int> GetPendingQueueCountAsync()
		{
			var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);

			return col.CountAsync(c => c.Status == QueueStatus.Pending);
		}

		public async Task<FileQueue?> GetNextQueueItemAsync()
		{
			var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
			var queueItem = await queueCol.Query().Where(q => q.Status == QueueStatus.Pending).FirstOrDefaultAsync().ConfigureAwait(false);

			if (queueItem is not null)
			{
				await this.dbFactory.EnsureTransactionAsync().ConfigureAwait(false);
				queueItem.Status = QueueStatus.Encoding;
				await queueCol.UpdateAsync(queueItem).ConfigureAwait(false);
				await this.dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
			}
			else
			{
				return null;
			}

			return ReplacePrefixes(queueItem);
		}

		public async IAsyncEnumerable<FileQueue> GetQueueItemsAsync(QueueStatus? status)
		{
			var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);

			IEnumerable<FileQueue> items;

			if (status is null)
				items = await queueCol.FindAllAsync().ConfigureAwait(false);
			else
				items = await queueCol.FindAsync(q => q.Status == status).ConfigureAwait(false);

			foreach (var item in items)
			{
				yield return ReplacePrefixes(item)!;
			}
		}

		public Task SaveChangesAsync()
		{
			return this.dbFactory.CommitTransactionAsync();
		}

		public Task AbortChangesAsync()
		{
			return this.dbFactory.RollbackTransactionAsync();
		}

		public async Task UpdateQueueAsync(FileQueue queue)
		{
			if (queue is null)
				throw new ArgumentNullException(nameof(queue));

			var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);

			await this.dbFactory.EnsureTransactionAsync().ConfigureAwait(false);

			var foundQueue = await col.FindByIdAsync(queue.Id).ConfigureAwait(false);

			foundQueue.AudioCodec = queue.AudioCodec;
			foundQueue.NewHash = queue.NewHash;
			foundQueue.OldHash = queue.OldHash;
			foundQueue.OutputPath = ReplaceWithPrefix(foundQueue.OutputPath);
			foundQueue.Path = ReplaceWithPrefix(foundQueue.Path);
			foundQueue.Status = queue.Status;
			foundQueue.StatusMessage = queue.StatusMessage;
			foundQueue.Streams = queue.Streams;
			foundQueue.SubtitleCodec = queue.SubtitleCodec;
			foundQueue.VideoCodec = queue.VideoCodec;

			await col.UpdateAsync(foundQueue).ConfigureAwait(false);

			await this.dbFactory.CreateCheckpointAsync().ConfigureAwait(false);
		}

		private static FileQueue SetPendingStatus(FileQueue queue)
		{
			queue.Status = QueueStatus.Pending;
			queue.StatusMessage = null;
			return queue;
		}

		private FileQueue? ReplacePrefixes(FileQueue? fileQueue)
		{
			if (fileQueue is null)
				return null;

			fileQueue.Path = ReplacePrefixes(fileQueue.Path);
			fileQueue.OutputPath = ReplacePrefixes(fileQueue.OutputPath);
			return fileQueue;
		}

		private string ReplacePrefixes(string? prefixedPath)
		{
			if (prefixedPath is null)
				return string.Empty;

			foreach (var prefix in this.config.Prefixes)
			{
				if (prefixedPath.StartsWith($"{{{prefix.Prefix.TrimEnd('/')}}}", StringComparison.OrdinalIgnoreCase))

				{
					var path = prefixedPath.Replace($"{{{prefix.Prefix.TrimEnd('/')}}}", prefix.Path, StringComparison.OrdinalIgnoreCase);
					return path;
				}
			}

			return prefixedPath;
		}

		private string ReplaceWithPrefix(string? path)
		{
			if (path is null)
				return string.Empty;

			var foundPaths = new List<string>();

			foreach (var prefix in this.config.Prefixes)
			{
				if (path.StartsWith(prefix.Path, StringComparison.OrdinalIgnoreCase))
				{
					foundPaths.Add(path.Replace(prefix.Path, $"{{{prefix.Prefix.TrimEnd('/')}}}", StringComparison.OrdinalIgnoreCase));
				}
			}

			if (foundPaths.Count == 0)
				return path;

			return foundPaths.OrderBy(f => f.Length).First();
		}
	}
}
