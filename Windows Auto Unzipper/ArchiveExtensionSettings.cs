using System;
using System.Collections.Generic;
using System.Linq;

namespace Windows_Auto_Unzipper
{
    static class ArchiveExtensionSettings
    {
        public const string DefaultExtensions = ".zip";

        public static IReadOnlyCollection<string> Parse(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return new[] { DefaultExtensions };
            }

            var extensions = value
                .Split(new[] { ',', ';', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Normalize)
                .Where(extension => !String.IsNullOrWhiteSpace(extension))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return extensions.Length == 0 ? new[] { DefaultExtensions } : extensions;
        }

        public static string Format(string value)
        {
            return String.Join(", ", Parse(value));
        }

        private static string Normalize(string extension)
        {
            extension = extension.Trim().ToLowerInvariant();
            if (extension == "*")
            {
                return String.Empty;
            }

            if (extension.StartsWith("*."))
            {
                extension = extension.Substring(1);
            }
            else if (!extension.StartsWith("."))
            {
                extension = "." + extension;
            }

            return extension;
        }
    }
}
