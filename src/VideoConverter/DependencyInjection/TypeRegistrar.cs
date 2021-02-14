namespace VideoConverter.DependencyInjection
{
	using System;
	using DryIoc;
	using Spectre.Console;
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
			// We do not want to register multiple ansi consoles
			if (service == typeof(IAnsiConsole))
				return;

			this.container.RegisterInstance(service, implementation);
		}
	}
}
