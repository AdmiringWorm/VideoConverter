namespace VideoConverter.Storage.Database
{
	using System;
	using System.Threading.Tasks;
	using LiteDB;
	using LiteDB.Async;
	using VideoConverter.Storage.Models;
	using Configuration = VideoConverter.Core.Models.Configuration;

	public sealed class DatabaseFactory : IDisposable
	{
		private readonly Configuration configuration;
		private LiteDatabaseAsync? database;
		private bool transactionStarted;

		private readonly BsonMapper bsonMapper;

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

		public ILiteCollectionAsync<TEntity> GetCollection<TEntity>(string? name = null)
		{
			EnsureDatabase();

			if (name is null)
				return this.database!.GetCollection<TEntity>();
			else
				return this.database!.GetCollection<TEntity>(name);
		}

		public async Task EnsureTransactionAsync()
		{
			EnsureDatabase();

			if (!transactionStarted)
				transactionStarted = await this.database!.BeginTransAsync().ConfigureAwait(false);
		}

		public async Task CreateCheckpointAsync()
		{
			await EnsureTransactionAsync().ConfigureAwait(false);

			await this.database!.CheckpointAsync().ConfigureAwait(false);
		}

		public async Task RollbackTransactionAsync()
		{
			if (this.database is not null)
			{
				await this.database.RollbackAsync().ConfigureAwait(false);
				this.transactionStarted = false;
			}
		}

		public async Task CommitTransactionAsync()
		{
			if (this.database is not null)
			{
				await this.database.CommitAsync().ConfigureAwait(false);
				this.transactionStarted = false;
			}
		}

		private void EnsureDatabase()
		{
			if (this.database is null)
				this.database = new LiteDatabaseAsync($"Filename={this.configuration.MapperDatabase};Connection=shared;Upgrade=true", bsonMapper);
		}

		public void Dispose()
		{
			this.database?.Dispose();
		}
	}
}
