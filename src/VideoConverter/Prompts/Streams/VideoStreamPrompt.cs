using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

using Spectre.Console;

using Xabe.FFmpeg;

namespace VideoConverter.Prompts.Streams
{
	internal sealed class VideoStreamPrompt : StreamPrompt<IVideoStream>
	{
		protected override async IAsyncEnumerable<int> ShowBaseAsync(
			IAnsiConsole console,
			IEnumerable<IVideoStream> streams,
			[EnumeratorCancellation] CancellationToken cancellationToken)
		{
			var prompt = new MultiSelectionPrompt<(int, string, string, TimeSpan)>()
				.Title("Which video streams do you wish to use ([fuchsia] Select no streams to use all streams)[/]?")
				.NotRequired()
				.AddChoices(streams.Select(
					s => (
						s.Index,
						s.Codec.EscapeMarkup(),
						s.Framerate + " fps",
						s.Duration
					)));

			var values = await prompt.ShowAsync(console, cancellationToken).ConfigureAwait(false);

			foreach (var item in values)
			{
				yield return item.Item1;
			}
		}
	}
}
