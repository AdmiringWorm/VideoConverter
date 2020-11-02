namespace VideoConverter.Core.Extensions
{
    using System;

    public static class StringExtensions
    {
        public static string GetExtensionFileType(this string extension)
        {
            if (extension is null)
                throw new ArgumentNullException(nameof(extension));

            switch (extension.TrimStart('.').ToUpperInvariant())
            {
                case "MKV":
                    return "Matroska";
                case "MK3D":
                    return "Matroska-3D";
                case "MKA":
                    return "Matroska-Audio";

                case "MP4":
                case "M4V":
                    return "MPEG-4";
                case "M4A":
                    return "MPEG-4-Audio";

                case "MPG":
                case "MPEG":
                    return "MPEG";

                case "DV":
                    return "DIGITAL VIDEO";

                case "AVI":
                    return "Audio Video Interleave";

                case "VOB":
                    return "DVD Video";

                case "WMV":
                case "ASF":
                    return "Advanced Systems Format";
                case "WMA":
                    return "Advanced Systems Format-Audio";

                default:
                    throw new ArgumentOutOfRangeException(nameof(extension));
            }
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
