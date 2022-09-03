namespace VideoConverter.Core.Services
{
	using VideoConverter.Core.Models;

	public interface IConfigurationService
	{
		ConverterConfiguration GetConfiguration();

		void SetConfiguration(ConverterConfiguration config);
	}
}
