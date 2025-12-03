using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SolastaUnfinishedBusiness.Models.TranslationServices;

/// <summary>
///     Translation service using Google Translate's free API.
/// </summary>
internal sealed class GoogleTranslationService : ITranslationService
{
    private const string BaseUrl = "https://translate.googleapis.com/translate_a/single";

    public string Name => "Google Translate";

    public Task<string> TranslateAsync(string sourceText, string targetLanguageCode)
    {
        return Task.Run(() => TranslateInternal(sourceText, targetLanguageCode));
    }

    public bool IsConfigured()
    {
        return true;
    }

    private static string TranslateInternal(string sourceText, string targetLanguageCode)
    {
        if (string.IsNullOrEmpty(sourceText))
        {
            return string.Empty;
        }

        try
        {
            var encoded = HttpUtility.UrlEncode(sourceText.Replace("_", " "));
            var url = $"{BaseUrl}?client=gtx&sl=auto&tl={targetLanguageCode}&dt=t&q={encoded}";
            var payload = GetPayload(url);
            var json = JsonConvert.DeserializeObject(payload);

            if (json is not JArray outerArray)
            {
                return sourceText;
            }

            if (outerArray.First() is not JArray terms)
            {
                return sourceText;
            }

            var result = terms.Aggregate(string.Empty, (current, term) => current + term.First());
            return result;
        }
        catch (Exception ex)
        {
            Main.Error($"Google Translate failed: {ex.Message}");
            return sourceText;
        }
    }

    private static string GetPayload(string url)
    {
        using var wc = new WebClient();

        wc.Headers.Add("user-agent",
            "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36");
        wc.Encoding = Encoding.UTF8;

        return wc.DownloadString(url);
    }
}
