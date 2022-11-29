namespace VideoConverter.Storage.IntegrationTests
{
	using NUnit.Framework;

	using VerifyTests;

	[SetUpFixture]
	internal class NUnitSetup
	{
		[OneTimeSetUp]
		public void Setup()
		{
			ClipboardAccept.Enable();
		}
	}
}
