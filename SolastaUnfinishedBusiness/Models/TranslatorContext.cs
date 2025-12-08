using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using I2.Loc;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;

namespace SolastaUnfinishedBusiness.Models;

internal struct LanguageEntry
{
    public string Code;
    public string Text;
    public string Directory;
    [UsedImplicitly] public string SourceCode;
}

internal static class TranslatorContext
{
    private const string UnofficialLanguagesFolderPrefix = "UnofficialTranslations/";

    internal const string English = "en";

    internal static readonly string[] AvailableLanguages =
    [
        "de", "en", "es", "fr", "ja", "it", "ko", "pt", "ru", "zh-CN"
    ];

    internal static readonly List<LanguageEntry> Languages = [];

    private static readonly Regex RegexHasCJK = new(@"\p{IsCJKUnifiedIdeographs}", RegexOptions.Compiled);

    /// <summary>
    ///     Maps unofficial language codes to official language codes.
    /// </summary>
    private static Dictionary<string, string> SourceCodeCache { get; } = new();

    public static bool IsCJKChar(char c)
    {
        return c >= 0x4E00 && c <= 0x9FA5;
    }

    public static bool HasCJKChar(string s)
    {
        return s.Length > 0 && RegexHasCJK.IsMatch(s);
    }

    public static bool HasCJKCharQuick(string s)
    {
        return s.Length > 0 && IsCJKChar(s[0]);
    }

    internal static void EarlyLoad()
    {
        if (Main.Settings.DisableUnofficialTranslations)
        {
            Main.Info("Unofficial translations support disabled.");

            return;
        }

        if (!Directory.Exists(Path.Combine(Main.ModFolder, UnofficialLanguagesFolderPrefix)))
        {
            Main.Error("Unofficial translations not found.");

            return;
        }

        LoadCustomLanguages();
        LoadCustomTerms();

        // LOAD CUSTOM FONTS

        var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();

        // JAPANESE

        var fullFilename = Path.Combine(Main.ModFolder, $"{UnofficialLanguagesFolderPrefix}JapaneseHanSans.unity3d");

        if (!File.Exists(fullFilename))
        {
            Main.Error($"Loading the font bundle {fullFilename}.");
        }
        else
        {
            var fontBundle = AssetBundle.LoadFromFile(fullFilename);

            AddFont("NotoSansJP-Light SDF", fontBundle, allFonts, "Noto-Light SDF", "Noto-Thin SDF");
            AddFont("NotoSansJP-Regular SDF", fontBundle, allFonts, "Noto-Regular SDF", "LiberationSans SDF");
            AddFont("NotoSansJP-Bold SDF", fontBundle, allFonts, "Noto-Bold SDF");
        }

        // KOREAN

        fullFilename = Path.Combine(Main.ModFolder, $"{UnofficialLanguagesFolderPrefix}KoreanHanSans.unity3d");

        if (!File.Exists(fullFilename))
        {
            Main.Error($"Loading the font bundle {fullFilename}.");
        }
        else
        {
            var fontBundle = AssetBundle.LoadFromFile(fullFilename);

            AddFont("SourceHanSansK-Light SDF", fontBundle, allFonts, "Noto-Light SDF", "Noto-Thin SDF");
            AddFont("SourceHanSansK-Regular SDF", fontBundle, allFonts, "Noto-Regular SDF", "LiberationSans SDF");
            AddFont("SourceHanSansK-Bold SDF", fontBundle, allFonts, "Noto-Bold SDF");
        }
    }

    private static void LoadCustomLanguages()
    {
        var cultureInfos = CultureInfo.GetCultures(CultureTypes.AllCultures);
        var directoryInfo = new DirectoryInfo($@"{Main.ModFolder}/{UnofficialLanguagesFolderPrefix}");
        var directories = directoryInfo.GetDirectories();

        foreach (var directory in directories)
        {
            var code = directory.Name;
            var cultureInfo = cultureInfos.FirstOrDefault(o => o.Name == code);

            if (File.Exists($"{directory.FullName}/info.json"))
            {
                var info = JsonConvert.DeserializeObject<JObject>(File.ReadAllText($"{directory.FullName}/info.json"));
                var sourceCode = info["SourceCode"]?.ToString() ?? string.Empty;

                if (!string.IsNullOrEmpty(sourceCode))
                {
                    SourceCodeCache.Add(code, sourceCode);
                }

                Languages.Add(new LanguageEntry
                {
                    Code = code,
                    Text = info["NativeName"]!.ToString(),
                    Directory = directory.FullName,
                    SourceCode = sourceCode
                });

                Main.Info($"Language {code} detected.");
            }
            else if (cultureInfo != null)
            {
                if (LocalizationManager.HasLanguage(cultureInfo.DisplayName))
                {
                    Main.Error($"Language {code} from {directory.Name} already in game.");
                }
                else
                {
                    Languages.Add(new LanguageEntry
                    {
                        Code = code,
                        Text = cultureInfo.TextInfo.ToTitleCase(cultureInfo.NativeName),
                        Directory = directory.FullName
                    });

                    Main.Info($"Language {code} detected.");
                }
            }
            else
            {
                Main.Error($"Language {code} illegal!");
            }
        }
    }

    private static void LoadCustomTerms()
    {
        var languageSourceData = LocalizationManager.Sources[0];

        // load new language terms
        foreach (var language in Languages)
        {
            // add language
            languageSourceData.AddLanguage(language.Text, language.Code);

            var languageIndex = languageSourceData.GetLanguageIndex(language.Text);

            // add terms
            var directoryInfo = new DirectoryInfo(language.Directory);
            var files = directoryInfo.GetFiles("*.txt");
            var separator = new[] { '=' };

            foreach (var file in files)
            {
                using var sr = new StreamReader(file.FullName);

                while (sr.ReadLine() is { } line)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    try
                    {
                        var split = line.Split(separator, 2);
                        var term = split[0];
                        var text = split[1];

                        languageSourceData.AddTerm(term).Languages[languageIndex] = text;
                    }
                    catch
                    {
                        Main.Error($"Skipping line [{line}] in file [{file.FullName}]");
                    }
                }
            }
        }
    }

    private static void AddFont(
        string fontName,
        AssetBundle fontBundle,
        IEnumerable<TMP_FontAsset> allFonts,
        params string[] fontsToAppend)
    {
        var modFontAsset = fontBundle.LoadAsset<TMP_FontAsset>($"{fontName}.asset");

        if (!modFontAsset)
        {
            Main.Error($"Font asset {fontName} not found.");

            return;
        }

        foreach (var tmpFontAsset in allFonts.Where(x => fontsToAppend.Contains(x.name)))
        {
            tmpFontAsset.fallbackFontAssetTable.Add(modFontAsset);

            Main.Info($"Font asset {fontName} loaded.");
        }
    }

    private static bool IsModTerm(string fullName, string languageCode)
    {
        return fullName.StartsWith(languageCode) && fullName.EndsWith($"{languageCode}.txt");
    }

    private static bool IsFixedTerm(string fullName, string languageCode)
    {
        return fullName == $"Fixes-{languageCode}.txt";
    }

    [UsedImplicitly]
    internal static IEnumerable<string> GetTranslations(string languageCode, Func<string, string, bool> validate)
    {
        using var zipStream = new MemoryStream(Properties.Resources.Translations);
        using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);

        foreach (var entry in zip.Entries.Where(x => validate(x.FullName, languageCode)))
        {
            using var dataStream = entry.Open();
            using var data = new StreamReader(dataStream);

            while (!data.EndOfStream)
            {
                yield return data.ReadLine();
            }
        }
    }

    private static Dictionary<string, string> GetTermsDict(
        string languageCode,
        Func<string, string, bool> validate)
    {
        var result = new Dictionary<string, string>();
        var separator = new[] { '=' };

        if (SourceCodeCache.TryGetValue(languageCode, out var sourceCode))
        {
            // if has source language, use it
            languageCode = sourceCode;
        }

        foreach (var line in GetTranslations(languageCode, validate))
        {
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            var split = line.Split(separator, 2);

            if (split.Length != 2)
            {
                Main.Error($"cannot parse line {line}");
                continue;
            }

            var term = split[0];
            var text = split[1];

            if (result.ContainsKey(term))
            {
                Main.Error($"duplicate term {term}");
            }
            else
            {
                result.Add(term, text);
            }
        }

        return result;
    }

    internal static void Load()
    {
        var languageCode = LocalizationManager.CurrentLanguageCode;

        var englishTerms = GetTermsDict(English, IsModTerm);
        var currentLanguageTerms = languageCode != English ? GetTermsDict(languageCode, IsModTerm) : englishTerms;
        var fixedTerms = GetTermsDict(languageCode, IsFixedTerm);

        var languageSourceData = LocalizationManager.Sources[0];
        var languageIndex = languageSourceData.GetLanguageIndex(LocalizationManager.CurrentLanguage);

        // loads mod translations
        // we loop on default EN terms collection as this is the one to be trusted
        var lineCount = 0;

        foreach (var term in englishTerms.Keys)
        {
            // if we find a translated term them we use it otherwise fall back to EN default
            if (!currentLanguageTerms.TryGetValue(term, out var text))
            {
                text = englishTerms[term];
            }

            AddTerm(term, text);

            lineCount++;
        }

        Main.Info($"{lineCount} {languageCode} translation terms loaded of {currentLanguageTerms.Count} provided.");

        // loads official translations fixes
        lineCount = 0;

        foreach (var term in fixedTerms.Keys)
        {
            var text = fixedTerms[term];

            AddTerm(term, text);

            lineCount++;
        }

        Main.Info($"{lineCount} {languageCode} translation fixes loaded.");

        // creates a report on missing terms
        if (languageCode == English)
        {
            return;
        }

        var termsToAdd = englishTerms.Keys.Except(currentLanguageTerms.Keys).ToArray();

        if (termsToAdd.Length != 0)
        {
            Main.Info("ADD THESE TERMS:");

            foreach (var term in termsToAdd)
            {
                Main.Info($"{term} is missing from {languageCode} translation assets");
            }
        }

        var termsToDelete = currentLanguageTerms.Keys.Except(englishTerms.Keys);

        if (!termsToDelete.Any())
        {
            return;
        }

        Main.Info("DELETE THESE TERMS:");

        foreach (var term in currentLanguageTerms.Keys.Except(englishTerms.Keys))
        {
            Main.Info($"{term} must be deleted from {languageCode} translation assets");
        }

        return;

        void AddTerm(string term, string text)
        {
            var termData = languageSourceData.GetTermData(term);

            if (termData?.Languages[languageIndex] != null)
            {
                // ReSharper disable once InvocationIsSkipped
                Main.Log($"term {term} overwritten with text {text}");
                termData.Languages[languageIndex] = text;
            }
            else
            {
                languageSourceData.AddTerm(term).Languages[languageIndex] = text;
            }
        }
    }

    internal static bool HasTranslation(string term)
    {
        return LocalizationManager.Sources[0].ContainsTerm(term);
    }
}
