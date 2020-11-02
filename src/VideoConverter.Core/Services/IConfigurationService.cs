namespace VideoConverter.Core.Services
{
    using VideoConverter.Core.Models;

    public interface IConfigurationService
    {
        Configuration GetConfiguration();

        void SetConfiguration(Configuration config);
    }
}
