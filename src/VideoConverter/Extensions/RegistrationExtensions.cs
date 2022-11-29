namespace VideoConverter.Extensions
{
	using System.Diagnostics.CodeAnalysis;

	using DryIoc;

	internal static class RegistrationExtensions
	{
		public static void RegisterScoped<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(
			this IRegistrator registrator)
			where TService : class
		{
			registrator.Register<TService>(Reuse.ScopedOrSingleton);
		}

		public static void RegisterSingleton<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(
			this IRegistrator registrator)
			where TService : class
		{
			registrator.Register<TService>(Reuse.Singleton);
		}

		public static void RegisterSingleton<
			TService,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(
			this IRegistrator registrator)
			where TImplementation : class, TService
		{
			registrator.Register<TService, TImplementation>(Reuse.Singleton);
		}

		public static void RegisterTransient<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(
			this IRegistrator registrator)
			where TService : class
		{
			registrator.Register<TService>(Reuse.Transient);
		}
	}
}
