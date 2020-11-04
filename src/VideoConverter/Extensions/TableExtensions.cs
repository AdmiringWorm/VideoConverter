using System.Globalization;
using System.Drawing;
namespace VideoConverter.Extensions
{
	using System;
	using System.Collections.Generic;
	using Humanizer;
	using Spectre.Console;
	using VideoConverter.Core.Models;
	using VideoConverter.Storage.Models;

	public static class TableExtensions
	{
		public static Table SetDefaults(this Table table)
			=> table
				.BorderColor(Color.Silver)
				.DoubleEdgeBorder();

		public static Table AddColorRow(this Table table, params object?[] columns)
		{
			if (table is null)
				throw new ArgumentNullException(nameof(table));

			var coloredColumns = new List<Text>();

			foreach (var column in columns)
			{
				coloredColumns.Add(column.GetAnsiText());
			}

			return table.AddRow(coloredColumns.ToArray());
		}

		public static Text GetAnsiText(this QueueStatus status)
		{
			var color = status switch
			{
				QueueStatus.Completed => Color.Grey,
				QueueStatus.Failed => Color.DarkRed,
				QueueStatus.Encoding => Color.Teal,
				_ => Color.Olive,
			};

			return new Text(status.ToString()!, new Style(color));
		}

		public static Text GetAnsiText(this object? value)
		{
			if (value is null)
				return new Text(string.Empty, Style.Plain);
			if (value is QueueStatus qs)
				return qs.GetAnsiText();

			var (textValue, color) = value switch
			{
				int i => (i.ToString(CultureInfo.CurrentCulture)!, Color.Aqua),
				long l => (l.ToString(CultureInfo.CurrentCulture)!, Color.Aqua),
				TimeSpan ts => (ts.Humanize(3, countEmptyUnits: true), Color.Fuchsia),
				EpisodeData ed => (ed.ToString()!, Color.DarkCyan),
				_ => (value.ToString()!, Color.Fuchsia)
			};

			return new Text(textValue, new Style(color));
		}

		public static string GetAnsiTextString(this QueueStatus status)
		{
			return status switch
			{
				QueueStatus.Completed => $"[grey]{status}[/]",
				QueueStatus.Failed => $"[darkred]{status}[/]",
				QueueStatus.Encoding => $"[teal]{status}[/]",
				_ => $"[olive]{status}[/]",
			};
		}

		public static Text GetAnsiText(this object? value, Color color)
		{
			if (value is null)
				return new Text(string.Empty);

			return new Text(value.ToString()!, new Style(color));
		}

		public static void RenderTable(this IAnsiConsole console, Table table, string header)
		{
			var panel = new Panel(table)
				.Header(header, new Style(Color.Teal, decoration: Decoration.Bold | Decoration.Underline), Justify.Center)
				.NoBorder();

			console.Render(panel);
		}
	}
}
