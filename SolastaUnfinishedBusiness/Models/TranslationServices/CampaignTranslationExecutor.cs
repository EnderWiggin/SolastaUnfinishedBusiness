using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using SolastaUnfinishedBusiness.Api.LanguageExtensions;
using UnityEngine;

namespace SolastaUnfinishedBusiness.Models.TranslationServices;

internal sealed class CampaignTranslationExecutor : MonoBehaviour
{
    internal const string UbTranslationTag = "UB auto translation\n";

    internal const int MaxTranslationConcurrency = 40;

    private static CampaignTranslationExecutor _instance;
    private static readonly ConcurrentDictionary<string, string> TranslationsCache = new();
    private static readonly ConcurrentDictionary<string, CampaignTranslationTask> ActiveTasks = new();

    private readonly ConcurrentQueue<Action> _mainThreadActions = new();

    [NotNull]
    internal static CampaignTranslationExecutor Instance
    {
        get
        {
            if (_instance)
            {
                return _instance;
            }

            _instance = new GameObject("CampaignTranslationExecutor").AddComponent<CampaignTranslationExecutor>();
            DontDestroyOnLoad(_instance.gameObject);

            return _instance;
        }
    }

    internal static IReadOnlyDictionary<string, CampaignTranslationTask> Tasks => ActiveTasks;

    private void Update()
    {
        // Process main thread actions
        while (_mainThreadActions.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Main.Error($"Error executing main thread action: {ex.Message}");
            }
        }
    }

    internal static CampaignTranslationTask StartTranslation(
        string languageCode,
        [NotNull] string campaignTitle,
        [NotNull] UserCampaign userCampaign)
    {
        if (ActiveTasks.ContainsKey(campaignTitle))
        {
            Main.Info($"Translation task for '{campaignTitle}' already exists.");
            return ActiveTasks[campaignTitle];
        }

        var newUserCampaign = userCampaign.DeepCopy();
        var oldUserCampaignTitle = Regex.Replace(userCampaign.Title, @"\[.+\] - (.*)", "$1");
        newUserCampaign.Title = $"[{languageCode}] - {oldUserCampaignTitle}";
        newUserCampaign.IsWorkshopItem = false;

        var task = new CampaignTranslationTask(campaignTitle, languageCode);
        ActiveTasks[campaignTitle] = task;

        // Collect all translation items
        CollectTranslationItems(task, newUserCampaign);

        // Start async translation
        _ = ExecuteTranslationAsync(task, newUserCampaign);

        return task;
    }

    internal static CampaignTranslationTask GetTask(string campaignTitle)
    {
        return ActiveTasks.TryGetValue(campaignTitle, out var task) ? task : null;
    }

    internal static void CancelTask(string campaignTitle)
    {
        if (!ActiveTasks.TryGetValue(campaignTitle, out var task))
        {
            return;
        }

        task.Cancel();
        ActiveTasks.TryRemove(campaignTitle, out _);
    }

    internal static void PauseTask(string campaignTitle)
    {
        if (ActiveTasks.TryGetValue(campaignTitle, out var task))
        {
            task.Pause();
        }
    }

    internal static void ResumeTask(string campaignTitle)
    {
        if (ActiveTasks.TryGetValue(campaignTitle, out var task))
        {
            task.Resume();
        }
    }

    internal static void RetryFailedItems(string campaignTitle, UserCampaign userCampaign)
    {
        if (!ActiveTasks.TryGetValue(campaignTitle, out var task))
        {
            return;
        }

        if (task.Status is CampaignTranslationStatus.Running or CampaignTranslationStatus.Paused)
        {
            return;
        }

        task.ResetFailedItems();
        task.Status = CampaignTranslationStatus.Running;
        task.RecreateCancellationTokenSource();

        var newUserCampaign = userCampaign.DeepCopy();
        _ = ExecuteTranslationAsync(task, newUserCampaign, retryFailedOnly: true);
    }

    private static void CollectTranslationItems(CampaignTranslationTask task, UserCampaign userCampaign)
    {
        // Campaign description
        if (!string.IsNullOrEmpty(userCampaign.Description))
        {
            task.AddItem(new TranslationItem(
                "Campaign",
                "Description",
                userCampaign.Description,
                translated => userCampaign.Description = translated));
        }

        if (!string.IsNullOrEmpty(userCampaign.TechnicalInfo))
        {
            task.AddItem(new TranslationItem(
                "Campaign",
                "TechnicalInfo",
                userCampaign.TechnicalInfo,
                translated => userCampaign.TechnicalInfo = UbTranslationTag + translated));
        }

        // USER LOCATIONS
        foreach (var userLocation in userCampaign.UserLocations)
        {
            if (!string.IsNullOrEmpty(userLocation.Description))
            {
                var location = userLocation;
                task.AddItem(new TranslationItem(
                    "Locations",
                    $"{location.Title}_Description",
                    location.Description,
                    translated => location.Description = translated));
            }

            foreach (var gadgetKvp in userLocation.GadgetsByName)
            {
                var gadgetName = gadgetKvp.Key;
                var gadget = gadgetKvp.Value;
                foreach (var parameterValue in gadget.ParameterValues)
                {
                    var pv = parameterValue;
                    switch (pv.GadgetParameterDescription.Name)
                    {
                        case "Speech":
                        case "Banter":
                        case "BanterLines":
                        case "ExitLore":
                            if (!string.IsNullOrEmpty(pv.StringValue))
                            {
                                task.AddItem(new TranslationItem(
                                    "Gadgets",
                                    $"{gadgetName}_{pv.GadgetParameterDescription.Name}",
                                    pv.StringValue,
                                    translated => pv.StringValue = translated));
                            }

                            for (var i = 0; i < pv.StringsList.Count; i++)
                            {
                                var index = i;
                                var original = pv.StringsList[i];
                                if (!string.IsNullOrEmpty(original))
                                {
                                    task.AddItem(new TranslationItem(
                                        "Gadgets",
                                        $"{gadgetName}_{pv.GadgetParameterDescription.Name}_{i}",
                                        original,
                                        translated => pv.StringsList[index] = translated));
                                }
                            }

                            break;

                        case "LocationsList":
                            foreach (var destination in pv.DestinationsList)
                            {
                                var dest = destination;
                                if (!string.IsNullOrEmpty(dest.DisplayedTitle))
                                {
                                    task.AddItem(new TranslationItem(
                                        "Gadgets",
                                        $"{gadgetName}_Destination_{dest.DisplayedTitle}",
                                        dest.DisplayedTitle,
                                        translated => dest.DisplayedTitle = translated));
                                }
                            }

                            break;
                    }
                }
            }
        }

        // USER DIALOGS
        foreach (var dialog in userCampaign.UserDialogs)
        {
            var d = dialog;
            if (!string.IsNullOrEmpty(d.Title))
            {
                task.AddItem(new TranslationItem(
                    "Dialogs",
                    $"{d.Title}_Title",
                    d.Title,
                    translated => d.Title = translated));
            }

            if (!string.IsNullOrEmpty(d.Description))
            {
                task.AddItem(new TranslationItem(
                    "Dialogs",
                    $"{d.Title}_Description",
                    d.Description,
                    translated => d.Description = translated));
            }

            foreach (var userDialogState in d.AllDialogStates
                         .Where(x => x.Type is "AnswerChoice" or "CharacterSpeech" or "NpcSpeech"))
            {
                foreach (var dialogLine in userDialogState.DialogLines)
                {
                    var line = dialogLine;
                    if (!string.IsNullOrEmpty(line.TextLine))
                    {
                        task.AddItem(new TranslationItem(
                            "Dialogs",
                            $"{d.Title}_{userDialogState.Type}_{line.TextLine.GetHashCode()}",
                            line.TextLine,
                            translated => line.TextLine = translated));
                    }
                }
            }
        }

        // USER ITEMS
        foreach (var item in userCampaign.UserItems)
        {
            var i = item;
            if (!string.IsNullOrEmpty(i.Title))
            {
                task.AddItem(new TranslationItem(
                    "Items",
                    $"{i.Title}_Title",
                    i.Title,
                    translated => i.Title = translated));
            }

            if (!string.IsNullOrEmpty(i.Description))
            {
                task.AddItem(new TranslationItem(
                    "Items",
                    $"{i.Title}_Description",
                    i.Description,
                    translated => i.Description = translated));
            }

            for (var idx = 0; idx < i.DocumentFragments.Count; idx++)
            {
                var fragmentIndex = idx;
                var fragment = i.DocumentFragments[idx];
                if (!string.IsNullOrEmpty(fragment))
                {
                    task.AddItem(new TranslationItem(
                        "Items",
                        $"{i.Title}_Fragment_{idx}",
                        fragment,
                        translated => i.DocumentFragments[fragmentIndex] = translated));
                }
            }
        }

        // USER QUESTS
        foreach (var quest in userCampaign.UserQuests)
        {
            var q = quest;
            if (!string.IsNullOrEmpty(q.Title))
            {
                task.AddItem(new TranslationItem(
                    "Quests",
                    $"{q.Title}_Title",
                    q.Title,
                    translated => q.Title = translated));
            }

            if (!string.IsNullOrEmpty(q.Description))
            {
                task.AddItem(new TranslationItem(
                    "Quests",
                    $"{q.Title}_Description",
                    q.Description,
                    translated => q.Description = translated));
            }

            foreach (var userQuestStep in q.AllQuestStepDescriptions)
            {
                var step = userQuestStep;
                if (!string.IsNullOrEmpty(step.Title))
                {
                    task.AddItem(new TranslationItem(
                        "Quests",
                        $"{q.Title}_{step.Title}_Title",
                        step.Title,
                        translated => step.Title = translated));
                }

                if (!string.IsNullOrEmpty(step.Description))
                {
                    task.AddItem(new TranslationItem(
                        "Quests",
                        $"{q.Title}_{step.Title}_Description",
                        step.Description,
                        translated => step.Description = translated));
                }

                foreach (var outcome in step.OutcomesTable)
                {
                    var o = outcome;
                    if (!string.IsNullOrEmpty(o.DescriptionText))
                    {
                        task.AddItem(new TranslationItem(
                            "Quests",
                            $"{q.Title}_{step.Title}_Outcome_{o.DescriptionText.GetHashCode()}",
                            o.DescriptionText,
                            translated => o.DescriptionText = translated));
                    }
                }
            }
        }

        // USER MONSTERS
        foreach (var monster in userCampaign.UserMonsters)
        {
            var m = monster;
            if (!string.IsNullOrEmpty(m.Title))
            {
                task.AddItem(new TranslationItem(
                    "Monsters",
                    $"{m.Title}_Title",
                    m.Title,
                    translated => m.Title = translated));
            }

            if (!string.IsNullOrEmpty(m.Description))
            {
                task.AddItem(new TranslationItem(
                    "Monsters",
                    $"{m.Title}_Description",
                    m.Description,
                    translated => m.Description = translated));
            }
        }

        // USER NPCs
        foreach (var npc in userCampaign.UserNpcs)
        {
            var n = npc;
            if (!string.IsNullOrEmpty(n.Title))
            {
                task.AddItem(new TranslationItem(
                    "NPCs",
                    $"{n.Title}_Title",
                    n.Title,
                    translated => n.Title = translated));
            }

            if (!string.IsNullOrEmpty(n.Description))
            {
                task.AddItem(new TranslationItem(
                    "NPCs",
                    $"{n.Title}_Description",
                    n.Description,
                    translated => n.Description = translated));
            }
        }

        // USER MERCHANT INVENTORIES
        foreach (var merchantInventory in userCampaign.UserMerchantInventories)
        {
            var mi = merchantInventory;
            if (!string.IsNullOrEmpty(mi.Title))
            {
                task.AddItem(new TranslationItem(
                    "Merchants",
                    $"{mi.Title}_Title",
                    mi.Title,
                    translated => mi.Title = translated));
            }

            if (!string.IsNullOrEmpty(mi.Description))
            {
                task.AddItem(new TranslationItem(
                    "Merchants",
                    $"{mi.Title}_Description",
                    mi.Description,
                    translated => mi.Description = translated));
            }
        }

        // USER LOOT PACKS
        foreach (var lootPack in userCampaign.UserLootPacks)
        {
            var lp = lootPack;
            if (!string.IsNullOrEmpty(lp.Title))
            {
                task.AddItem(new TranslationItem(
                    "LootPacks",
                    $"{lp.Title}_Title",
                    lp.Title,
                    translated => lp.Title = translated));
            }

            if (!string.IsNullOrEmpty(lp.Description))
            {
                task.AddItem(new TranslationItem(
                    "LootPacks",
                    $"{lp.Title}_Description",
                    lp.Description,
                    translated => lp.Description = translated));
            }
        }

        // USER BIOMES
        foreach (var userBiome in userCampaign.userBiomes)
        {
            var b = userBiome;
            if (!string.IsNullOrEmpty(b.Title))
            {
                task.AddItem(new TranslationItem(
                    "Biomes",
                    $"{b.Title}_Title",
                    b.Title,
                    translated => b.Title = translated));
            }

            if (!string.IsNullOrEmpty(b.Description))
            {
                task.AddItem(new TranslationItem(
                    "Biomes",
                    $"{b.Title}_Description",
                    b.Description,
                    translated => b.Description = translated));
            }

            for (var idx = 0; idx < b.NarrativeEventBasicLines.Count; idx++)
            {
                var lineIndex = idx;
                var narrativeLine = b.NarrativeEventBasicLines[idx];
                if (!string.IsNullOrEmpty(narrativeLine))
                {
                    task.AddItem(new TranslationItem(
                        "Biomes",
                        $"{b.Title}_Narrative_{idx}",
                        narrativeLine,
                        translated => b.NarrativeEventBasicLines[lineIndex] = translated));
                }
            }
        }

        // USER ENCOUNTER TABLES
        foreach (var userEncounterTable in userCampaign.userEncounterTables)
        {
            var et = userEncounterTable;
            if (!string.IsNullOrEmpty(et.Title))
            {
                task.AddItem(new TranslationItem(
                    "EncounterTables",
                    $"{et.Title}_Title",
                    et.Title,
                    translated => et.Title = translated));
            }

            if (!string.IsNullOrEmpty(et.Description))
            {
                task.AddItem(new TranslationItem(
                    "EncounterTables",
                    $"{et.Title}_Description",
                    et.Description,
                    translated => et.Description = translated));
            }
        }

        // USER ENCOUNTERS
        foreach (var userEncounter in userCampaign.userEncounters)
        {
            var e = userEncounter;
            if (!string.IsNullOrEmpty(e.Title))
            {
                task.AddItem(new TranslationItem(
                    "Encounters",
                    $"{e.Title}_Title",
                    e.Title,
                    translated => e.Title = translated));
            }

            if (!string.IsNullOrEmpty(e.Description))
            {
                task.AddItem(new TranslationItem(
                    "Encounters",
                    $"{e.Title}_Description",
                    e.Description,
                    translated => e.Description = translated));
            }
        }

        // USER CAMPAIGN MAP NODES
        foreach (var userCampaignMapNode in userCampaign.campaignMapNodes)
        {
            var node = userCampaignMapNode;
            if (!string.IsNullOrEmpty(node.unchartedTitle))
            {
                task.AddItem(new TranslationItem(
                    "MapNodes",
                    $"MapNode_{node.unchartedTitle}_UnchartedTitle",
                    node.unchartedTitle,
                    translated => node.unchartedTitle = translated));
            }

            if (!string.IsNullOrEmpty(node.overriddenTitle))
            {
                task.AddItem(new TranslationItem(
                    "MapNodes",
                    $"MapNode_{node.overriddenTitle}_OverriddenTitle",
                    node.overriddenTitle,
                    translated => node.overriddenTitle = translated));
            }

            if (!string.IsNullOrEmpty(node.overriddenDescription))
            {
                task.AddItem(new TranslationItem(
                    "MapNodes",
                    $"MapNode_{node.overriddenTitle}_OverriddenDescription",
                    node.overriddenDescription,
                    translated => node.overriddenDescription = translated));
            }
        }
    }

    private static async Task ExecuteTranslationAsync(
        CampaignTranslationTask task,
        UserCampaign userCampaign,
        bool retryFailedOnly = false)
    {
        task.Status = CampaignTranslationStatus.Running;
        var translationService = TranslationServiceFactory.GetCurrentService();
        var cancellationToken = task.CancellationTokenSource.Token;

        var concurrency = Math.Max(1, Math.Min(MaxTranslationConcurrency, Main.Settings.TranslationConcurrency));
        using var semaphore = new SemaphoreSlim(concurrency, concurrency);

        try
        {
            var itemsToProcess = retryFailedOnly
                ? task.GetFailedItems().ToList()
                : task.AllItems.Where(i => i.Status == TranslationItemStatus.Pending).ToList();

            // Create tasks for concurrent execution
            var translationTasks = new List<Task>();

            foreach (var item in itemsToProcess)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                while (!task.PauseEvent.IsSet && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // Wait for semaphore slot
                await semaphore.WaitAsync(cancellationToken);

                // Start translation task
                var translationTask = TranslateItemAsync(
                    task,
                    item,
                    translationService,
                    semaphore,
                    cancellationToken);

                translationTasks.Add(translationTask);
            }

            // Wait for all ongoing translations to complete
            await Task.WhenAll(translationTasks);

            // All items processed
            if (task.FailedItems > 0)
            {
                task.Status = CampaignTranslationStatus.Completed;
                task.ErrorMessage = $"{task.FailedItems} items failed to translate.";
            }
            else
            {
                task.Status = CampaignTranslationStatus.Completed;
            }

            // Save the campaign on main thread
            Instance._mainThreadActions.Enqueue(() =>
            {
                try
                {
                    var userCampaignPoolService = ServiceRepository.GetService<IUserCampaignPoolService>();
                    userCampaignPoolService.SaveUserCampaign(userCampaign);
                    Main.Info($"Campaign '{task.CampaignTitle}' translation completed and saved.");
                }
                catch (Exception ex)
                {
                    Main.Error($"Failed to save translated campaign: {ex.Message}");
                }
            });
        }
        catch (OperationCanceledException)
        {
            task.Status = CampaignTranslationStatus.Cancelled;
            Main.Info($"Campaign '{task.CampaignTitle}' translation cancelled.");
        }
        catch (Exception ex)
        {
            task.Status = CampaignTranslationStatus.Failed;
            task.ErrorMessage = ex.Message;
            Main.Error($"Campaign translation failed: {ex.Message}");
        }
    }

    /// <summary>
    ///     Translates a single item with semaphore control.
    /// </summary>
    private static async Task TranslateItemAsync(
        CampaignTranslationTask task,
        TranslationItem item,
        ITranslationService translationService,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        try
        {
            // Mark as in progress
            task.MarkItemInProgress(item);
            task.CurrentItem = item;

            // Check cache first
            var cacheKey = GetCacheKey(item.SourceText);
            if (TranslationsCache.TryGetValue(cacheKey, out var cachedTranslation))
            {
                item.TranslatedText = cachedTranslation;
            }
            else
            {
                // Perform translation
                var translated =
                    await translationService.TranslateAsync(item.SourceText, task.TargetLanguageCode,
                        cancellationToken);
                item.TranslatedText = translated;

                // Cache the result
                TranslationsCache.TryAdd(cacheKey, translated);
            }

            // Apply translation on main thread
            Instance._mainThreadActions.Enqueue(() =>
            {
                try
                {
                    item.ApplyTranslation(item.TranslatedText);
                }
                catch (Exception ex)
                {
                    Main.Error($"Failed to apply translation: {ex.Message}");
                }
            });

            task.MarkItemCompleted(item);

            // Small delay to prevent rate limiting
            await Task.Delay(50, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // On cancellation, mark item as pending so it can be retried
            item.Status = TranslationItemStatus.Pending;
            task.RemoveFromInProgress(item);
        }
        catch (Exception ex)
        {
            task.MarkItemFailed(item, ex.Message);
        }
        finally
        {
            // Always release semaphore
            semaphore.Release();
        }
    }

    private static string GetCacheKey(string sourceText)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(sourceText));
        var builder = new StringBuilder();
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }
}
