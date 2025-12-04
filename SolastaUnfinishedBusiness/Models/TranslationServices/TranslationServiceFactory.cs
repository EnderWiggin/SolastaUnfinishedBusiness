using System.Collections.Generic;

namespace SolastaUnfinishedBusiness.Models.TranslationServices;

/// <summary>
///     Factory and registry for translation services.
/// </summary>
internal static class TranslationServiceFactory
{
    private static readonly Dictionary<TranslationServiceType, ITranslationService> Services = new()
    {
        { TranslationServiceType.Google, new GoogleTranslationService() },
        { TranslationServiceType.OpenAI, new OpenAITranslationService() }
    };

    internal static readonly string[] ServiceNames =
    [
        "Google Translate",
        "OpenAI"
    ];

    internal static ITranslationService GetCurrentService()
    {
        var serviceType = Main.Settings.SelectedTranslationService;
        return Services.TryGetValue(serviceType, out var service) ? service : Services[TranslationServiceType.Google];
    }

    internal static ITranslationService GetService(TranslationServiceType serviceType)
    {
        return Services.TryGetValue(serviceType, out var service) ? service : Services[TranslationServiceType.Google];
    }
}

public enum TranslationServiceType
{
    Google = 0,
    OpenAI = 1
}
