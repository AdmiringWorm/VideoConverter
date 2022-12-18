namespace VideoConverter.Core.Services
{
	using System;
	using System.IO;
	using System.Text;
	using System.Xml;
	using System.Xml.Serialization;

	using VideoConverter.Core.IO;
	using VideoConverter.Core.Models;

	public sealed class XmlConfigurationService : IConfigurationService, IDisposable
	{
		private static readonly Encoding configEncoding = new UTF8Encoding(false, true);

		private readonly string configPath;
		private readonly IIOHelpers ioHelpers;
		private readonly FileSystemWatcher watcher;
		private ConverterConfiguration? config;

		public XmlConfigurationService(string configPath, IIOHelpers ioHelpers)
		{
			this.configPath = configPath;

			var dir = Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory;
			var name = Path.GetFileName(configPath);
			ioHelpers.EnsureDirectory(dir);

			watcher = new FileSystemWatcher(dir, name);
			watcher.Changed += OnFileChanged;
			watcher.NotifyFilter = NotifyFilters.LastWrite;
			this.ioHelpers = ioHelpers;
		}

		public void Dispose()
		{
			watcher.Dispose();
		}

		public ConverterConfiguration GetConfiguration()
		{
			if (config is not null)
			{
				return config;
			}

			config = new ConverterConfiguration();

			if (!ioHelpers.FileExists(configPath))
			{
				config.MapperDatabase = GetMapperDatabase();
			}
			else
			{
				UpdateConfiguration(config);
			}
			watcher.EnableRaisingEvents = true;

			return config;
		}

		public void SetConfiguration(ConverterConfiguration config)
		{
			watcher.EnableRaisingEvents = false;

			var directory = Path.GetDirectoryName(configPath);
			ioHelpers.EnsureDirectory(directory);

			ioHelpers.FileRemove(configPath);
			using var writer = ioHelpers.FileOpenWrite(configPath);
			using var xmlWriter = XmlWriter.Create(writer, new XmlWriterSettings
			{
				Indent = true,
				Encoding = configEncoding
			});

			var serializer = new XmlSerializer(typeof(ConverterConfiguration));

			serializer.Serialize(xmlWriter, config);

			this.config = config;

			watcher.EnableRaisingEvents = true;
		}

		private static string GetMapperDatabase()
		{
			return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"VideoConverter",
				"storage.db"
			);
		}

		private void OnFileChanged(object sender, FileSystemEventArgs e)
		{
			if (e.ChangeType != WatcherChangeTypes.Changed || config is null)
			{
				return;
			}

			UpdateConfiguration(config);
		}

		private void UpdateConfiguration(ConverterConfiguration config)
		{
			try
			{
				using var reader = ioHelpers.FileOpenRead(configPath);
				using var xmlReader = XmlReader.Create(reader);
				var serializer = new XmlSerializer(typeof(ConverterConfiguration));

				var tempConfig = serializer.Deserialize(xmlReader) as ConverterConfiguration ?? new ConverterConfiguration();

				config.AudioCodec = tempConfig.AudioCodec;
				config.FileType = tempConfig.FileType;
				config.IncludeFansubber = tempConfig.IncludeFansubber;
				config.MapperDatabase = tempConfig.MapperDatabase ?? GetMapperDatabase();
				config.Prefixes = tempConfig.Prefixes;
				config.SubtitleCodec = tempConfig.SubtitleCodec;
				config.VideoCodec = tempConfig.VideoCodec;
				config.WorkDirectory = tempConfig.WorkDirectory;
				config.Fansubbers = tempConfig.Fansubbers;
			}
			catch
			{
			}
		}
	}
}
