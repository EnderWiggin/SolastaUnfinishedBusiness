using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SolastaUnfinishedBusiness.Models.TranslationServices;

/// <summary>
///     Translation service using Google Translate's free API.
/// </summary>
internal sealed class GoogleTranslationService : ITranslationService
{
    private const string BaseUrl = "https://translate.googleapis.com/translate_a/single";

    private static readonly HttpClient HttpClient;
    private static readonly ConcurrentBag<WebClient> WebClients = [];

    static GoogleTranslationService()
    {
        ServicePointManager.DefaultConnectionLimit = int.MaxValue;

        HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        try
        {
            var servicePoint = ServicePointManager.FindServicePoint(new Uri(BaseUrl));
            servicePoint.ConnectionLimit = int.MaxValue;
        }
        catch (Exception ex)
        {
            Main.Error($"Failed to configure ServicePoint: {ex.Message}");
        }
    }

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

            var payload = await GetPayload(url, cancellationToken);

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
            Main.Error($"Google Translate failed: {ex}");
            return sourceText;
        }
    }

    [NotNull]
    private static async Task<string> GetPayload([NotNull] string url, CancellationToken cancellationToken)
    {
        if (Main.Settings.GoogleLegacyMode)
        {
            return await GetPayloadWebClient(url);
        }

        return await GetPayloadHttpClient(url, cancellationToken);
    }

    [NotNull]
    private static async Task<string> GetPayloadWebClient([NotNull] string url)
    {
        var client = GetWebClient();
        var payload = await client.DownloadStringTaskAsync(new Uri(url));
        ReturnWebClient(client);

        return payload;
    }

    private static WebClient GetWebClient()
    {
        if (WebClients.TryTake(out var client))
        {
            return client;
        }

        client = new WebClient();

        client.Headers.Add("user-agent",
            "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36");
        client.Encoding = Encoding.UTF8;

        return client;
    }

    private static void ReturnWebClient(WebClient client)
    {
        WebClients.Add(client);
    }

    private static async Task<string> GetPayloadHttpClient(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/41.0.2228.0 Safari/537.36");

        var response = await HttpClient.SendAsync(request, cancellationToken);
        return await response.Content.ReadAsStringAsync();
    }

    public bool IsConfigured()
    {
        return true;
    }
}
