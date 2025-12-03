using System.Threading.Tasks;

namespace SolastaUnfinishedBusiness.Models.TranslationServices;

/// <summary>
///     Defines the contract for translation services.
/// </summary>
internal interface ITranslationService
{
    /// <summary>
    ///     Gets the display name of the translation service.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Translates the source text to the target language.
    /// </summary>
    /// <param name="sourceText">The text to translate.</param>
    /// <param name="targetLanguageCode">The target language code (e.g., "zh-CN", "ja", "ko").</param>
    /// <returns>The translated text, or the original text if translation fails.</returns>
    Task<string> TranslateAsync(string sourceText, string targetLanguageCode);

    /// <summary>
    ///     Validates if the service is properly configured and ready to use.
    /// </summary>
    /// <returns>True if the service is ready, false otherwise.</returns>
    bool IsConfigured();
}
