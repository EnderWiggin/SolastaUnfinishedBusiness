using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
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

    private static readonly HttpClient HttpClient = new();

    public string Name => "Google Translate";

    public async Task<string> TranslateAsync(string sourceText, string targetLanguageCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sourceText))
        {
            return string.Empty;
        }

        try
        {
            var encoded = HttpUtility.UrlEncode(sourceText.Replace("_", " "));
            var url = $"{BaseUrl}?client=gtx&sl=auto&tl={targetLanguageCode}&dt=t&q={encoded}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36");

            var response = await HttpClient.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync();

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
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation
        }
        catch (Exception ex)
        {
            Main.Error($"Google Translate failed: {ex.Message}");
            return sourceText;
        }
    }

    public bool IsConfigured()
    {
        return true;
    }
}
