using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

using Spectre.Console;

using Xabe.FFmpeg;

namespace VideoConverter.Prompts.Streams
{
	internal sealed class SubtitleStreamPrompt : StreamPrompt<ISubtitleStream>
	{
		protected override async IAsyncEnumerable<int> ShowBaseAsync(
			IAnsiConsole console,
			IEnumerable<ISubtitleStream> streams,
			[EnumeratorCancellation] CancellationToken cancellationToken)
		{
			var prompt = new MultiSelectionPrompt<(int, string, string, string)>()
				.Title("Which subtitle streams do you wish to use ([fuchsia]Select no streams to use all streams)[/]?")
				.NotRequired()
				.AddChoices(streams.Select(MapStream));

			var values = await prompt.ShowAsync(console, cancellationToken).ConfigureAwait(false);

			foreach (var item in values)
			{
				yield return item.Item1;
			}
		}

		private (int, string, string, string) MapStream(ISubtitleStream stream)
		{
			CultureInfo? ci = null;
			try
			{
				ci = new CultureInfo(stream.Language);
			}
			catch
			{
				// Ignore any execption on purpose
			}

			return (
				stream.Index,
				ci?.EnglishName ?? stream.Language,
				stream.Title.EscapeMarkup(),
				stream.Codec
			);
		}
	}
}
