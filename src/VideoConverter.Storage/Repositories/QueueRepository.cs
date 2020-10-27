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

            return col.FindById(identifier);
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
            var nextQueue = queueCol.Find(q => q.Path == queueItem.Path).FirstOrDefault();
            this.dbFactory.EnsureTransaction();

            if (nextQueue is not null)
            {
                if (nextQueue.Status == QueueStatus.Encoding)
                    return false;
                queueItem.Id = nextQueue.Id;
            }

            queueCol.Upsert(queueItem);
            this.dbFactory.CreateCheckpoint();

            return true;
        }

        public bool FileExists(string path, string hash)
        {
            var col = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);

            return col.Count(c => (c.Path != path && c.OldHash == hash) || c.NewHash == hash) > 0;
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
            var queueItem = queueCol.Find(q => q.Path == path).FirstOrDefault();
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
            var queueItem = queueCol.Find(q => q.Path == path).FirstOrDefault();
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

            return queueItem;
        }

        public IEnumerable<FileQueue> GetQueueItems(QueueStatus? status)
        {
            var queueCol = this.dbFactory.GetCollection<FileQueue>(TABLE_NAME);

            if (status is null)
                return queueCol.FindAll();
            else
                return queueCol.Find(q => q.Status == status);
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

            col.Update(queue);

            this.dbFactory.CreateCheckpoint();
        }
    }
}
