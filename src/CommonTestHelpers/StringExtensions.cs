namespace VideoConverter.Tests
{
	using System.IO;
	using System.Text;

	public static class StringExtensions
	{
		public static MemoryStream ToStream(this string input)
		{
			var stream = new MemoryStream(Encoding.UTF8.GetBytes(input))
			{
				Position = 0
			};

			return stream;
		}
	}
}
