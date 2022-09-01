using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
			throw new NotSupportedException("Synchron calling is not supported!");
		}

		public async Task<IEnumerable<T>> ShowAsync(IAnsiConsole console, CancellationToken cancellationToken)
		{
			var streamDictionary = streams.ToDictionary(
				k => k.Index,
				v => v);

			var result = new List<T>();

			await foreach (var index in ShowBaseAsync(console, streams, cancellationToken))
			{
				if (streamDictionary.ContainsKey(index))
				{
					result.Add(streamDictionary[index]);
				}
			}

			return result;
		}

		protected abstract IAsyncEnumerable<int> ShowBaseAsync(
			IAnsiConsole console,
			IEnumerable<T> streams,
			CancellationToken cancellationToken);
	}
}
