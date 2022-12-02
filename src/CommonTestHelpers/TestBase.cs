namespace VideoConverter.Tests
{
	using System;

	using DryIoc;

	using Moq;

	using NUnit.Framework;

	public abstract class TestBase<TService> : TestBase
	{
		protected TService Service;

		public TestBase()
					: base()
		{
		}

		public TestBase(IContainer productionContainer)
			: base(productionContainer)
		{
		}

		[TearDown]
		public void DisposeService()
		{
			if (Service is IDisposable disposable)
			{
				disposable.Dispose();
			}
		}

		protected override void SetupContainer(IContainer container)
		{
			Service = Resolve<TService>();
		}
	}

	public abstract class TestBase
	{
		private readonly bool disposeProductionContainer = false;
		private IContainer container;
		private IContainer productionContainer;
		private IResolver scopeResolver;

		public TestBase()
			: this(new Container())
		{
			disposeProductionContainer = true;
		}

		public TestBase(IContainer productionContainer)
		{
			this.productionContainer = productionContainer;
		}

		[SetUp]
		public void CreateContainer()
		{
			container = CreateTestContainer(productionContainer);
			SetupContainer(container);

			scopeResolver = container.OpenScope("Test Scope");
		}

		[OneTimeTearDown]
		public void DisposeProductionContainer()
		{
			if (disposeProductionContainer)
			{
				productionContainer.Dispose();
				productionContainer = null;
			}
		}

		[TearDown]
		public void DisposeTestContainer()
		{
			if (scopeResolver is IDisposable disposable)
			{
				disposable.Dispose();
			}
			scopeResolver = null;

			container?.Dispose();
			container = null;
		}

		protected Mock<TService> Mock<TService>()
			where TService : class
		{
			var service = Resolve<Mock<TService>>();
			container.RegisterInstance(service.Object);

			return service;
		}

		protected void Register<TService>(IReuse reuse = null)
			=> (container ?? productionContainer).Register<TService>(reuse ?? Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Replace);

		protected TService Resolve<TService>()
			=> (scopeResolver ?? container ?? productionContainer).Resolve<TService>();

		protected TService Resolve<TService>(params object[] args)
			=> (scopeResolver ?? container ?? productionContainer).Resolve<TService>(args);

		protected virtual void SetupContainer(IContainer container)
		{
		}

		private static IContainer CreateTestContainer(IContainer container)
		{
			var containerChild = container.CreateChild(IfAlreadyRegistered.Replace,
				container.Rules.WithDynamicRegistration((serviceType, serviceKey) =>
				{
					// Ignore services with non-default key
					if (serviceKey is not null)
					{
						return null;
					}

					if (serviceType == typeof(object))
					{
						return null;
					}

					// Get the Mock object for the abstract class or interface
					if (serviceType.IsInterface || serviceType.IsAbstract)
					{
						// Except for the open-generic ones
						if (serviceType.IsGenericType && serviceType.IsOpenGeneric())
						{
							return null;
						}

						var mockType = typeof(Mock<>).MakeGenericType(serviceType);

						var mockFactory = DelegateFactory.Of(r => ((Mock)r.Resolve(mockType)).Object, Reuse.Singleton);

						return new[] { new DynamicRegistration(mockFactory, IfAlreadyRegistered.Keep) };
					}

					// Concrete types
					var concreteTypeFactory = serviceType.ToFactory(Reuse.Singleton, FactoryMethod.ConstructorWithResolvableArgumentsIncludingNonPublic);

					return new[] { new DynamicRegistration(concreteTypeFactory) };
				},
				DynamicRegistrationFlags.Service | DynamicRegistrationFlags.AsFallback));

			containerChild.Register(typeof(Mock<>), Reuse.Singleton, FactoryMethod.DefaultConstructor());

			return containerChild;
		}
	}
}
