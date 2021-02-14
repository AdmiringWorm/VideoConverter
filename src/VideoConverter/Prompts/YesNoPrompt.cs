using System.Globalization;
using System.Text;
namespace VideoConverter.Prompts
{
	using System;
	using Spectre.Console;

	internal enum PromptResponse
	{
		Yes,
		No,
		Skip,
	}

	internal sealed class YesNoPrompt : IPrompt<PromptResponse>
	{
		private readonly string _prompt;


		public PromptResponse DefaultResponse { get; set; }

		public YesNoPrompt(string prompt)
		{
			_prompt = prompt;
		}

		public PromptResponse Show(IAnsiConsole console)
		{
			if (console is null)
			{
				throw new ArgumentNullException(nameof(console));
			}

			var promptStyle = Style.Plain;

			WritePrompt(console);

			while (true)
			{
				var key = console.Input.ReadKey(true);

				if (key.Key == ConsoleKey.Enter)
				{
					console.WriteLine(DefaultResponse.ToString(), promptStyle);
					return DefaultResponse;
				}

				switch (key.Key)
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
						if (key.Modifiers == ConsoleModifiers.Control)
						{
							console.WriteLine("Skip", promptStyle);
							return PromptResponse.Skip;
						}
						break;
				}
			}
		}

		private void WritePrompt(IAnsiConsole console)
		{
			if (console is null)
			{
				throw new ArgumentNullException(nameof(console));
			}

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
