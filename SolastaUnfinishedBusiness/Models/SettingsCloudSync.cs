using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using JetBrains.Annotations;
using UnityModManagerNet;

namespace SolastaUnfinishedBusiness.Models;

internal static class SettingsCloudSync
{
    internal const string MirrorFileName = "UnfinishedBusiness.sav";

    internal static bool IsMirrorFile([CanBeNull] string path)
    {
        return string.Equals(Path.GetFileName(path), MirrorFileName, StringComparison.OrdinalIgnoreCase);
    }

    internal static void PullIfNewer([NotNull] UnityModManager.ModEntry modEntry)
    {
        try
        {
            var localPath = LocalSettingsPath(modEntry);

            if (!AllowsPull(localPath))
            {
                return;
            }

            var cloudPath = CloudSettingsPath();

            if (cloudPath == null || !File.Exists(cloudPath))
            {
                return;
            }

            if (File.Exists(localPath) &&
                File.GetLastWriteTimeUtc(cloudPath) <= File.GetLastWriteTimeUtc(localPath))
            {
                return;
            }

            if (!TryRead(cloudPath, out _))
            {
                Main.Error(
                    $"Steam Cloud settings mirror '{cloudPath}' is not valid UB settings. Leaving local settings unchanged.");

                return;
            }

            var folder = Path.GetDirectoryName(localPath);

            if (!string.IsNullOrEmpty(folder))
            {
                Main.EnsureFolderExists(folder);
            }

            File.Copy(cloudPath, localPath, true);
            Main.Info("Loaded newer Unfinished Business settings from Steam Cloud.");
        }
        catch (Exception ex)
        {
            Main.Error(ex);
        }
    }

    internal static void Push([NotNull] UnityModManager.ModEntry modEntry)
    {
        try
        {
            if (Main.Settings is not { SyncSettingsThroughSteamCloud: true })
            {
                return;
            }

            var localPath = LocalSettingsPath(modEntry);

            if (!File.Exists(localPath))
            {
                return;
            }

            var cloudPath = CloudSettingsPath();

            if (cloudPath == null)
            {
                return;
            }

            if (File.Exists(cloudPath) && SameBytes(localPath, cloudPath))
            {
                return;
            }

            var folder = Path.GetDirectoryName(cloudPath);

            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            Main.EnsureFolderExists(folder);
            File.Copy(localPath, cloudPath, true);
            Main.Info("Mirrored Unfinished Business settings into the Steam Cloud save folder.");
        }
        catch (Exception ex)
        {
            Main.Error(ex);
        }
    }

    private static string LocalSettingsPath([NotNull] UnityModManager.ModEntry modEntry)
    {
        return Path.Combine(modEntry.Path, "Settings.xml");
    }

    [CanBeNull]
    private static string CloudSettingsPath()
    {
        var gameDirectory = TacticalAdventuresApplication.GameDirectory;

        if (string.IsNullOrEmpty(gameDirectory))
        {
            return null;
        }

        return Path.Combine(gameDirectory, "Saves", MirrorFileName);
    }

    private static bool AllowsPull(string localPath)
    {
        if (!File.Exists(localPath))
        {
            return true;
        }

        return !TryRead(localPath, out var settings) || settings.SyncSettingsThroughSteamCloud;
    }

    private static bool TryRead(string path, out Settings settings)
    {
        settings = null;

        try
        {
            using var stream = File.OpenRead(path);
            var serializer = new XmlSerializer(typeof(Settings));

            settings = serializer.Deserialize(stream) as Settings;

            return settings != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool SameBytes(string left, string right)
    {
        return File.ReadAllBytes(left).SequenceEqual(File.ReadAllBytes(right));
    }
}
