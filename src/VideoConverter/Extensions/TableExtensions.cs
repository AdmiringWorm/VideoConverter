namespace VideoConverter.Extensions
{
	using System;
	using System.Globalization;
	using Humanizer;
	using Spectre.Console;
	using VideoConverter.Core.Models;
	using VideoConverter.Storage.Models;

	public static class TableExtensions
	{
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
				.NoBorder();
			panel.Header = new PanelHeader("[teal bold underline]" + header + "[/]", Justify.Center);

			console.Write(panel);
		}
	}
}
