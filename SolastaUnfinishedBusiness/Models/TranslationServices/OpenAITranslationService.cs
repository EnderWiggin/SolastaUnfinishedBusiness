using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SolastaUnfinishedBusiness.Models.TranslationServices;

/// <summary>
///     Translation service using OpenAI-compatible API (works with OpenAI, Azure OpenAI, local LLMs, etc.).
/// </summary>
internal sealed class OpenAITranslationService : ITranslationService
{
    private const string PlayerPrefsApiKeyKey = "UB_OpenAI_ApiKey";

    // Default values - endpoint should be base URL up to /v1, we append /chat/completions automatically
    internal const string DefaultEndpoint = "https://api.openai.com/v1";
    internal const string DefaultModel = "gpt-4o-mini";
    internal const float DefaultTemperature = 0.3f;
    internal const float DefaultTopP = 1.0f;
    internal const int DefaultTopK = 0;

    internal const string DefaultSystemPrompt =
        "You are a professional translator. Translate the following text to {targetLanguage}. " +
        "Keep the original formatting, preserve any special characters or markup. " +
        "Only output the translated text, nothing else.";

    private static readonly HttpClient HttpClient;
    private static bool _servicePointConfigured;

    static OpenAITranslationService()
    {
        HttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10) // LLM requests can take a long time
        };
    }

    /// <summary>
    ///     Configures ServicePoint for high concurrency. Call before making requests.
    /// </summary>
    private static void EnsureServicePointConfigured(string endpoint)
    {
        if (_servicePointConfigured)
        {
            return;
        }

        try
        {
            ServicePointManager.DefaultConnectionLimit = int.MaxValue;
            
            var uri = new Uri(endpoint);
            var servicePoint = ServicePointManager.FindServicePoint(uri);
            servicePoint.ConnectionLimit = int.MaxValue;
            
            _servicePointConfigured = true;
        }
        catch (Exception ex)
        {
            Main.Error($"Failed to configure ServicePoint: {ex.Message}");
        }
    }

    public string Name => "OpenAI";

    public async Task<string> TranslateAsync(string sourceText, string targetLanguageCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sourceText))
        {
            return string.Empty;
        }

        try
        {
            var apiKey = GetApiKey();
            var baseEndpoint = Main.Settings.OpenAIEndpoint.TrimEnd('/');
            var endpoint = $"{baseEndpoint}/chat/completions";
            
            EnsureServicePointConfigured(baseEndpoint);
            
            var model = Main.Settings.OpenAIModel;
            var temperature = Main.Settings.OpenAITemperature;
            var topP = Main.Settings.OpenAITopP;
            var systemPrompt = Main.Settings.OpenAISystemPrompt
                .Replace("{targetLanguage}", GetLanguageName(targetLanguageCode));

            var requestBody = new
            {
                model,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = sourceText }
                },
                temperature,
                top_p = topP,
                max_tokens = 4096
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = content;
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await HttpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Main.Error($"OpenAI API error: {response.StatusCode} - {responseContent}");
                return sourceText;
            }

            var responseJson = JObject.Parse(responseContent);
            var translatedText = responseJson["choices"]?[0]?["message"]?["content"]?.ToString();

            return string.IsNullOrEmpty(translatedText) ? sourceText : translatedText.Trim();
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation
        }
        catch (Exception ex)
        {
            Main.Error($"OpenAI translation failed: {ex.Message}");
            return sourceText;
        }
    }

    public bool IsConfigured()
    {
        var apiKey = GetApiKey();
        return !string.IsNullOrEmpty(apiKey) &&
               !string.IsNullOrEmpty(Main.Settings.OpenAIEndpoint) &&
               !string.IsNullOrEmpty(Main.Settings.OpenAIModel);
    }

    #region API Key Storage (PlayerPrefs)

    internal static string GetApiKey()
    {
        return PlayerPrefs.GetString(PlayerPrefsApiKeyKey, string.Empty);
    }

    internal static void SetApiKey(string apiKey)
    {
        PlayerPrefs.SetString(PlayerPrefsApiKeyKey, apiKey ?? string.Empty);
        PlayerPrefs.Save();
    }

    internal static void ClearApiKey()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsApiKeyKey);
        PlayerPrefs.Save();
    }

    #endregion

    private static string GetLanguageName(string languageCode)
    {
        return languageCode switch
        {
            "de" => "German",
            "en" => "English",
            "es" => "Spanish",
            "fr" => "French",
            "ja" => "Japanese",
            "it" => "Italian",
            "ko" => "Korean",
            "pt" => "Portuguese",
            "ru" => "Russian",
            "zh-CN" => "Simplified Chinese",
            _ => languageCode
        };
    }
}
