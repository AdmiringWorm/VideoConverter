using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Spectre.Console;

using Xabe.FFmpeg;

namespace VideoConverter.Prompts.Streams
{
	internal sealed class AudioStreamPrompt : StreamPrompt<IAudioStream>
	{
		protected override IEnumerable<int> ShowBase(IAnsiConsole console, IEnumerable<IAudioStream> streams)
		{
			var prompt = new MultiSelectionPrompt<(int, string, string, int, string)>()
				.Title("Which audio streams do you wish to use ([fuchsia]Select no streams to use all streams)[/]?")
				.NotRequired()
				.AddChoices(streams.Select(MapStream));

			return console.Prompt(prompt).Select(p => p.Item1);
		}

		private (int, string, string, int, string) MapStream(IAudioStream stream)
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
				stream.Codec,
				stream.Channels,
				stream.SampleRate + " Hz"
			);
		}
	}
}
