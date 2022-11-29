namespace VideoConverter.DependencyInjection
{
	using System;
	using System.Diagnostics.CodeAnalysis;

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
			return new TypeResolver(container);
		}

		public void Register(Type service, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementation)
		{
			container.Register(service, implementation, Reuse.ScopedOrSingleton);
		}

		public void RegisterInstance(Type service, object implementation)
		{
			// We do not want to register multiple ansi consoles
			if (service == typeof(IAnsiConsole))
			{
				return;
			}

			container.RegisterInstance(service, implementation);
		}

		public void RegisterLazy(Type service, Func<object> factory)
		{
			if (service == typeof(IAnsiConsole))
			{
				return;
			}

			container.RegisterDelegate(service, (_) => factory());
		}
	}
}
