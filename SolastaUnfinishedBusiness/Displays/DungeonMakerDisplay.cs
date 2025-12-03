using System;
using System.Linq;
using SolastaUnfinishedBusiness.Api.LanguageExtensions;
using SolastaUnfinishedBusiness.Api.ModKit;
using SolastaUnfinishedBusiness.Models;
using SolastaUnfinishedBusiness.Models.TranslationServices;
using UnityEngine;

namespace SolastaUnfinishedBusiness.Displays;

internal static class DungeonMakerDisplay
{
    private static bool _showOpenAISettings;
    private static bool _showOpenAIAdvancedSettings;
    private static string _openAIApiKeyInput = string.Empty;
    private static bool _apiKeyInitialized;
    internal static void DisplayDungeonMaker()
    {
        const float DOC_BUTTON_WIDTH = 147f;

        UI.Label();

        using (UI.HorizontalScope())
        {
            UI.ActionButton("Aberrations".Bold().Khaki(),
                () => UpdateContext.OpenDocumentation("Monsters/SolastaMonstersAberration.md"),
                UI.Width(DOC_BUTTON_WIDTH));
            UI.ActionButton("Beasts".Bold().Khaki(),
                () => UpdateContext.OpenDocumentation("Monsters/SolastaMonstersBeast.md"), UI.Width(DOC_BUTTON_WIDTH));
            UI.ActionButton("Celestials".Bold().Khaki(),
                () => UpdateContext.OpenDocumentation("Monsters/SolastaMonstersCelestial.md"),
                UI.Width(DOC_BUTTON_WIDTH));
            UI.ActionButton("Constructs".Bold().Khaki(),
                () => UpdateContext.OpenDocumentation("Monsters/SolastaMonstersConstruct.md"),
                UI.Width(DOC_BUTTON_WIDTH));
        }

        using (UI.HorizontalScope())
        {
            UI.ActionButton("Dragons".Bold().Khaki(),
                () => UpdateContext.OpenDocumentation("Monsters/SolastaMonstersDragon.md"), UI.Width(DOC_BUTTON_WIDTH));
            UI.ActionButton("Elementals".Bold().Khaki(),
                () => UpdateContext.OpenDocumentation("Monsters/SolastaMonstersElemental.md"),
                UI.Width(DOC_BUTTON_WIDTH));
            UI.ActionButton("Fey".Bold().Khaki(),
                () => UpdateContext.OpenDocumentation("Monsters/SolastaMonstersFey.md"), UI.Width(DOC_BUTTON_WIDTH));
            UI.ActionButton("Fiend".Bold().Khaki(),
                () => UpdateContext.OpenDocumentation("Monsters/SolastaMonstersFiend.md"), UI.Width(DOC_BUTTON_WIDTH));
        }

        using (UI.HorizontalScope())
        {
            UI.ActionButton("Giants".Bold().Khaki(),
                () => UpdateContext.OpenDocumentation("Monsters/SolastaMonstersGiant.md"), UI.Width(DOC_BUTTON_WIDTH));
            UI.ActionButton("Humanoids".Bold().Khaki(),
                () => UpdateContext.OpenDocumentation("Monsters/SolastaMonstersHumanoid.md"),
                UI.Width(DOC_BUTTON_WIDTH));
            UI.ActionButton("Monstrosities".Bold().Khaki(),
                () => UpdateContext.OpenDocumentation("Monsters/SolastaMonstersMonstrosity.md"),
                UI.Width(DOC_BUTTON_WIDTH));
            UI.ActionButton("Undead".Bold().Khaki(),
                () => UpdateContext.OpenDocumentation("Monsters/SolastaMonstersUndead.md"), UI.Width(DOC_BUTTON_WIDTH));
        }

        UI.Label();
        UI.Label(Gui.Localize("ModUi/&Basic"));
        UI.Label();
        UI.Label(Gui.Localize("ModUi/&DungeonMakerBasicHelp"));
        UI.Label();

        var toggle = Main.Settings.EnableLoggingInvalidReferencesInUserCampaigns;
        if (UI.Toggle(Gui.Localize("ModUi/&EnableLoggingInvalidReferencesInUserCampaigns"), ref toggle))
        {
            Main.Settings.EnableLoggingInvalidReferencesInUserCampaigns = toggle;
        }

        toggle = Main.Settings.EnableSortingDungeonMakerAssets;
        if (UI.Toggle(Gui.Localize("ModUi/&EnableSortingDungeonMakerAssets"), ref toggle))
        {
            Main.Settings.EnableSortingDungeonMakerAssets = toggle;
        }

        toggle = Main.Settings.AllowGadgetsAndPropsToBePlacedAnywhere;
        if (UI.Toggle(Gui.Localize("ModUi/&AllowGadgetsAndPropsToBePlacedAnywhere"), ref toggle))
        {
            Main.Settings.AllowGadgetsAndPropsToBePlacedAnywhere = toggle;
        }

        toggle = Main.Settings.UnleashEnemyAsNpc;
        if (UI.Toggle(Gui.Localize("ModUi/&UnleashEnemyAsNpc"), ref toggle))
        {
            Main.Settings.UnleashEnemyAsNpc = toggle;
        }

        UI.Label();
        UI.Label(Gui.Localize("ModUi/&Advanced"));
        UI.Label();
        UI.Label(Gui.Localize("ModUi/&AdvancedHelp"));
        UI.Label();

        toggle = Main.Settings.AddNewWeaponsAndRecipesToEditor;
        if (UI.Toggle(Gui.Localize(Gui.Localize("ModUi/&EnableAdditionalItemsInDungeonMaker")), ref toggle,
                UI.AutoWidth()))
        {
            Main.Settings.AddNewWeaponsAndRecipesToEditor = toggle;
        }

        toggle = Main.Settings.UnleashNpcAsEnemy;
        if (UI.Toggle(Gui.Localize("ModUi/&UnleashNpcAsEnemy"), ref toggle))
        {
            Main.Settings.UnleashNpcAsEnemy = toggle;
        }

        toggle = Main.Settings.EnableVariablePlaceholdersOnTexts;
        if (UI.Toggle(Gui.Localize("ModUi/&EnableVariablePlaceholdersOnTexts"), ref toggle))
        {
            Main.Settings.EnableVariablePlaceholdersOnTexts = toggle;
        }

        toggle = Main.Settings.EnableDungeonMakerModdedContent;
        if (UI.Toggle(Gui.Localize("ModUi/&EnableDungeonMakerModdedContent"), ref toggle))
        {
            Main.Settings.EnableDungeonMakerModdedContent = toggle;
        }

        UI.Label();

        UI.Label();
        UI.Label(Gui.Format("ModUi/&Translations"));
        UI.Label();

        using (UI.HorizontalScope())
        {
            UI.Label(Gui.Localize("ModUi/&TranslationService"), UI.Width(150f));

            var serviceIndex = (int)Main.Settings.SelectedTranslationService;

            if (UI.SelectionGrid(
                    ref serviceIndex,
                    TranslationServiceFactory.ServiceNames,
                    TranslationServiceFactory.ServiceNames.Length,
                    TranslationServiceFactory.ServiceNames.Length, UI.Width(300f)))
            {
                Main.Settings.SelectedTranslationService = (TranslationServiceType)serviceIndex;
            }
        }

        UI.Label();

        if (Main.Settings.SelectedTranslationService == TranslationServiceType.OpenAI)
        {
            DisplayOpenAISettings();
        }

        using (UI.HorizontalScope())
        {
            UI.Label(Gui.Localize("ModUi/&TargetLanguage"), UI.Width(150f));

            var intValue = Array.IndexOf(TranslatorContext.AvailableLanguages, Main.Settings.SelectedLanguageCode);

            if (UI.SelectionGrid(
                    ref intValue,
                    TranslatorContext.AvailableLanguages,
                    TranslatorContext.AvailableLanguages.Length,
                    10, UI.Width(600f)))
            {
                Main.Settings.SelectedLanguageCode = TranslatorContext.AvailableLanguages[intValue];
            }
        }

        UI.Label();

        var userCampaignPoolService = ServiceRepository.GetService<IUserCampaignPoolService>();

        foreach (var userCampaign in userCampaignPoolService.AllCampaigns
                     .Where(x => !x.TechnicalInfo.StartsWith(TranslatorContext.TranslatorBehaviour.UbTranslationTag))
                     .OrderBy(x => x.Author)
                     .ThenBy(x => x.Title))
        {
            var exportName = userCampaign.Title;

            using (UI.HorizontalScope())
            {
                string buttonLabel;

                UI.Label(
                    userCampaign.Author.Substring(0, Math.Min(20, userCampaign.Author.Length)).Bold().Orange(),
                    UI.Width(150f));
                UI.Label(userCampaign.Title.Bold().Italic(), UI.Width(400f));

                if (TranslatorContext.TranslatorBehaviour.CurrentExports.TryGetValue(exportName, out var status))
                {
                    buttonLabel = Gui.Format("ModUi/&TranslateCancel", status.LanguageCode.ToUpper(),
                        $"{status.PercentageComplete:00.0%}").Bold().Khaki();
                }
                else
                {
                    buttonLabel = Gui.Localize("ModUi/&Translate");
                }

                UI.ActionButton(buttonLabel, () =>
                    {
                        if (status == null)
                        {
                            TranslatorContext.TranslatorBehaviour.TranslateUserCampaign(
                                Main.Settings.SelectedLanguageCode, userCampaign.Title, userCampaign);
                        }
                        else
                        {
                            TranslatorContext.TranslatorBehaviour.Cancel(userCampaign.Title);
                        }
                    },
                    UI.Width(200f));
            }
        }

        UI.Label();
    }

    private static void DisplayOpenAISettings()
    {
        const float LabelWidth = 150f;
        const float InputWidth = 400f;
        const float ResetButtonWidth = 60f;

        if (!_apiKeyInitialized)
        {
            _openAIApiKeyInput = OpenAITranslationService.GetApiKey();
            _apiKeyInitialized = true;
        }

        if (UI.DisclosureToggle(Gui.Localize("ModUi/&OpenAISettings"), ref _showOpenAISettings, 200))
        {
            // Toggle state changed
        }

        if (!_showOpenAISettings)
        {
            return;
        }

        UI.Label();

        // Endpoint URL
        using (UI.HorizontalScope())
        {
            UI.Label(Gui.Localize("ModUi/&OpenAIEndpoint"), UI.Width(LabelWidth));

            var endpoint = Main.Settings.OpenAIEndpoint;
            UI.TextField(ref endpoint, null, UI.Width(InputWidth));

            if (endpoint != Main.Settings.OpenAIEndpoint)
            {
                Main.Settings.OpenAIEndpoint = endpoint;
            }

            UI.ActionButton(Gui.Localize("ModUi/&Reset"),
                () => Main.Settings.OpenAIEndpoint = OpenAITranslationService.DefaultEndpoint,
                UI.Width(ResetButtonWidth));
        }

        // API Key (stored in PlayerPrefs)
        using (UI.HorizontalScope())
        {
            UI.Label(Gui.Localize("ModUi/&OpenAIApiKey"), UI.Width(LabelWidth));

            UI.TextField(ref _openAIApiKeyInput, null, UI.Width(InputWidth));

            var currentApiKey = OpenAITranslationService.GetApiKey();

            if (_openAIApiKeyInput != currentApiKey)
            {
                OpenAITranslationService.SetApiKey(_openAIApiKeyInput);
            }

            UI.ActionButton(Gui.Localize("ModUi/&Reset"), () =>
            {
                _openAIApiKeyInput = string.Empty;
                OpenAITranslationService.ClearApiKey();
            }, UI.Width(ResetButtonWidth));
        }

        // Model
        using (UI.HorizontalScope())
        {
            UI.Label(Gui.Localize("ModUi/&OpenAIModel"), UI.Width(LabelWidth));

            var model = Main.Settings.OpenAIModel;
            UI.TextField(ref model, null, UI.Width(InputWidth));

            if (model != Main.Settings.OpenAIModel)
            {
                Main.Settings.OpenAIModel = model;
            }

            UI.ActionButton(Gui.Localize("ModUi/&Reset"),
                () => Main.Settings.OpenAIModel = OpenAITranslationService.DefaultModel,
                UI.Width(ResetButtonWidth));
        }

        UI.Label();

        // Advanced Settings
        if (UI.DisclosureToggle(Gui.Localize("ModUi/&OpenAIAdvancedSettings"), ref _showOpenAIAdvancedSettings, 200))
        {
            // Toggle state changed
        }

        if (!_showOpenAIAdvancedSettings)
        {
            UI.Label();
            return;
        }

        UI.Label();

        // Temperature
        var temperature = Main.Settings.OpenAITemperature;

        if (UI.Slider(Gui.Localize("ModUi/&OpenAITemperature"), ref temperature, 0f, 2f,
                OpenAITranslationService.DefaultTemperature, 2))
        {
            Main.Settings.OpenAITemperature = temperature;
        }

        // Top P
        var topP = Main.Settings.OpenAITopP;

        if (UI.Slider(Gui.Localize("ModUi/&OpenAITopP"), ref topP, 0f, 1f,
                OpenAITranslationService.DefaultTopP, 2))
        {
            Main.Settings.OpenAITopP = topP;
        }

        // Top K
        var topK = Main.Settings.OpenAITopK;

        if (UI.Slider(Gui.Localize("ModUi/&OpenAITopK"), ref topK, 0, 100,
                OpenAITranslationService.DefaultTopK))
        {
            Main.Settings.OpenAITopK = topK;
        }

        // System Prompt
        UI.Label();
        using (UI.HorizontalScope())
        {
            UI.Label(Gui.Localize("ModUi/&OpenAISystemPrompt"), UI.Width(LabelWidth));

            UI.ActionButton(Gui.Localize("ModUi/&Reset"),
                () => Main.Settings.OpenAISystemPrompt = OpenAITranslationService.DefaultSystemPrompt,
                UI.Width(ResetButtonWidth));
        }

        var systemPrompt = Main.Settings.OpenAISystemPrompt;
        systemPrompt = GUILayout.TextArea(systemPrompt, UI.Height(100f), UI.Width(600f));

        if (systemPrompt != Main.Settings.OpenAISystemPrompt)
        {
            Main.Settings.OpenAISystemPrompt = systemPrompt;
        }

        UI.Label();
        UI.Label(Gui.Localize("ModUi/&OpenAISystemPromptHelp"));

        UI.Label();
    }
}
