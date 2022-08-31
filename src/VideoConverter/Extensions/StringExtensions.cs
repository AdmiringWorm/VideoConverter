namespace VideoConverter.Extensions
{
	using System;

	internal static class StringExtensions
	{
		/// <summary>
		/// Does an incasive contains check by passing in <see cref="StringComparison.Ordinal"/>.
		/// </summary>
		/// <param name="source">The source to check for a substring</param>
		/// <param name="value">The value to use as the substring.</param>
		/// <returns>
		/// <c>True</c> if the <paramref name="source"/> contains the substring specified as the
		/// <paramref name="value"/>; Otherwise <c>false</c>.
		/// </returns>
		public static bool ContainsExact(this string source, string value)
		{
			return source.Contains(value, StringComparison.Ordinal);
		}

		/// <summary>
		/// Does an incasive contains check by passing in <see cref="StringComparison.OrdinalIgnoreCase"/>.
		/// </summary>
		/// <param name="source">The source to check for a substring</param>
		/// <param name="value">The value to use as the substring.</param>
		/// <returns>
		/// <c>True</c> if the <paramref name="source"/> contains the substring specified as the
		/// <paramref name="value"/>; Otherwise <c>false</c>.
		/// </returns>
		public static bool ContainsInvariant(this string source, string value)
		{
			return source.Contains(value, StringComparison.OrdinalIgnoreCase);
		}
	}
}
