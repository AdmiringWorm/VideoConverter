using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Spectre.Console;
using Xabe.FFmpeg;

namespace VideoConverter.Prompts
{
	internal abstract class StreamPrompt<T> : IPrompt<IEnumerable<T>>
	where T : IStream
	{
		private readonly List<T> streams = new();

		public IEnumerable<T> Show(IAnsiConsole console)
		{
			var indexes = ShowBase(console, streams);

			if (indexes.Any())
			{
				return streams.Where(s => indexes.Contains(s.Index));
			}
			else
			{
				return streams;
			}
		}

		public StreamPrompt<T> AddStreams(IEnumerable<T> streams)
		{
			this.streams.AddRange(streams);
			return this;
		}

		protected abstract IEnumerable<int> ShowBase(IAnsiConsole console, IEnumerable<T> streams);
	}

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

	internal sealed class SubtitleStreamPrompt : StreamPrompt<ISubtitleStream>
	{
		protected override IEnumerable<int> ShowBase(IAnsiConsole console, IEnumerable<ISubtitleStream> streams)
		{
			var prompt = new MultiSelectionPrompt<(int, string, string, string)>()
				.Title("Which subtitle streams do you wish to use ([fuchsia]Select no streams to use all streams)[/]?")
				.NotRequired()
				.AddChoices(streams.Select(MapStream));

			return console.Prompt(prompt).Select(p => p.Item1);
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
