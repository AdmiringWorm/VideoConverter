namespace VideoConverter.Storage.Database
{
    using System;
    using LiteDB;
    using VideoConverter.Storage.Models;

    public sealed class DatabaseFactory : IDisposable
    {
        private readonly Configuration configuration;
        private LiteDatabase? database;
        private bool transactionStarted = false;

        private BsonMapper bsonMapper;

        public DatabaseFactory(Configuration configuration)
        {
            this.configuration = configuration;
            bsonMapper = new BsonMapper()
                .UseCamelCase()
                .UseLowerCaseDelimiter('-');
            bsonMapper.EmptyStringToNull = true;
            bsonMapper.EnumAsInteger = false;
            bsonMapper.TrimWhitespace = true;
            bsonMapper
                .Entity<Criteria<EpisodeCriteria>>()
                .DbRef(x => x.Criterias);
        }

        public ILiteCollection<TEntity> GetCollection<TEntity>(string? name = null)
        {
            EnsureDatabase();

            if (name is null)
                return this.database!.GetCollection<TEntity>();
            else
                return this.database!.GetCollection<TEntity>(name);
        }

        public void EnsureTransaction()
        {
            EnsureDatabase();

            if (!transactionStarted)
                transactionStarted = this.database!.BeginTrans();
        }

        public void CreateCheckpoint()
        {
            EnsureTransaction();

            this.database!.Checkpoint();
        }

        public void RollbackTransaction()
        {
            if (this.database is not null)
            {
                this.database.Rollback();
                this.transactionStarted = false;

                this.database.Dispose();
                this.database = null;
            }
        }

        public void CommitTransaction()
        {
            if (this.database is not null)
            {
                this.database.Commit();
                this.transactionStarted = false;

                this.database.Dispose();
                this.database = null;
            }
        }

        private void EnsureDatabase()
        {
            if (this.database is null)
                this.database = new LiteDatabase($"Filename={this.configuration.MapperDatabase};Upgrade=true", bsonMapper);
        }

        public void Dispose()
        {
            this.database?.Dispose();
        }
    }
}
