using System;
using System.Collections.Generic;
using System.Linq;

using Spectre.Console;

using Xabe.FFmpeg;

namespace VideoConverter.Prompts.Streams
{
	internal sealed class VideoStreamPrompt : StreamPrompt<IVideoStream>
	{
		protected override IEnumerable<int> ShowBase(IAnsiConsole console, IEnumerable<IVideoStream> streams)
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

			return console.Prompt(prompt).Select(p => p.Item1);
		}
	}
}
