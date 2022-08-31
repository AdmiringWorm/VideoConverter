namespace VideoConverter.Core.Extensions
{
	using System;

	public static class StringExtensions
	{
		public static string GetExtensionFileType(this string extension)
		{
			if (extension is null)
			{
				throw new ArgumentNullException(nameof(extension));
			}

			return extension.TrimStart('.').ToUpperInvariant() switch
			{
				"AVI" => "Audio Video Interleave",
				"DV" => "DIGITAL VIDEO",
				"GIF" => "Graphics Interchange Format",
				"M4A" => "MPEG-4-Audio",
				"MK3D" or "MKV3D" => "Matroska-3D",
				"MKA" => "Matroska-Audio",
				"MKV" => "Matroska",
				"MP4" or "M4V" => "MPEG-4",
				"MPG" or "MPEG" => "MPEG",
				"VOB" => "DVD Video",
				"WEBM" => "WebM",
				"WMA" => "Advanced Systems Format-Audio",
				"WMV" or "ASF" => "Advanced Systems Format",
				_ => throw new ArgumentOutOfRangeException(nameof(extension)),
			};
		}

		public static string GetFileExtension(this string extensionType)
		{
			if (extensionType is null)
			{
				throw new ArgumentNullException(nameof(extensionType));
			}

			return extensionType.ToUpperInvariant() switch
			{
				"ADVANCED SYSTEMS FORMAT-AUDIO" => ".wma",
				"ADVANCED SYSTEMS FORMAT" => ".wmv",
				"AUDIO VIDEO INTERLEAVE" => ".avi",
				"DIGITAL VIDEO" => ".dv",
				"DVD VIDEO" => ".vob",
				"GIF" or "Graphics Interchange Format" => ".gif",
				"MATROSKA-3D" => ".mk3d",
				"MATROSKA-AUDIO" => ".mka",
				"MATROSKA" => ".mkv",
				"MPEG-4-AUDIO" => ".m4a",
				"MPEG-4" => ".mp4",
				"MPEG" => ".mpg",
				"WEBM" => ".webm",

				_ => throw new ArgumentOutOfRangeException(nameof(extensionType))
			};
		}
	}
}
