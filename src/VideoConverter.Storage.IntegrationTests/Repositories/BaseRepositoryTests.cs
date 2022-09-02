namespace VideoConverter.Storage.IntegrationTests.Repositories
{
	using System;
	using System.Threading.Tasks;

	using NUnit.Framework;

	using VideoConverter.Storage.Database;
	using VideoConverter.Storage.Tests;

	public abstract class BaseRepositoryTests<TRepository>
	{
		protected DatabaseFactory factory;
		protected TRepository repository;

		[SetUp]
		public async Task TestSetup()
		{
			factory = await DatabaseFactoryHelper.CreateTestFactory();

			repository = CreateRepository(factory);
		}

		[TearDown]
		public async Task TestTeardown()
		{
			await DatabaseFactoryHelper.CleanTestFactory(factory);

			if (repository is IDisposable disposable)
			{
				disposable.Dispose();
			}
		}

		protected abstract TRepository CreateRepository(DatabaseFactory factory);
	}
}
