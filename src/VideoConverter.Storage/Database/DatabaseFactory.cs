namespace VideoConverter.Storage.Database
{
	using System;
	using System.Diagnostics.CodeAnalysis;
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
		private ILiteDatabaseAsync? transaction;

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
			if (transaction is not null)
			{
				await transaction.CommitAsync().ConfigureAwait(false);

				transaction.Dispose();
				transaction = null;
			}

			if (database is null)
			{
				return;
			}

			await database.CommitAsync().ConfigureAwait(false);
			database.Dispose();
			database = null;
		}

		public async Task CreateCheckpointAsync()
		{
			await EnsureTransactionAsync().ConfigureAwait(false);

			await transaction.CheckpointAsync().ConfigureAwait(false);
		}

		public void Dispose()
		{
			transaction?.Dispose();
			transaction = null;

			database?.Dispose();
			database = null;
		}

		[MemberNotNull(nameof(transaction))]
		public async Task EnsureTransactionAsync()
		{
			EnsureDatabase();

			if (transaction is not null)
			{
				return;
			}

#pragma warning disable CS8774 // Member must have a non-null value when exiting.
			var startedTransaction = await database.BeginTransactionAsync().ConfigureAwait(false);
#pragma warning restore CS8774 // Member must have a non-null value when exiting.

			if (startedTransaction is null)
			{
				throw new ApplicationException("Unable to create the transaction");
			}

			transaction = startedTransaction;
		}

		public ILiteCollectionAsync<TEntity> GetCollection<TEntity>(string? name = null)
		{
			EnsureDatabase();

			if (name is null && transaction is not null)
			{
				return transaction.GetCollection<TEntity>();
			}
			else
			{
				return database.GetCollection<TEntity>(name);
			}
		}

		public async Task RollbackTransactionAsync()
		{
			if (database is null)
			{
				return;
			}

			if (transaction is not null)
			{
				await transaction.RollbackAsync().ConfigureAwait(false);
				transaction.Dispose();
				transaction = null;
			}
			else
			{
				await database.RollbackAsync().ConfigureAwait(false);
				database.Dispose();
				database = null;
			}
		}

		[MemberNotNull(nameof(database))]
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
