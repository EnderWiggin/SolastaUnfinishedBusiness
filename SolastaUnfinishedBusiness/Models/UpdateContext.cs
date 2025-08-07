using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SolastaUnfinishedBusiness.Api.LanguageExtensions;
using SolastaUnfinishedBusiness.Api.ModKit.Utility;
using SolastaUnfinishedBusiness.CustomUI;
using UnityModManagerNet;

namespace SolastaUnfinishedBusiness.Models;

internal static class UpdateContext
{
    private static readonly string TempFolder = $"TEMP_UPDATE{Path.DirectorySeparatorChar}";
    private static readonly string ModFolder = $"SolastaUnfinishedBusiness{Path.DirectorySeparatorChar}";
    private static InfoJson Info { get; set; }
    private static string BaseURL { get; set; }
    private static string VersionURL { get; set; }
    private static string InstalledVersion { get; set; }
    private static string LatestVersion { get; set; }
    private static string PreviousVersion { get; set; }
    internal static bool InProgress { get; private set; }
    internal static int Progress { get; private set; }

    private static bool ShouldUpdate;

    internal static void Load()
    {
        var infoPayload = File.ReadAllText(Path.Combine(Main.ModFolder, "Info.json"));
        Info = JsonConvert.DeserializeObject<InfoJson>(infoPayload);

        BaseURL = Info.Repository + "/releases/download";
        VersionURL = Info.VersionURL;
        InstalledVersion = Info.Version;
        PreviousVersion = GetPreviousVersion();

        LatestVersion = GetLatestVersion(out ShouldUpdate);
        if (ShouldUpdate)
        {
            DisplayUpdateMessage();
        }
        else
        {
            CustomModels.AlertIfModelsNotFound();

            if (Main.Settings.DisplayModMessage == 0)
            {
                DisplayWelcomeMessage();
            }
        }

        // display mod message every 100 launches
        Main.Settings.DisplayModMessage = (Main.Settings.DisplayModMessage + 1) % 100;
    }

    private static string GetPreviousVersion()
    {
        var a1 = InstalledVersion.Split('.');
        var minor = int.Parse(a1[3]);

        a1[3] = (--minor).ToString();

        // ReSharper disable once AssignNullToNotNullAttribute
        return string.Join(".", a1);
    }

    private static string GetLatestVersion(out bool shouldUpdate)
    {
        var version = "";

        shouldUpdate = false;

        using var wc = new WebClient();

        wc.Encoding = Encoding.UTF8;

        try
        {
            var infoPayload = wc.DownloadString(VersionURL);
            var infoJson = JsonConvert.DeserializeObject<JObject>(infoPayload);

            // ReSharper disable once AssignNullToNotNullAttribute
            version = infoJson["Version"].Value<string>();

            var a1 = InstalledVersion.Split('.');
            var a2 = version.Split('.');
            var v1 = a1[0] + a1[1] + a1[2] + int.Parse(a1[3]).ToString("D3");
            var v2 = a2[0] + a2[1] + a2[2] + int.Parse(a2[3]).ToString("D3");

            shouldUpdate = string.Compare(v2, v1, StringComparison.Ordinal) > 0;
        }
        catch
        {
            Main.Error("cannot fetch update data.");
        }

        return version;
    }

    internal static void UpdateMod(bool toLatest = true)
    {
        if (InProgress) { return; }

        if (!ShouldUpdate && toLatest)
        {
            ShowMessage("Mod version is already the latest or higher",
                "ChangeLog", OpenChangeLog);

            return;
        }

        InProgress = true;
        Progress = 0;

        var version = toLatest ? LatestVersion : PreviousVersion;
        var zipFile = "SolastaUnfinishedBusiness.zip";
        var fullZipFile = Path.Combine(Main.ModFolder, zipFile);
        var fullZipFolder = Path.Combine(Main.ModFolder, TempFolder);
        var baseUrlByVersion = BaseURL.Replace("download", $"download/{version}");
        var url = new Uri($"{baseUrlByVersion}/{zipFile}");

        try
        {
            using var wc = new WebClient();

            wc.Encoding = Encoding.UTF8;
            wc.DownloadProgressChanged += (_, e) => Progress = e.ProgressPercentage;

            wc.DownloadFileCompleted += OnDownloadFileCompleted;

            wc.DownloadFileAsync(url, fullZipFile);
        }
        catch (Exception ex)
        {
            InProgress = false;
            Main.Error($"Failed to update mod: {ex.Message}: {ex.StackTrace}");

            ShowMessage($"Cannot fetch update payload. Try again or download from:\r\n{url}.",
                "Open Download Url", () => OpenUrl(url.ToString()),
                severity: MessageModal.Severity.Serious3);
        }

        return;

        void OnDownloadFileCompleted(object _, AsyncCompletedEventArgs e)
        {
            InProgress = false;

            if (e.Error != null)
            {
                ShowMessage($"Cannot fetch update payload. Try again or download from:\r\n{url}.",
                    "Open Download Url", () => OpenUrl(url.ToString()),
                    severity: MessageModal.Severity.Serious3);
                return;
            }

            if (e.Cancelled)
            {
                ShowMessage("Update was cancelled",
                    "Open Download Url", () => OpenUrl(url.ToString()),
                    severity: MessageModal.Severity.Serious3);
                return;
            }

            try
            {
                if (Directory.Exists(fullZipFolder))
                {
                    Directory.Delete(fullZipFolder, true);
                }

                ZipFile.ExtractToDirectory(fullZipFile, fullZipFolder);

                foreach (var sourceFile in Directory.GetFiles(fullZipFolder, "*", SearchOption.AllDirectories))
                {
                    var destFile = sourceFile.ReplaceFirst(TempFolder, string.Empty);

                    while (Regex.Matches(destFile, Regex.Escape(ModFolder)).Count > 1)
                    {
                        destFile = destFile.ReplaceLastOccurrence(ModFolder, string.Empty);
                    }

                    var destFolder = Path.GetDirectoryName(destFile)!;

                    Directory.CreateDirectory(destFolder);

                    if (Checksum(destFile) != Checksum(sourceFile))
                    {
                        File.Delete(destFile);
                        File.Move(sourceFile, destFile);
                    }
                }

                ShowMessage("Mod update is successful. Please restart.", "ChangeLog", OpenChangeLog);
            }
            catch (Exception err)
            {
                Main.Error($"Failed to update mod: {err.Message}: {err.StackTrace}");

                ShowMessage($"Failed to unpack update. Try again or download and update manually from:\r\n{url}.",
                    "Open Download Url", () => OpenUrl(url.ToString()),
                    severity: MessageModal.Severity.Serious3);
            }
            finally
            {
                try
                {
                    File.Delete(fullZipFile);
                    Directory.Delete(fullZipFolder, true);
                }
                catch
                {
                    /* ignored */
                }
            }
        }
    }

    internal static void DisplayRollbackMessage()
    {
        if (InProgress) { return; }

        ShowMessage($"Would you like to rollback to {PreviousVersion}?",
            "Message/&MessageOkTitle", () => UpdateMod(false),
            "Message/&MessageCancelTitle");
    }

    private static void DisplayUpdateMessage()
    {
        ShowMessage($"Version {LatestVersion} is now available. Open Mod UI > Gameplay > General to update.",
            "Changelog", OpenChangeLog);
    }

    private static void DisplayWelcomeMessage()
    {
        ShowMessage("Message/&MessageModWelcomeDescription",
            "ChangeLog", OpenChangeLog);
    }

    private static void ShowMessage(
        string content,
        string validateCaption,
        [CanBeNull] MessageModal.MessageValidatedHandler onValidated = null,
        string cancelCaption = "Message/&MessageOkTitle",
        [CanBeNull] MessageModal.MessageCancelledHandler onCancelled = null,
        string title = "Message/&MessageModWelcomeTitle",
        MessageModal.Severity severity = MessageModal.Severity.Attention2
    )
    {
        onValidated ??= () => { };
        onCancelled ??= () => { };
        Gui.GuiService.ShowMessage(severity, title, content, validateCaption, cancelCaption, onValidated, onCancelled);

        UnityModManager.UI.Instance.ToggleWindow(false);
    }

    internal static void OpenChangeLog()
    {
        OpenUrl(Info.Changelog);
    }

    internal static void OpenDocumentation(string filename)
    {
        OpenUrl($"file://{Main.ModFolder}/Documentation/{filename}");
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(url);
        }
        catch
        {
            // because of this: https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                throw;
            }
        }
    }

    private static string Checksum(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var buffer = new BufferedStream(file);
        using var cryptoProvider = SHA1.Create();

        var hash = cryptoProvider.ComputeHash(buffer);

        var str = new StringBuilder();
        foreach (var b in hash)
        {
            str.Append(b.ToString("X2"));
        }

        return str.ToString();
    }
}
