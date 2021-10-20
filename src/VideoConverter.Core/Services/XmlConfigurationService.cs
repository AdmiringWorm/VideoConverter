namespace VideoConverter.Core.Services
{
	using System.Xml;
	using System.Xml.Serialization;
	using System.Text;
	using System;
	using System.IO;
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
				Directory.CreateDirectory(dir);
			watcher = new FileSystemWatcher(dir, name);
			watcher.Changed += OnFileChanged;
			watcher.NotifyFilter = NotifyFilters.LastWrite;
		}

		public Configuration GetConfiguration()
		{
			if (this.config is not null)
				return this.config;

			this.config = new Configuration();

			if (!File.Exists(this.configPath))
			{
				this.config.MapperDatabase = GetMapperDatabase();
			}
			else
			{
				UpdateConfiguration(this.config, this.configPath);
			}
			watcher.EnableRaisingEvents = true;

			return this.config;
		}

		private static void UpdateConfiguration(Configuration config, string configPath)
		{
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
			}
			catch
			{
			}
		}

		public void SetConfiguration(Configuration config)
		{
			this.watcher.EnableRaisingEvents = false;

			var directory = Path.GetDirectoryName(configPath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				Directory.CreateDirectory(directory);

			using var writer = new StreamWriter(configPath, false, Encoding.UTF8);

			var serializer = new XmlSerializer(typeof(Configuration));

			serializer.Serialize(writer, config);

			this.config = config;

			this.watcher.EnableRaisingEvents = true;
		}

		private void OnFileChanged(object sender, FileSystemEventArgs e)
		{
			if (e.ChangeType != WatcherChangeTypes.Changed || this.config is null)
				return;

			UpdateConfiguration(this.config, this.configPath);
		}

		private static string GetMapperDatabase()
		{
			return Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"VideoConverter",
				"storage.db"
			);
		}

		public void Dispose()
		{
			this.watcher.Dispose();
		}
	}
}
