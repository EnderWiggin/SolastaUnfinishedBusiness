using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.UI.WebControls;
using HarmonyLib;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.Helpers;
using SolastaUnfinishedBusiness.Api.LanguageExtensions;
using SolastaUnfinishedBusiness.Builders;
using TA;
using UnityEngine;
using UnityEngine.UI;
using static Gui.LocalizationSpeakerGender;
using static RuleDefinitions;

namespace SolastaUnfinishedBusiness.Models;

internal static class ToolsContext
{
    internal const int GamePartySize = 4;

    internal const int MinPartySize = 1;
    internal const int MaxPartySize = 6;

    internal const float CustomScale = 0.85f;

    internal const int DungeonMinLevel = 1;
    internal const int DungeonMaxLevel = 20;

    private const RestActivityDefinition.ActivityCondition ActivityConditionDisabled =
        (RestActivityDefinition.ActivityCondition)(-1001);

    private const string RespecName = "RestActivityRespec";

    private static List<string> BuiltInHeroNames { get; } = [];

    private static RestActivityDefinition RestActivityRespec { get; } = RestActivityDefinitionBuilder
        .Create(RespecName)
        .SetGuiPresentation(Category.RestActivity)
        .SetRestData(
            RestDefinitions.RestStage.AfterRest,
            RestType.LongRest,
            RestActivityDefinition.ActivityCondition.None,
            RespecName,
            string.Empty)
        .AddToDB();

    internal static bool IsBuiltIn(string name)
    {
        return BuiltInHeroNames.Contains(name);
    }

    internal static void Load()
    {
        var gameBuiltInCharactersDirectory = TacticalAdventuresApplication.GameBuiltInCharactersDirectory;

        if (Directory.Exists(gameBuiltInCharactersDirectory))
        {
            BuiltInHeroNames.AddRange(Directory
                .GetFiles(gameBuiltInCharactersDirectory)
                .Select(x => Path
                    .GetFileName(x)
                    .Replace(".chr", "")));
        }

        ServiceRepository.GetService<IFunctorService>().RegisterFunctor(RespecName, new FunctorRespec());
        SwitchRespec();
        // SwitchEncounterPercentageChance();
    }

    internal static void SwitchRespec()
    {
        RestActivityRespec.condition = Main.Settings.EnableRespecAction
            ? RestActivityDefinition.ActivityCondition.None
            : ActivityConditionDisabled;
    }

#if false
    internal static void SwitchEncounterPercentageChance()
    {
        foreach (var travelEventProbabilityDescription in DatabaseRepository.GetDatabase<TravelActivityDefinition>()
                     .SelectMany(x => x.RandomEvents)
                     .Where(x => x.EventDefinition.Name == "TravelEventEncounter"))
        {
            travelEventProbabilityDescription.basePercent = Main.Settings.EncounterPercentageChance;
        }
    }
#endif

    public static void Rebase(Transform parent, int max)
    {
        while (Main.Settings.DefaultPartyHeroes.Count > max)
        {
            var heroToDelete = Main.Settings.DefaultPartyHeroes.ElementAt(0);

            var child = parent.FindChildRecursive(heroToDelete);

            if (child)
            {
                child.GetComponentInChildren<Toggle>().isOn = false;
            }
        }
    }

    public static Transform CreateHeroCheckbox(Component character)
    {
        // ReSharper disable once Unity.UnknownResource
        var settingCheckboxItem = Resources.Load<GameObject>("Gui/Prefabs/Modal/Setting/SettingCheckboxItem");
        var smallToggleNoFrame = settingCheckboxItem.transform.Find("SmallToggleNoFrame");
        var checkBox = UnityEngine.Object.Instantiate(smallToggleNoFrame, character.transform);
        var checkBoxRect = checkBox.GetComponent<RectTransform>();

        checkBox.name = "DefaultHeroToggle";
        checkBox.gameObject.SetActive(true);
        checkBox.Find("Background").gameObject.AddComponent<GuiTooltip>();

        checkBoxRect.anchoredPosition = new Vector2(160, 40);

        return checkBox;
    }

    internal static void Disable(RectTransform charactersTable)
    {
        for (var i = 0; i < charactersTable.childCount; i++)
        {
            var character = charactersTable.GetChild(i);
            var checkBoxToggle = character.GetComponentInChildren<Toggle>();

            if (checkBoxToggle)
            {
                checkBoxToggle.gameObject.SetActive(false);
            }
        }
    }

    internal sealed class FunctorRespec : Functor
    {
        private static readonly float STOP_ROUTINE = -1;
        private static readonly float RETRY_ROUTINE = 0.1f; // Interval of retry (second)

        internal static bool IsRespecing { get; private set; }
        internal static string OldHeroName { get; private set; }
        internal static RulesetCharacterHero OldHero { get; private set; }

        public override IEnumerator Execute(
            FunctorParametersDescription functorParameters, FunctorExecutionContext context)
        {
            var guiConsoleScreen = Gui.GuiService.GetScreen<GuiConsoleScreen>();
            var gameLocationScreenExploration = Gui.GuiService.GetScreen<GameLocationScreenExploration>();

            if (!guiConsoleScreen || !gameLocationScreenExploration)
            {
                yield break;
            }

            if (!gameLocationScreenExploration.Visible)
            {
                Gui.GuiService.ShowMessage(
                    MessageModal.Severity.Informative1,
                    "RestActivity/&RestActivityRespecTitle", "Message/&RespecMultiplayerAbortDescription",
                    "Message/&MessageOkTitle", string.Empty,
                    null, null);

                yield break;
            }

            IsRespecing = true;

            var characterBuildingService = ServiceRepository.GetService<ICharacterBuildingService>();
            OldHero = functorParameters.RestingHero;
            var newHero = characterBuildingService.CreateNewCharacter().HeroCharacter;

            // Register generating new guid, then copy basic stats for the character info panel
            newHero.Register(true);
            newHero.sex = OldHero.sex;
            newHero.SurName = OldHero.surName;
            newHero.Name = OldHero.name;
            newHero.raceDefinition = OldHero.raceDefinition;
            newHero.subRaceDefinition = OldHero.subRaceDefinition;
            foreach ( var ability in AttributeDefinitions.AbilityScoreNames ) { 
                if ( ! OldHero.TryGetAttribute( ability, out var oldScore ) || ! newHero.TryGetAttribute( ability, out var newScore ) ) continue;
                newScore.baseValue = oldScore.baseValue;
                newScore.Refresh();
            }

            OldHeroName = OldHero.Name;

            guiConsoleScreen.Hide(true);
            gameLocationScreenExploration.Hide(true);

            yield return StartRespec(newHero);

            if (IsRespecing)
            {
                FinalizeRespec(OldHero, newHero);
            }

            guiConsoleScreen.Show();
            gameLocationScreenExploration.Show();
        }

        private static IEnumerator StartRespec(RulesetCharacterHero hero)
        {
            var restModalScreen = Gui.GuiService.GetScreen<RestModal>();
            var characterCreationScreen = Gui.GuiService.GetScreen<CharacterCreationScreen>();

            restModalScreen.KeepCurrentState = true;
            restModalScreen.Hide(true);
            characterCreationScreen.OriginScreen = restModalScreen;
            characterCreationScreen.CurrentHero = hero;
            characterCreationScreen.Show();
            characterCreationScreen.CommonData.CharacterInfoLabel.gameObject.SetActive(false); // Hide "Character Info"
            if ( characterCreationScreen.StagePanelsByName[ "RaceSelection" ] is CharacterStageRaceSelectionPanel racePanel ) {
                RespecRaceStageEntered( characterCreationScreen, racePanel );
            }
            if ( characterCreationScreen.StagePanelsByName[ "ClassSelection" ] is CharacterStageClassSelectionPanel classPanel ) {
                classPanel.selectedClass = 1; // Reset to detect panel activation.
                RespecClassStageEntered( characterCreationScreen, classPanel );
            }
            if ( characterCreationScreen.StagePanelsByName[ "BackgroundSelection" ] is CharacterStageBackgroundSelectionPanel backgroundPanel ) {
                backgroundPanel.selectedBackground = 1; // Reset to detect panel activation.
                RespecBackgroundStageEntered( characterCreationScreen, backgroundPanel );
            }
            if ( characterCreationScreen.StagePanelsByName[ "AbilityScores" ] is CharacterStageAbilityScoresPanel abilityPanel ) {
                abilityPanel.currentMethod = CharacterStageAbilityScoresPanel.AbilityScoreMethod.DiceRolls;
                abilityPanel.abilityScoresRolled = false; // Reset to detect panel activation.
                RespecAbilityStageEntered( characterCreationScreen, abilityPanel );
            }
            if ( characterCreationScreen.StagePanelsByName[ "IdentityDefinition" ] is CharacterStageIdentityDefinitionPanel idPanel ) {
                idPanel.refreshingNames = true; // Set to detect panel activation.
                RespecIdStageEntered( characterCreationScreen, idPanel);
            }

            while (characterCreationScreen.currentHero != null)
            {
                yield return null;
            }

            characterCreationScreen.Hide();
            characterCreationScreen.RestoreOriginScreen();
            restModalScreen.Refresh();
            IsRespecing = !hero.TryGetHeroBuildingData(out _);
        }

        private static void RespecRaceStageEntered ( MonoBehaviour parent, CharacterStageRaceSelectionPanel panel ) {
            parent.StartCoroutine( WaitAndDo( 0.1f, () => {
                var race = OldHero.RaceDefinition;
                var raceIndex = panel.eligibleRaces.IndexOf( race );
                Main.Info( $"Respec [{OldHeroName}] Assign original race {race.Name}, index {raceIndex}" );
                if ( ! IsRespecing || raceIndex < 0 ) return;
                panel.selectedRace = raceIndex;
                if ( race.IsContentAvailable ) {
                    var subraceIndex = panel.sortedSubRaces[ race ]?.IndexOf(OldHero.SubRaceDefinition ) ?? -1;
                    if ( subraceIndex >= 0 )
                        panel.selectedSubRace[ raceIndex ] = subraceIndex;
                }
                panel.Refresh();
                panel.CommonData.CharacterPlate.RefreshNameAndRace();
                panel.CommonData.CharacterInfoLabel.gameObject.SetActive( false ); // Hide "Character Info"
            } ) );
        }

        private static void RespecClassStageEntered ( MonoBehaviour parent, CharacterStageClassSelectionPanel panel ) {
            parent.StartIntervalCoroutine( () => {
                if ( !IsRespecing) return STOP_ROUTINE;
                if ( panel.selectedClass == 1 || panel.classesTable.childCount <= 0 ) return RETRY_ROUTINE;
                var firstClass = OldHero.ClassesHistory.FirstOrDefault();
                var index = panel.compatibleClasses.IndexOf( firstClass );
                Main.Info( $"Respec [{OldHeroName}] Assign original class {firstClass.Name}, index {index}" );
                if ( index < 0 ) return STOP_ROUTINE;
                panel.selectedClass = index;
                panel.RefreshNow();
                return STOP_ROUTINE;
            } );
        }

        private static void RespecBackgroundStageEntered ( MonoBehaviour parent, CharacterStageBackgroundSelectionPanel panel ) {
            parent.StartIntervalCoroutine( () => {
                if ( ! IsRespecing ) return STOP_ROUTINE;
                if ( panel.selectedBackground == 1 || panel.backgroundsTable.childCount <= 0 ) return RETRY_ROUTINE;
                var backrgound = OldHero.BackgroundDefinition;
                var index = panel.compatibleBackgrounds.IndexOf( backrgound );
                Main.Info( $"Respec [{OldHeroName}] Assign original background {backrgound.Name}, index {index}" );
                if ( index < 0 ) return STOP_ROUTINE;
                panel.selectedBackground = index;
                panel.selectedBackgroundPersonalityFlagsMap[ backrgound ].Clear();
                panel.selectedBackgroundPersonalityFlagsMap[ backrgound ].AddRange( OldHero.BackgroundOptionalPersonalityFlags );
                panel.selectedAlignmentPersonalityFlags.Clear();
                panel.selectedAlignmentPersonalityFlags.AddRange( OldHero.AlignmentOptionaPersonalityFlags );
                panel.Refresh();
                return STOP_ROUTINE;
            } );
        }

        private static void RespecAbilityStageEntered ( MonoBehaviour parent, CharacterStageAbilityScoresPanel panel ) {
            parent.StartIntervalCoroutine( () => {
                if ( !IsRespecing ) return STOP_ROUTINE;
                if ( ! panel.abilityScoresRolled ) return RETRY_ROUTINE;
                var abilityCount = AttributeDefinitions.AbilityScoreNames.Length;
                var sortedScores = AttributeDefinitions.AbilityScoreNames.Select( e => OldHero.GetAttribute( e, true )?.baseValue ?? 10 ).ToArray();
                Array.Sort( sortedScores );
                Array.Reverse( sortedScores );
                Main.Info( $"Respec [{OldHeroName}] Assign original ability scores {string.Join( ", ", sortedScores )}" );
                panel.rollValues.SetRange( sortedScores );
                var mainClass = panel.guiCharacter.MainClassDefinition;
                if ( mainClass.AbilityScoresPriority != null && mainClass.AbilityScoresPriority.Count == abilityCount ) {
                    for ( int i = 0 ; i < abilityCount ; i++ ) {
                        // Assign score
                        var assignedBox = panel.abilityScoresTable.GetChild( i ).GetComponent< AbilityRollBox >();
                        var abilityScore = assignedBox.AbilityScore;
                        abilityScore.BaseValue = OldHero.Attributes[ abilityScore.Name ].BaseValue;
                        abilityScore.Refresh( false );
                        var isImportant = mainClass.AbilityScoresPriority.IndexOf( abilityScore.Name ) < 3;
                        // Find index of roll and bind both boxes.
                        var scoreIndex = Array.IndexOf( sortedScores, abilityScore.BaseValue );
                        if ( scoreIndex < 0 ) scoreIndex = sortedScores.ToList().FindIndex( e => e > 0 );
                        sortedScores[ scoreIndex ] = 0;
                        var rollBox = panel.diceRollsTable.GetChild( scoreIndex ).GetComponent< AbilityRollBox >();
                        rollBox.Bind( scoreIndex, 0, null );
                        assignedBox.Bind( abilityScore, scoreIndex, abilityScore.CurrentValue, isImportant, new AbilityValueCell.DragStartedHandler( panel.OnScoreBoxDragStarted ) );
                    }
                }
                panel.Refresh();
                return STOP_ROUTINE;
            } );
        }

        private static void RespecIdStageEntered ( MonoBehaviour parent, CharacterStageIdentityDefinitionPanel panel ) {
            parent.StartIntervalCoroutine( () => {
                if ( ! IsRespecing ) return STOP_ROUTINE;
                if ( panel.refreshingNames ) return RETRY_ROUTINE;
                Main.Info( $"Respec [{OldHeroName}] Assign original identity" );
                panel.firstNameInputField.text = panel.currentHero.Name = OldHero.name;
                panel.lastNameInputField.text = panel.currentHero.SurName = OldHero.surName;
                panel.backstoryInputField.text = OldHero.additionalBackstory;

                var morphOptions = panel.customizationOptionsBySelectedOrigin[ panel.selectedOptionsByPart[ MorphotypeElementDefinition.ElementCategory.Origin ] ];
                var oldMorphs = OldHero.MorphotypeElements;
                for ( int i = 0 ; i < panel.modifiers.Count ; i++ ) {
                    var modifier = panel.modifiers[ i ];
                    if ( ! modifier.gameObject.activeSelf ) continue;
                    var category = MorphotypeElementDefinition.CommonCategories[ i ];
                    if ( ! oldMorphs.TryGetValue( category, out var morphText ) ) continue;
                    // Sliding Values
                    if ( modifier.sliderGroup.gameObject.activeSelf 
                            && OldHero.MorphotypeElementAdditionalValues.TryGetValue( category, out var num ) ) {
                        //Main.Info( category.ToString() + " = " + num );
                        modifier.slider.value = num;
                        modifier.OnSliderCb();
                        modifier.OnSliderDragEndCb();
                        //panel.OnCustomizationAdditionalValueChanged( category, num );
                        continue;
                    }
                    // Non-sliding values: colours, images (), or toggle
                    var morphIndex = morphOptions[ category ].IndexOf( morphText );
                    //Main.Info( category.ToString() + " = " + morphText + ", Index " + morphIndex );
                    if ( morphIndex < 0 ) continue;
                    if ( modifier.colorsGroup.gameObject.activeSelf ) {
                        modifier.OnColorSelected( morphIndex );
                    } else if ( morphIndex < modifier.valuesList.Count ) {
                        if ( modifier.selectorGroup.gameObject.activeSelf || modifier.gamepadSelector.gameObject.activeSelf )
                            while ( modifier.currentValue != morphIndex )
                                modifier.OnSelectNext();
                        else if ( modifier.imagesGroup.gameObject.activeSelf )
                            modifier.OnImageSelected( morphIndex );
                        else
                            Main.Error( $"Respec [{OldHeroName}] Morph control not implemented: {category}" );
                    }
                    //panel.OnCustomizationChanged( category, morphIndex );
                }

                var voiceIndex = panel.compatibleVoices.FindIndex( e => string.Equals( e.Name, OldHero.voiceID ) );
                if ( voiceIndex >= 0 )
                    panel.voiceModifier.OnLabelSelected( voiceIndex );

                var pronounIndex = new List<Gui.LocalizationSpeakerGender>{ Female, Male, NonBinary }.IndexOf( OldHero.pronoun );
                if ( pronounIndex >= 0 )
                    panel.pronounModifier.OnLabelSelected( pronounIndex );

                panel.CommonData.CharacterPlate.CommonBind( new GuiCharacter( panel.currentHero ) );
                panel.Refresh();
                return STOP_ROUTINE;
            } );
        }

        //TODO: prefer to use below code once I identify why RESPEC saves break on load
#if false
        private static void FinalizeRespec(
            [NotNull] RulesetCharacterHero oldHero,
            [NotNull] RulesetCharacterHero newHero)
        {
            //Init RESPEC
            var locationManager =
                ServiceRepository.GetService<IGameLocationService>() as GameLocationManager;
            var characterManager =
                ServiceRepository.GetService<IGameLocationCharacterService>() as GameLocationCharacterManager;
            var entityFactoryManager =
                ServiceRepository.GetService<IWorldLocationEntityFactoryService>() as
                    WorldLocationEntityFactoryManager;

            if (!characterManager || !entityFactoryManager)
            {
                IsRespecing = false;

                return;
            }

            var oldCharacter = GameLocationCharacter.GetFromActor(oldHero);
            var oldExperience = oldHero.GetAttribute(AttributeDefinitions.Experience);
            var oldGuid = oldHero.Guid;
            var newGuid = newHero.Guid;

            //Terminate all effects started by old character
            EffectHelpers.GetAllEffectsBySourceGuid(oldGuid).ForEach(e => e.DoTerminate(oldHero));

            //Replace source for all effects of new character
            EffectHelpers.GetAllEffectsBySourceGuid(newGuid).ForEach(e => e.SetGuid(oldGuid));

            //Replace source for all conditions of new character
            EffectHelpers.GetAllConditionsBySourceGuid(newGuid).ForEach(c => c.sourceGuid = oldGuid);

            //Unregister under new guid and assign older hero guid
            newHero.Unregister();

            //Create character will register new hero with oldGuid later on
            ServiceRepository.GetService<IRulesetEntityService>().SwapEntities(oldHero, newHero);

            //Copy tags and campaign stats
            newHero.Tags.AddRange(oldHero.Tags);
            newHero.Attributes[AttributeDefinitions.Experience] = oldExperience;
            newHero.criticalHits = oldHero.criticalHits;
            newHero.criticalFailures = oldHero.criticalFailures;
            newHero.inflictedDamage = oldHero.inflictedDamage;
            newHero.slainEnemies = oldHero.slainEnemies;
            newHero.sustainedInjuries = oldHero.sustainedInjuries;
            newHero.restoredHealth = oldHero.restoredHealth;
            newHero.usedMagicAndPowers = oldHero.usedMagicAndPowers;
            newHero.knockOuts = oldHero.knockOuts;

            //Handle conditions
            TransferConditionsOfCategory(oldHero, newHero, AttributeDefinitions.TagEffect);
            CleanupOldHeroConditions(oldHero, newHero);

            //Handle inventory
            //DropInventoryOnFloor(oldCharacter);

            //Create new character, spawn it, replace, and destroy old one
            var newCharacter = characterManager.CreateCharacter(oldCharacter.ControllerId, newHero, Side.Ally);

            TransferRelevantPersistentData(oldCharacter, newCharacter);
            entityFactoryManager.SpawnCharacter(newCharacter);
            entityFactoryManager.FinalizeSpawnCharacter(newCharacter);
            characterManager.ReplaceCharacter(oldCharacter, newCharacter);
            characterManager.RemoveCharacterFromTheGame(oldCharacter);
            characterManager.KillCharacter(oldCharacter, true, true, true, true);
            //entityFactoryManager.DestroyCharacter(oldCharacter);
            
            //Update game campaign party
            var gameCampaignCharacters = Gui.GameCampaign.Party.CharactersList;

            gameCampaignCharacters.Find(x => x.RulesetCharacter == oldHero).RulesetCharacter = newHero;

            //Finalize RESPEC
            UpdateRestPanelUi(gameCampaignCharacters);

            Gui.GuiService.ShowMessage(
                MessageModal.Severity.Informative1,
                "RestActivity/&RestActivityRespecTitle", "Message/&RespecSuccessfulDescription",
                "Message/&MessageOkTitle", string.Empty,
                null, null);

            IsRespecing = false;
        }
        
                
        private static void TransferRelevantPersistentData(
            GameLocationCharacter oldCharacter, GameLocationCharacter newCharacter)
        {
            newCharacter.ControllerId = oldCharacter.ControllerId;
            newCharacter.Orientation = oldCharacter.Orientation;
            newCharacter.LocationPosition = oldCharacter.LocationPosition;
            newCharacter.PerceptionState = oldCharacter.PerceptionState;
            newCharacter.ContextualFormation = oldCharacter.ContextualFormation;
        }
#endif

        private static void FinalizeRespec(
            [NotNull] RulesetCharacterHero oldHero,
            [NotNull] RulesetCharacterHero newHero)
        {
            var tags = oldHero.Tags;
            var experience = oldHero.GetAttribute(AttributeDefinitions.Experience);
            var gameCampaignCharacters = Gui.GameCampaign.Party.CharactersList;
            var characterManager =
                ServiceRepository.GetService<IGameLocationCharacterService>() as GameLocationCharacterManager;

            if (characterManager)
            {
                var gameLocationCharacter = GameLocationCharacter.GetFromActor(oldHero);

                var oldGuid = oldHero.Guid;
                var newGuid = newHero.Guid;

                //Terminate all effects started by old character
                EffectHelpers.GetAllEffectsBySourceGuid(oldGuid).ForEach(e => e.DoTerminate(oldHero));
                //Replace source for all effects of new character
                EffectHelpers.GetAllEffectsBySourceGuid(newGuid).ForEach(e => e.SetGuid(oldGuid));
                //Replace source for all conditions of new character
                EffectHelpers.GetAllConditionsBySourceGuid(newGuid).ForEach(c => c.sourceGuid = oldGuid);

                newHero.Unregister(); //unregister under new guid
                //Replace old character with new
                ServiceRepository.GetService<IRulesetEntityService>().SwapEntities(oldHero, newHero);
                newHero.Register(false); //register again under old guid

                newHero.Tags.AddRange(tags);
                newHero.Attributes[AttributeDefinitions.Experience] = experience;
                newHero.criticalHits = oldHero.criticalHits;
                newHero.criticalFailures = oldHero.criticalFailures;
                newHero.inflictedDamage = oldHero.inflictedDamage;
                newHero.slainEnemies = oldHero.slainEnemies;
                newHero.sustainedInjuries = oldHero.sustainedInjuries;
                newHero.restoredHealth = oldHero.restoredHealth;
                newHero.usedMagicAndPowers = oldHero.usedMagicAndPowers;
                newHero.knockOuts = oldHero.knockOuts;

                TransferConditionsOfCategory(oldHero, newHero, AttributeDefinitions.TagEffect);
                CleanupOldHeroConditions(oldHero, newHero);

                DropInventoryOnFloor(gameLocationCharacter);

                gameCampaignCharacters.Find(x => x.RulesetCharacter == oldHero).RulesetCharacter = newHero;

                UpdateRestPanelUi(gameCampaignCharacters);

                gameLocationCharacter.SetRuleset(newHero);

                var worldLocationEntityFactoryService =
                    ServiceRepository.GetService<IWorldLocationEntityFactoryService>();

                if (worldLocationEntityFactoryService.TryFindWorldCharacter(gameLocationCharacter,
                        out var worldLocationCharacter))
                {
                    worldLocationCharacter.GraphicsCharacter.RulesetCharacter = newHero;
                }

                characterManager.dirtyParty = true;
                characterManager.RefreshAllCharacters();
            }

            Gui.GuiService.ShowMessage(
                MessageModal.Severity.Informative1,
                "RestActivity/&RestActivityRespecTitle", "Message/&RespecSuccessfulDescription",
                "Message/&MessageOkTitle", string.Empty,
                null, null);

            IsRespecing = false;
        }

        private static void TransferConditionsOfCategory(RulesetActor oldHero, RulesetActor newHero, string category)
        {
            if (!oldHero.ConditionsByCategory.TryGetValue(category, out var conditions))
            {
                return;
            }

            newHero.AddConditionCategoryAsNeeded(category);
            newHero.AllConditions.AddRange(conditions);
            newHero.ConditionsByCategory[category].AddRange(conditions);
        }

        private static void CleanupOldHeroConditions(RulesetCharacterHero oldHero, RulesetCharacterHero newHero)
        {
            //Unregister all conditions that are not present in new hero
            oldHero.ConditionsByCategory
                .SelectMany(x => x.Value)
                .Where(c => !newHero.ConditionsByCategory.SelectMany(x => x.Value).Contains(c))
                .ToArray()
                .Do(c => c.Unregister());
            oldHero.AllConditions.Clear();
            oldHero.ConditionsByCategory.Clear();
        }

        private static void DropInventoryOnFloor([NotNull] GameLocationCharacter oldCharacter)
        {
            var oldHero = oldCharacter.RulesetCharacter;
            var position = oldCharacter.LocationPosition;
            var personalSlots = oldHero.CharacterInventory.PersonalContainer.InventorySlots;
            var slotsByName = oldHero.CharacterInventory.InventorySlotsByName;

            foreach (var equipedItem in personalSlots.Select(i => i.EquipedItem).Where(i => i != null))
            {
                DropItem(equipedItem, position);
            }

            foreach (var equipedItem in slotsByName.Select(s => s.Value.EquipedItem).Where(i => i != null))
            {
                DropItem(equipedItem, position);
            }
        }

        private static void DropItem(RulesetItem equipedItem, int3 position)
        {
            var inventoryCommandService = ServiceRepository.GetService<IInventoryCommandService>();

            equipedItem.AttunedToCharacter = string.Empty;

            if (equipedItem is RulesetItemSpellbook spellbook)
            {
                foreach (var scrollDefinition in spellbook.ScribedSpells
                             .Select(spellDefinition =>
                                 DatabaseRepository.GetDatabase<ItemDefinition>()
                                     .FirstOrDefault(item =>
                                         item.IsUsableDevice &&
                                         item.UsableDeviceDescription.UsableDeviceTags.Contains("Scroll") &&
                                         item.UsableDeviceDescription.DeviceFunctions.Any(function =>
                                             function.SpellDefinition == spellDefinition)))
                             .Where(scrollDefinition => scrollDefinition))
                {
                    var rulesetItem = new RulesetItem(scrollDefinition);

                    inventoryCommandService.CreateItemAtPosition(rulesetItem, position);
                }
            }
            else
            {
                inventoryCommandService.CreateItemAtPosition(equipedItem, position);
            }
        }

        private static void UpdateRestPanelUi(List<GameCampaignCharacter> gameCampaignCharacters)
        {
            var restModalScreen = Gui.GuiService.GetScreen<RestModal>();
            var restAfterPanel = restModalScreen.restAfterPanel;
            var characterPlatesTable = restAfterPanel.characterPlatesTable;

            for (var index = 0; index < characterPlatesTable.childCount; ++index)
            {
                var child = characterPlatesTable.GetChild(index);
                var component = child.GetComponent<CharacterPlateGame>();

                component.Unbind();

                if (index < gameCampaignCharacters.Count)
                {
                    component.Bind(gameCampaignCharacters[index].RulesetCharacter,
                        TooltipDefinitions.AnchorMode.BOTTOM_CENTER);
                    component.Refresh();
                }

                child.gameObject.SetActive(index < gameCampaignCharacters.Count);
            }
        }
    }

    private static void StartIntervalCoroutine ( this MonoBehaviour coroutineObject, Func<float> actionReturnNextInterval )
    {
        var nextInterval = actionReturnNextInterval();
        if ( nextInterval < 0 ) return;
        coroutineObject.StartCoroutine( WaitAndDo( nextInterval, () => StartIntervalCoroutine( coroutineObject, actionReturnNextInterval ) ) );
    }

    private static IEnumerator WaitAndDo ( float waitSeconds, Action action )
    {
        yield return new WaitForSeconds(waitSeconds);
        action();
    }

}
