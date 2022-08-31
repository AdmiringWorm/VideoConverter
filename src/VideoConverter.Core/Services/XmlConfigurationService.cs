namespace VideoConverter.Core.Services
{
	using System;
	using System.IO;
	using System.Text;
	using System.Xml;
	using System.Xml.Serialization;

	using VideoConverter.Core.Models;

	public sealed class XmlConfigurationService : IConfigurationService, IDisposable
	{
		private readonly string configPath;
		private readonly FileSystemWatcher watcher;
		private Configuration? config;

		public XmlConfigurationService(string configPath)
		{
			this.configPath = configPath;

			var dir = Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory;
			var name = Path.GetFileName(configPath);
			if (!Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}

			watcher = new FileSystemWatcher(dir, name);
			watcher.Changed += OnFileChanged;
			watcher.NotifyFilter = NotifyFilters.LastWrite;
		}

		public void Dispose()
		{
			watcher.Dispose();
		}

		public Configuration GetConfiguration()
		{
			if (config is not null)
			{
				return config;
			}

			config = new Configuration();

			if (!File.Exists(configPath))
			{
				config.MapperDatabase = GetMapperDatabase();
			}
			else
			{
				UpdateConfiguration(config, configPath);
			}
			watcher.EnableRaisingEvents = true;

			return config;
		}

		public void SetConfiguration(Configuration config)
		{
			watcher.EnableRaisingEvents = false;

			var directory = Path.GetDirectoryName(configPath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			using var writer = new StreamWriter(configPath, false, Encoding.UTF8);

			var serializer = new XmlSerializer(typeof(Configuration));

			serializer.Serialize(writer, config);

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

		private static void UpdateConfiguration(Configuration config, string configPath)
		{
#pragma warning disable CA1031 // Do not catch general exception types
			try
			{
				using var reader = new StreamReader(configPath, Encoding.UTF8);
				using var xmlReader = XmlReader.Create(reader);
				var serializer = new XmlSerializer(typeof(Configuration));

				var tempConfig = serializer.Deserialize(xmlReader) as Configuration ?? new Configuration();

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
#pragma warning restore CA1031 // Do not catch general exception types
		}

		private void OnFileChanged(object sender, FileSystemEventArgs e)
		{
			if (e.ChangeType != WatcherChangeTypes.Changed || config is null)
			{
				return;
			}

			UpdateConfiguration(config, configPath);
		}
	}
}
