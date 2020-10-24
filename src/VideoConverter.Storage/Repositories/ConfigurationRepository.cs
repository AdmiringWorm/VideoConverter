namespace VideoConverter.Storage.Repositories
{
    using System.Text;
    using System.IO;
    using System.Xml.Serialization;
    using System;
    using VideoConverter.Storage.Models;

    public class ConfigurationRepository
    {
        private readonly string configPath;

        public ConfigurationRepository(string configurationPath)
        {
            this.configPath = configurationPath ?? throw new ArgumentNullException(nameof(configurationPath));
        }

        public Configuration GetConfiguration()
        {
            if (!File.Exists(configPath))
                return new Configuration();

            using var reader = new StreamReader(configPath, Encoding.UTF8);


            var serializer = new XmlSerializer(typeof(Configuration));

            return (serializer.Deserialize(reader) as Configuration) ?? new Configuration();
        }

        public void SaveConfiguration(Configuration configuration)
        {
            var directory = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using var writer = new StreamWriter(configPath, false, Encoding.UTF8);

            var serializer = new XmlSerializer(configuration.GetType());

            serializer.Serialize(writer, configuration);
        }
    }
}
