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
		private readonly BsonMapper bsonMapper;
		private readonly Configuration configuration;
		private LiteDatabaseAsync? database;
		private bool transactionStarted;

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

		public async Task CommitTransactionAsync()
		{
			if (database is null)
			{
				return;
			}

			await database.CommitAsync().ConfigureAwait(false);
			transactionStarted = false;
		}

		public async Task CreateCheckpointAsync()
		{
			await EnsureTransactionAsync().ConfigureAwait(false);

			await database!.CheckpointAsync().ConfigureAwait(false);
		}

		public void Dispose()
		{
			database?.Dispose();
		}

		public async Task EnsureTransactionAsync()
		{
			EnsureDatabase();

			if (transactionStarted)
			{
				return;
			}

			transactionStarted = await database!.BeginTransAsync().ConfigureAwait(false);
		}

		public ILiteCollectionAsync<TEntity> GetCollection<TEntity>(string? name = null)
		{
			EnsureDatabase();

			if (name is null)
			{
				return database!.GetCollection<TEntity>();
			}
			else
			{
				return database!.GetCollection<TEntity>(name);
			}
		}

		public async Task RollbackTransactionAsync()
		{
			if (database is null)
			{
				return;
			}

			await database.RollbackAsync().ConfigureAwait(false);
			transactionStarted = false;
		}

		private void EnsureDatabase()
		{
			if (database is not null)
			{
				return;
			}

			database = new LiteDatabaseAsync(
				$"Filename={configuration.MapperDatabase};Connection=shared;Upgrade=true", bsonMapper
			);
		}
	}
}
