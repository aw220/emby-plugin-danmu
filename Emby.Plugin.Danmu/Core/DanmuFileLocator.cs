using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Emby.Plugin.Danmu.Core
{
    public static class DanmuFileLocator
    {
        public const string DanmuDirectoryName = "danmu";

        public static string BuildDownloadScopeKey(string itemId, string providerId)
        {
            return $"{itemId}_{providerId}";
        }

        public static string GetDanmuDirectoryPath(string containingFolderPath, bool useLegacyDirectory = false)
        {
            if (string.IsNullOrWhiteSpace(containingFolderPath))
            {
                return null;
            }

            return useLegacyDirectory
                ? containingFolderPath
                : Path.Combine(containingFolderPath, DanmuDirectoryName);
        }

        public static string EnsureDanmuDirectory(string containingFolderPath, bool useLegacyDirectory = false)
        {
            var directoryPath = GetDanmuDirectoryPath(containingFolderPath, useLegacyDirectory);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return null;
            }

            Directory.CreateDirectory(directoryPath);
            return directoryPath;
        }

        public static string GetDanmuXmlFilePath(string containingFolderPath, string fileNameWithoutExtension, string providerId,
            bool useLegacyDirectory = false)
        {
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension) || string.IsNullOrWhiteSpace(providerId))
            {
                return null;
            }

            var directoryPath = GetDanmuDirectoryPath(containingFolderPath, useLegacyDirectory);
            return string.IsNullOrWhiteSpace(directoryPath)
                ? null
                : Path.Combine(directoryPath, fileNameWithoutExtension + "_" + providerId + ".xml");
        }

        public static string GetDefaultDanmuXmlFilePath(string containingFolderPath, string fileNameWithoutExtension,
            bool useLegacyDirectory = false)
        {
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                return null;
            }

            var directoryPath = GetDanmuDirectoryPath(containingFolderPath, useLegacyDirectory);
            return string.IsNullOrWhiteSpace(directoryPath)
                ? null
                : Path.Combine(directoryPath, fileNameWithoutExtension + ".xml");
        }

        public static string GetAssFilePath(string containingFolderPath, string fileNameWithoutExtension, string providerId,
            bool useLegacyDirectory = false)
        {
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension) || string.IsNullOrWhiteSpace(providerId))
            {
                return null;
            }

            var directoryPath = GetDanmuDirectoryPath(containingFolderPath, useLegacyDirectory);
            return string.IsNullOrWhiteSpace(directoryPath)
                ? null
                : Path.Combine(directoryPath, fileNameWithoutExtension + ".chs[" + providerId + "_danmu].ass");
        }

        public static string FindBestExistingDanmuFile(string containingFolderPath, string fileNameWithoutExtension,
            IEnumerable<string> preferredProviderIds, bool useLegacyDirectory = false)
        {
            if (string.IsNullOrWhiteSpace(containingFolderPath) || string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                return null;
            }

            var providerIds = preferredProviderIds?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            var candidateDirectories = EnumerateCandidateDirectories(containingFolderPath, useLegacyDirectory).ToList();

            foreach (var providerId in providerIds)
            {
                foreach (var directoryPath in candidateDirectories)
                {
                    var preferredPath = Path.Combine(directoryPath, fileNameWithoutExtension + "_" + providerId + ".xml");
                    if (File.Exists(preferredPath))
                    {
                        return preferredPath;
                    }
                }
            }

            foreach (var directoryPath in candidateDirectories)
            {
                if (!Directory.Exists(directoryPath))
                {
                    continue;
                }

                var wildcardPattern = fileNameWithoutExtension + "_*.xml";
                var anyProviderFile = Directory
                    .EnumerateFiles(directoryPath, wildcardPattern, SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(anyProviderFile))
                {
                    return anyProviderFile;
                }
            }

            foreach (var directoryPath in candidateDirectories)
            {
                var defaultPath = Path.Combine(directoryPath, fileNameWithoutExtension + ".xml");
                if (File.Exists(defaultPath))
                {
                    return defaultPath;
                }
            }

            return null;
        }

        private static IEnumerable<string> EnumerateCandidateDirectories(string containingFolderPath, bool useLegacyDirectory)
        {
            var primaryDirectory = GetDanmuDirectoryPath(containingFolderPath, useLegacyDirectory);
            if (!string.IsNullOrWhiteSpace(primaryDirectory))
            {
                yield return primaryDirectory;
            }

            if (!useLegacyDirectory && !string.Equals(primaryDirectory, containingFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                yield return containingFolderPath;
            }
        }
    }
}
