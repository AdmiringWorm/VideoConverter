namespace VideoConverter.Commands
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using Humanizer;
	using Spectre.Console.Cli;
	using Spectre.Console;
	using VideoConverter.Core.Services;
	using VideoConverter.Options;
	using System.Diagnostics.CodeAnalysis;
	using VideoConverter.Core.Models;

	public class ConfigCommand : Command<ConfigOption>
	{
		private readonly IConfigurationService service;
		private readonly IAnsiConsole console;

		public ConfigCommand(IConfigurationService service, IAnsiConsole console)
		{
			this.service = service ?? throw new ArgumentNullException(nameof(service));
			this.console = console ?? throw new ArgumentNullException(nameof(console));
		}

		public override int Execute([NotNull] CommandContext context, [NotNull] ConfigOption settings)
		{
			try
			{
				var config = service.GetConfiguration();

				if (string.IsNullOrWhiteSpace(settings.Name))
				{
					ListConfigurations(config);
					return 0;
				}
				if (settings.Name == "Prefixes")
					throw new Exception("Prefixes can not be displayed at this time");

				var property = GetProperty(settings.Name.Dehumanize());

				if (property is null)
				{
					this.console.MarkupLine(
						"[red on black] ERROR: We could not find any configuration with the name '[fuchsia]{0}[/]'[/]",
						settings.Name
					);
					return 1;
				}

				if (string.IsNullOrEmpty(settings.Value))
				{
					this.console.MarkupLine(
						"[fuchsia]{0}[/] = [aqua]{1}[/]",
						property.Name.Humanize(),
						property.GetValue(config) ?? string.Empty
					);
				}
				else if (string.Equals(property.GetValue(config), settings.Value))
				{
					this.console.MarkupLine("[yellow on black] WARNING: No change in configuration value. Ignoring...[/]");
				}
				else if (property.PropertyType == typeof(bool))
				{
					var value = bool.Parse(settings.Value);
					property.SetValue(config, value);
					service.SetConfiguration(config);
					this.console.WriteLine("Configuration Updated!", Style.Plain);
				}
				else if (string.Equals(
							property.Name,
							"MapperDatabase",
							StringComparison.OrdinalIgnoreCase) &&
						!string.IsNullOrEmpty(config.MapperDatabase) &&
						File.Exists(config.MapperDatabase)
				)
				{
					this.console.Markup(
						"[yellow on black] WARNING: We found an existing database in the previous location. " +
						"Do you want to move this to the new location? (y/N)[/]"
					);

					var key = Console.ReadKey();
					if (key.KeyChar == 'Y' || key.KeyChar == 'y')
					{
						var directory = Path.GetDirectoryName(settings.Value) ?? Environment.CurrentDirectory;
						if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
							Directory.CreateDirectory(directory);
						File.Move(config.MapperDatabase, settings.Value);
					}
					this.console.WriteLine();

					property.SetValue(config, Path.GetFullPath(settings.Value));
					service.SetConfiguration(config);
					this.console.WriteLine("Configuration updated!", Style.Plain);
				}
				else
				{
					property.SetValue(config, settings.Value);
					service.SetConfiguration(config);
					this.console.WriteLine("Configuration updated!", Style.Plain);
				}
			}
			catch (Exception ex)
			{
				this.console.WriteException(ex);
				return 1;
			}

			return 0;
		}

		private static PropertyInfo? GetProperty(string name)
		{
			return Array.Find(
				typeof(Configuration).GetProperties(
					BindingFlags.Instance | BindingFlags.Public),
					p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
			);
		}

		private void ListConfigurations(Configuration config)
		{
			var table = new Table()
				.BorderStyle(new Style(Color.Olive))
				.DoubleEdgeBorder()
				.AddColumns(new TableColumn("Name").RightAligned(), new TableColumn("Value").LeftAligned());

			foreach (var property in
				config
					.GetType()
					.GetProperties(
						BindingFlags.Instance | BindingFlags.Public)
					.Where(p => p.Name != "Prefixes")
			)
			{
				var name = property.Name;
				var value = property.GetValue(config);
				table.AddRow(
					$"[fuchsia]{name.Humanize()}[/]",
					$"[aqua]{value}[/]"
				);
			}

			var panel = new Panel(table)
				.NoBorder();
			panel.Header = new PanelHeader("[aqua]Available Configuations[/]", Justify.Center);

			console.Write(panel);
		}
	}
}
