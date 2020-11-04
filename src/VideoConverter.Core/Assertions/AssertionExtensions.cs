using System;
namespace VideoConverter.Core.Assertions
{
	using System.Runtime.CompilerServices;
	public static class AssertionExtensions
	{
		public static TValue IsNotNull<TValue>(this TValue value, [CallerArgumentExpression("memberName")] string? memberName = default)
		{
			if (value is null)
				throw new ArgumentNullException(memberName);

			return value;
		}

		public static string IsNotEmpty(this string value, [CallerArgumentExpression("memberName")] string? memberName = default)
		{
			if (string.IsNullOrEmpty(value))
				throw new ArgumentOutOfRangeException(memberName);

			return value;
		}

		public static string IsNotWhitespace(this string value, [CallerArgumentExpression("memberName")] string? memberName = default)
		{
			if (string.IsNullOrWhiteSpace(value))
				throw new ArgumentOutOfRangeException(memberName);

			return value;
		}
	}
}
