namespace VideoConverter.Commands
{
	using System;
	using System.Collections;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Linq;
	using System.Reflection;

	using Humanizer;

	using Spectre.Console;
	using Spectre.Console.Cli;

	using VideoConverter.Core.Assertions;
	using VideoConverter.Core.Models;
	using VideoConverter.Core.Services;
	using VideoConverter.Options;

	public class ConfigCommand : Command<ConfigOption>
	{
		private readonly IAnsiConsole console;
		private readonly IConfigurationService service;

		public ConfigCommand(IConfigurationService service, IAnsiConsole console)
		{
			this.service = service.AssertAndReturnNotNull();
			this.console = console.AssertAndReturnNotNull();
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
				{
					throw new Exception("Prefixes can not be displayed at this time");
				}

				var propertyName = settings.Name.Dehumanize();

				if (string.Equals("Fansubber.Ignore", settings.Name, StringComparison.OrdinalIgnoreCase) ||
					string.Equals("Fansubber.Include", settings.Name, StringComparison.OrdinalIgnoreCase))
				{
					propertyName = nameof(ConverterConfiguration.Fansubbers);
				}

				var property = GetProperty(propertyName);

				if (property is null)
				{
					console.MarkupLine(
						"[red on black] ERROR: We could not find any configuration with the name '[fuchsia]{0}[/]'[/]",
						settings.Name
					);
					return 1;
				}

				if (string.IsNullOrEmpty(settings.Value))
				{
					console.MarkupLine(
						"[fuchsia]{0}[/] = [aqua]{1}[/]",
						property.Name.Humanize(),
						property.GetValue(config) ?? string.Empty
					);
				}
				else if (string.Equals(settings.Name, "Fansubber.Ignore", StringComparison.OrdinalIgnoreCase))
				{
					var fansubber = config.Fansubbers.Find(f => string.Equals(f.Name, settings.Value, StringComparison.Ordinal));

					if (fansubber is null)
					{
						config.Fansubbers.Add(new FansubberConfiguration
						{
							Name = settings.Value,
							IgnoreOnDuplicates = true
						});
						service.SetConfiguration(config);
						console.MarkupLine(
							"Added [aqua]{0}[/] to Ignore list",
							settings.Value);
					}
					else
					{
						console.MarkupLine("[yellow on black] WARNING: No change in configuration value. Ignoring...[/]");
					}
				}
				else if (string.Equals(settings.Name, "Fansubber.Include", StringComparison.OrdinalIgnoreCase))
				{
					var fansubber = config.Fansubbers.Find(f => string.Equals(f.Name, settings.Value, StringComparison.Ordinal));

					if (fansubber is not null)
					{
						service.SetConfiguration(config);
						fansubber.IgnoreOnDuplicates = false;
						console.MarkupLine(
							"Removed [aqua]{0}[/] from Ignore list",
							settings.Value);
					}
					else
					{
						console.MarkupLine("[yellow on black] WARNING: No change in configuration value. Ignoring...[/]");
					}
				}
				else if (string.Equals(property.GetValue(config), settings.Value))
				{
					console.MarkupLine("[yellow on black] WARNING: No change in configuration value. Ignoring...[/]");
				}
				else if (property.PropertyType == typeof(bool))
				{
					var value = bool.Parse(settings.Value);
					property.SetValue(config, value);
					service.SetConfiguration(config);
					console.WriteLine("Configuration Updated!", Style.Plain);
				}
				else if (string.Equals(
							property.Name,
							"MapperDatabase",
							StringComparison.OrdinalIgnoreCase) &&
						!string.IsNullOrEmpty(config.MapperDatabase) &&
						File.Exists(config.MapperDatabase)
				)
				{
					console.Markup(
						"[yellow on black] WARNING: We found an existing database in the previous location. " +
						"Do you want to move this to the new location? (y/N)[/]"
					);

					var key = Console.ReadKey();
					if (key.KeyChar == 'Y' || key.KeyChar == 'y')
					{
						var directory = Path.GetDirectoryName(settings.Value) ?? Environment.CurrentDirectory;
						if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
						{
							Directory.CreateDirectory(directory);
						}

						File.Move(config.MapperDatabase, settings.Value);
					}
					console.WriteLine();

					property.SetValue(config, Path.GetFullPath(settings.Value));
					service.SetConfiguration(config);
					console.WriteLine("Configuration updated!", Style.Plain);
				}
				else
				{
					property.SetValue(config, settings.Value);
					service.SetConfiguration(config);
					console.WriteLine("Configuration updated!", Style.Plain);
				}
			}
			catch (Exception ex)
			{
				console.WriteException(ex);
				return 1;
			}

			return 0;
		}

		private static PropertyInfo? GetProperty(string name)
		{
			return Array.Find(
				typeof(ConverterConfiguration).GetProperties(
					BindingFlags.Instance | BindingFlags.Public),
					p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
			);
		}

		private void ListConfigurations(ConverterConfiguration config)
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
				if (property.PropertyType.IsArray ||
					(property.PropertyType != typeof(string) && property.PropertyType.IsAssignableTo(typeof(IEnumerable))))
				{
					// For now we will ignore any lists
					continue;
				}

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
