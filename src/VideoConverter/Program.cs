namespace VideoConverter
{
	using System;
	using System.IO;
	using System.Text;
	using System.Threading.Tasks;
	using DryIoc;
	using Spectre.Console;
	using Spectre.Console.Cli;
	using VideoConverter.Commands;
	using VideoConverter.Core.Models;
	using VideoConverter.Core.Services;
	using VideoConverter.DependencyInjection;
	using VideoConverter.Storage.Database;
	using VideoConverter.Storage.Repositories;
	using AnsiSupport = Spectre.Console.AnsiSupport;

	internal static class Program
	{
		private static async Task<int> Main(string[] args)
		{
			Console.OutputEncoding = Encoding.UTF8;
			Console.InputEncoding = Encoding.UTF8;
			var container = CreateContainer();

			var console = container.Resolve<IAnsiConsole>();

			var registrar = new TypeRegistrar(container);

			try
			{
				var app = new CommandApp(registrar);

				app.Configure(config =>
				{
					config.AddBranch("add", add =>
					{
						config.UseStrictParsing();

						add.SetDescription("Common command collection for all things related to adding file/directory/criteria");

						add.AddCommand<AddDirectoryCommand>("directory")
							.WithAlias("directories")
							.WithAlias("dir")
							.WithAlias("dir")
							.WithAlias("d")
							.WithDescription("Adds files in the specified directory/directories to the existing queue. If they are parsable!");

						add.AddCommand<AddFileCommand>("file")
							.WithAlias("files")
							.WithAlias("f")
							.WithDescription("Adds a new file(s) to the existing queue");

						add.AddCommand<AddCriteriaCommand>("criteria")
							.WithAlias("criterias")
							.WithDescription("Adds or updates a new criteria to existing criterias.");

						add.AddCommand<AddPrefixCommand>("prefix");
					});

					config.AddCommand<EncodeCommand>("encode")
						.WithAlias("start")
						.WithDescription("Starts encoding using already queued files!");

					config.AddCommand<ConfigCommand>("config")
						.WithDescription("Shows or updates the stored user configuration!");

					config.AddBranch("queue", (queue) =>
					{
						queue.AddCommand<QueueClearCommand>("clear");
						queue.AddCommand<QueueListCommand>("list")
							.WithDescription("List files already in the queue!");
						queue.AddCommand<QueueRemoveCommand>("remove");
						queue.AddCommand<QueueResetCommand>("reset");
						queue.AddCommand<QueueShowCommand>("show");
					});
				});

				return await app.RunAsync(args).ConfigureAwait(true);
			}
			catch (Exception ex)
			{
				console.WriteException(ex);
				return 1;
			}
			finally
			{
				container.Dispose();
			}
		}

		private static IContainer CreateContainer()
		{
			var container = new Container(rules => rules.WithTrackingDisposableTransients().WithCaptureContainerDisposeStackTrace());
			var ansiSupport = Console.IsOutputRedirected ? AnsiSupport.No : AnsiSupport.Detect;
			var colorSupport = Console.IsOutputRedirected ? ColorSystemSupport.NoColors : ColorSystemSupport.Detect;

			container.RegisterDelegate(_ => AnsiConsole.Create(new AnsiConsoleSettings { Ansi = ansiSupport, ColorSystem = colorSupport }), Reuse.Singleton);
			container.RegisterDelegate(RegisterConfigurationRepository, Reuse.Singleton);
			container.RegisterDelegate(RegisterConfiguration, Reuse.Singleton);
			container.Register<DatabaseFactory>(Reuse.Singleton);
			container.Register<EpisodeCriteriaRepository>(Reuse.ScopedOrSingleton);
			container.Register<QueueRepository>(Reuse.ScopedOrSingleton);

			return container;
		}

		private static IConfigurationService RegisterConfigurationRepository(IResolverContext arg)
		{
			var configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoConverter", "config.xml");

			return new XmlConfigurationService(configPath);
		}

		private static Configuration RegisterConfiguration(IResolverContext context)
		{
			var repository = context.Resolve<IConfigurationService>();

			var config = repository.GetConfiguration();

			if (string.IsNullOrEmpty(config.MapperDatabase))

			{
				config.MapperDatabase = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoConverter", "storage.db");
				repository.SetConfiguration(config);
			}

			return config;
		}
	}
}
