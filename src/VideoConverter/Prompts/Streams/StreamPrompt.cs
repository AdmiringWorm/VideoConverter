using System;
using System.Collections.Generic;
using System.Linq;

using Spectre.Console;

using Xabe.FFmpeg;

namespace VideoConverter.Prompts.Streams
{
	internal abstract class StreamPrompt<T> : IPrompt<IEnumerable<T>>
		where T : IStream
	{
		private readonly List<T> streams = new();

		public StreamPrompt<T> AddStreams(IEnumerable<T> streams)
		{
			this.streams.AddRange(streams);
			return this;
		}

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

		protected abstract IEnumerable<int> ShowBase(IAnsiConsole console, IEnumerable<T> streams);
	}
}
