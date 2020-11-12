namespace VideoConverter.Core.Extensions
{
	using System;

	public static class StringExtensions
	{
		public static string GetExtensionFileType(this string extension)
		{
			if (extension is null)
				throw new ArgumentNullException(nameof(extension));

			return (extension.TrimStart('.').ToUpperInvariant()) switch
			{
				"MKV" => "Matroska",
				"MK3D" or "MKV3D" => "Matroska-3D",
				"MKA" => "Matroska-Audio",
				"MP4" or "M4V" => "MPEG-4",
				"M4A" => "MPEG-4-Audio",
				"MPG" or "MPEG" => "MPEG",
				"DV" => "DIGITAL VIDEO",
				"AVI" => "Audio Video Interleave",
				"VOB" => "DVD Video",
				"WMV" or "ASF" => "Advanced Systems Format",
				"WMA" => "Advanced Systems Format-Audio",
				_ => throw new ArgumentOutOfRangeException(nameof(extension)),
			};
		}

		public static string GetFileExtension(this string extensionType)
		{
			if (extensionType is null)
				throw new ArgumentNullException(nameof(extensionType));

			return extensionType.ToUpperInvariant() switch
			{
				"MATROSKA" => ".mkv",
				"MATROSKA-3D" => ".mk3d",
				"MATROSKA-AUDIO" => ".mka",

				"MPEG-4" => ".mp4",
				"MPEG-4-AUDIO" => ".m4a",

				"MPEG" => ".mpg",

				"DVD VIDEO" => ".vob",

				"DIGITAL VIDEO" => ".dv",

				"AUDIO VIDEO INTERLEAVE" => ".avi",

				"ADVANCED SYSTEMS FORMAT" => ".wmv",
				"ADVANCED SYSTEMS FORMAT-AUDIO" => ".wma",

				_ => throw new ArgumentOutOfRangeException(nameof(extensionType))
			};
		}
	}
}
