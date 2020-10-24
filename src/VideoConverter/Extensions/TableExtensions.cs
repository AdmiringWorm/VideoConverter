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
            => table.SetBorderColor(Color.Silver).DoubleEdgeBorder();

        public static Table AddColorRow(this Table table, params object?[] columns)
        {
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

            return new Text(status.ToString()!, Style.WithForeground(color));
        }

        public static Text GetAnsiText(this object? value)
        {
            if (value is null)
                return new Text(string.Empty, Style.Plain);
            if (value is QueueStatus qs)
                return qs.GetAnsiText();

            var (textValue, color) = value switch
            {
                int i => (i.ToString()!, Color.Aqua),
                long l => (l.ToString()!, Color.Aqua),
                TimeSpan ts => (ts.Humanize(3, countEmptyUnits: true), Color.Fuchsia),
                EpisodeData ed => (ed.ToString()!, Color.DarkCyan),
                _ => (value.ToString()!, Color.Fuchsia)
            };

            return new Text(textValue, Style.WithForeground(color));
        }

        public static Text GetAnsiText(this object? value, Color color)
        {
            if (value is null)
                return new Text(string.Empty, Style.Plain);

            return new Text(value.ToString()!, Style.WithForeground(color));
        }

        public static void RenderTable(this IAnsiConsole console, Table table, string header)
        {
            var panel = new Panel(table)
                .SetHeader(header, Style.WithForeground(Color.Teal).WithDecoration(Decoration.Bold | Decoration.Underline), Justify.Center)
                .NoBorder();

            console.Render(panel);
        }
    }
}
