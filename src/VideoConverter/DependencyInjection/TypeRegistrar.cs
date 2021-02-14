namespace VideoConverter.DependencyInjection
{
	using System;
	using DryIoc;
	using Spectre.Console.Cli;

	public class TypeRegistrar : ITypeRegistrar
	{
		private readonly IContainer container;

		public TypeRegistrar(IContainer container)
		{
			this.container = container;
		}

		public ITypeResolver Build()
		{
			return new TypeResolver(this.container);
		}

		public void Register(Type service, Type implementation)
		{
			this.container.Register(service, implementation, Reuse.ScopedOrSingleton);
		}

		public void RegisterInstance(Type service, object implementation)
		{
			this.container.RegisterInstance(service, implementation);
		}
	}
}
