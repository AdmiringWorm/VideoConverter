namespace VideoConverter.Core.Assertions
{
	using System;
	using System.Diagnostics.CodeAnalysis;
	using System.Runtime.CompilerServices;

	public static class AssertionExtensions
	{
		public static TValue AssertAndReturnNotNull<TValue>(
			[NotNull] this TValue value,
			[CallerArgumentExpression("memberName")] string? memberName = default)
		{
			AssertNotNull(value, memberName);

			return value;
		}

		public static void AssertNotEmpty(
							[NotNull] this string value,
			[CallerArgumentExpression("memberName")] string? memberName = default)
		{
			AssertNotNull(value, memberName);

			if (!string.IsNullOrEmpty(value))
			{
				return;
			}

			throw new ArgumentOutOfRangeException(memberName);
		}

		public static void AssertNotNull<TValue>(
							[NotNull] this TValue value,
			[CallerArgumentExpression("memberName")] string? memberName = default)
		{
			if (value is not null)
			{
				return;
			}

			throw new ArgumentNullException(memberName);
		}

		public static void AssertNotWhitespace(
			[NotNull] this string value,
			[CallerArgumentExpression("memberName")] string? memberName = default)
		{
			AssertNotNull(value, memberName);

			if (!string.IsNullOrWhiteSpace(value))
			{
				return;
			}

			throw new ArgumentOutOfRangeException(memberName);
		}
	}
}
