namespace VideoConverter.Storage.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LiteDB;
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

        public FileQueue GetQueueItem(int identifier)
        {
            var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);

            return ReplacePrefixes(col.FindById(identifier));
        }

        public int RemoveQueueItems(QueueStatus? status)
        {
            var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
            this.dbFactory.EnsureTransaction();

            if (status is null)
                return col.DeleteMany(c =>
                    c.Status == QueueStatus.Completed ||
                    c.Status == QueueStatus.Failed ||
                    c.Status == QueueStatus.Pending);
            else
                return col.DeleteMany(c => c.Status == status);
        }

        public void RemoveQueueItem(int id)
        {
            var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
            this.dbFactory.EnsureTransaction();

            col.Delete(id);

            this.dbFactory.CreateCheckpoint();
        }

        public long GetQueueItemCount(QueueStatus? status)
        {
            var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
            if (status is null)
                return col.LongCount();
            else
                return col.LongCount(c => c.Status == status);
        }

        public bool AddToQueue(FileQueue queueItem)
        {
            var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
            string path = ReplaceWithPrefix(queueItem.Path);
            var nextQueue = queueCol.Find(q => q.Path == queueItem.Path).FirstOrDefault();
            this.dbFactory.EnsureTransaction();

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
            nextQueue.Exception = queueItem.Exception;
            nextQueue.NewHash = queueItem.NewHash;
            nextQueue.OldHash = queueItem.OldHash;
            nextQueue.OutputPath = ReplaceWithPrefix(queueItem.OutputPath);
            nextQueue.Path = path;
            nextQueue.Status = queueItem.Status;
            nextQueue.StatusMessage = queueItem.StatusMessage;
            nextQueue.Streams = queueItem.Streams;
            nextQueue.SubtitleCodec = queueItem.SubtitleCodec;
            nextQueue.VideoCodec = queueItem.VideoCodec;

            queueCol.Upsert(nextQueue);
            this.dbFactory.CreateCheckpoint();

            return true;
        }

        public bool FileExists(string path, string hash)
        {
            var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
            var prefixedPath = ReplaceWithPrefix(path);

            return col.Count(c => (c.Path != prefixedPath && c.OldHash == hash) || c.NewHash == hash) > 0;
        }

        public void ResetFailedQueue()
        {
            var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
            var failedFiles = queueCol.Find(q => q.Status == QueueStatus.Failed);

            this.dbFactory.EnsureTransaction();

            foreach (var file in failedFiles)
            {
                file.Status = QueueStatus.Pending;
                file.StatusMessage = null;
                file.Exception = null;
                queueCol.Update(file);
                this.dbFactory.CreateCheckpoint();
            }
        }

        public void UpdateQueueStatus(string path, QueueStatus status, string? statusMessage = null)
        {
            var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
            var prefixedPath = ReplaceWithPrefix(path);
            var queueItem = queueCol.Find(q => q.Path == prefixedPath).FirstOrDefault();
            if (queueItem is not null)
            {
                this.dbFactory.EnsureTransaction();
                queueItem.Status = status;
                queueItem.StatusMessage = statusMessage;
                queueItem.Exception = null;

                queueCol.Update(queueItem);

                this.dbFactory.CreateCheckpoint();
            }
        }

        public void UpdateQueueStatus(string path, QueueStatus status, Exception exception)
        {
            var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
            var prefixedPath = ReplaceWithPrefix(path);
            var queueItem = queueCol.Find(q => q.Path == prefixedPath).FirstOrDefault();
            if (queueItem is not null)
            {
                this.dbFactory.EnsureTransaction();
                queueItem.Status = status;
                queueItem.StatusMessage = null;
                queueItem.Exception = exception;

                this.dbFactory.CreateCheckpoint();
            }
        }

        public void UpdateQueueStatus(int id, QueueStatus status, string? statusMessage = null)
        {
            var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
            var queueItem = queueCol.FindById(id);

            this.dbFactory.EnsureTransaction();

            queueItem.Status = status;
            queueItem.StatusMessage = statusMessage;
            queueItem.Exception = null;

            queueCol.Update(queueItem);

            this.dbFactory.CreateCheckpoint();
        }

        public void UpdateQueueStatus(int id, QueueStatus status, Exception exception)
        {
            var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
            var queueItem = queueCol.FindById(id);

            this.dbFactory.EnsureTransaction();

            queueItem.Status = status;
            queueItem.StatusMessage = null;
            queueItem.Exception = exception;

            queueCol.Update(queueItem);

            this.dbFactory.CreateCheckpoint();
        }

        public int GetPendingQueueCount()
        {
            var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);

            return col.Count(c => c.Status == QueueStatus.Pending);
        }

        public FileQueue? GetNextQueueItem()
        {
            var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);
            var queueItem = queueCol.Find(q => q.Status == QueueStatus.Pending).FirstOrDefault();

            if (queueItem is not null)
            {
                this.dbFactory.EnsureTransaction();
                queueItem.Status = QueueStatus.Encoding;
                queueCol.Update(queueItem);
                this.dbFactory.CreateCheckpoint();
            }
            else
                return null;

            return ReplacePrefixes(queueItem);
        }

        public IEnumerable<FileQueue> GetQueueItems(QueueStatus? status)
        {
            var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);

            if (status is null)
                return queueCol.FindAll().Select(f => ReplacePrefixes(f));
            else
                return queueCol.Find(q => q.Status == status).Select(f => ReplacePrefixes(f));
        }

        public void SaveChanges()
        {
            this.dbFactory.CommitTransaction();
        }

        public void AbortChanges()
        {
            this.dbFactory.RollbackTransaction();
        }

        public void UpdateQueue(FileQueue queue)
        {
            var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);

            this.dbFactory.EnsureTransaction();

            var foundQueue = col.FindById(queue.Id);

            foundQueue.AudioCodec = queue.AudioCodec;
            foundQueue.Exception = queue.Exception;
            foundQueue.NewHash = queue.NewHash;
            foundQueue.OldHash = queue.OldHash;
            foundQueue.OutputPath = ReplaceWithPrefix(foundQueue.OutputPath);
            foundQueue.Path = ReplaceWithPrefix(foundQueue.Path);
            foundQueue.Status = queue.Status;
            foundQueue.StatusMessage = queue.StatusMessage;
            foundQueue.Streams = queue.Streams;
            foundQueue.SubtitleCodec = queue.SubtitleCodec;
            foundQueue.VideoCodec = queue.VideoCodec;

            col.Update(foundQueue);

            this.dbFactory.CreateCheckpoint();
        }

        private FileQueue ReplacePrefixes(FileQueue fileQueue)
        {
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
                if (prefixedPath.StartsWith($"{{{prefix.Prefix}}}", StringComparison.OrdinalIgnoreCase))

                {
                    var path = prefixedPath.Replace($"{{{prefix.Prefix}}}", prefix.Path, StringComparison.OrdinalIgnoreCase);
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
                    foundPaths.Add(path.Replace(prefix.Path, $"{{{prefix.Prefix}}}", StringComparison.OrdinalIgnoreCase));
                }
            }

            if (foundPaths.Count == 0)
                return path;

            return foundPaths.OrderBy(f => f.Length).First();
        }
    }
}
