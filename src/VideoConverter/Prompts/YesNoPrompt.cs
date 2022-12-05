using System.Globalization;
using System.Text;

namespace VideoConverter.Prompts
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	using Spectre.Console;

	internal sealed class YesNoPrompt : IPrompt<PromptResponse>
	{
		private readonly string _prompt;

		public YesNoPrompt(string prompt)
		{
			_prompt = prompt;
		}

		public PromptResponse DefaultResponse { get; set; }

		public PromptResponse Show(IAnsiConsole console)
		{
			throw new NotSupportedException("Sync calls are not supported!");
		}

		public async Task<PromptResponse> ShowAsync(IAnsiConsole console, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(console);

			var promptStyle = Style.Plain;

			WritePrompt(console);

			while (true)
			{
				var key = await console.Input.ReadKeyAsync(intercept: true, cancellationToken).ConfigureAwait(false);

				if (!key.HasValue)
				{
					continue;
				}

				switch (key.Value.Key)
				{
					case ConsoleKey.Y:
						console.WriteLine("Yes", promptStyle);
						return PromptResponse.Yes;

					case ConsoleKey.N:
						console.WriteLine("No", promptStyle);
						return PromptResponse.No;

					case ConsoleKey.S:
						console.WriteLine("Skip", promptStyle);
						return PromptResponse.Skip;

					case ConsoleKey.C:
						if (key.Value.Modifiers == ConsoleModifiers.Control)
						{
							console.WriteLine("Skip", promptStyle);
							return PromptResponse.Skip;
						}

						break;

					case ConsoleKey.Enter:
						console.WriteLine(DefaultResponse.ToString(), promptStyle);
						return DefaultResponse;
				}
			}
		}

		private void WritePrompt(IAnsiConsole console)
		{
			ArgumentNullException.ThrowIfNull(console);

			var builder = new StringBuilder();
			builder.Append(_prompt.TrimEnd());

			builder.AppendFormat(
				CultureInfo.CurrentCulture,
				"[blue][[{0}]]/[[{1}]]/[[{2}]][/]",
				DefaultResponse == PromptResponse.Yes ? "[green]Yes[/]" : "yes",
				DefaultResponse == PromptResponse.No ? "[green]No[/]" : "no",
				DefaultResponse == PromptResponse.Skip ? "[green]Skip[/]" : "skip"
			);

			var markup = builder.ToString().Trim();
			if (!markup.EndsWith("?", StringComparison.OrdinalIgnoreCase) &&
				!markup.EndsWith(":", StringComparison.OrdinalIgnoreCase))
			{
				markup += ":";
			}

			console.Markup(markup + " ");
		}
	}
}
