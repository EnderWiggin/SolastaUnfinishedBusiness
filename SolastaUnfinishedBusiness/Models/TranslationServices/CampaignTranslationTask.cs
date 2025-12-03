using System;
using System.Collections.Generic;
using System.Threading;

namespace SolastaUnfinishedBusiness.Models.TranslationServices;

/// <summary>
///     Represents the status of a translation item.
/// </summary>
internal enum TranslationItemStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}

/// <summary>
///     Represents a single translation item.
/// </summary>
internal sealed class TranslationItem
{
    internal TranslationItem(string category, string key, string sourceText, Action<string> applyTranslation)
    {
        Category = category;
        Key = key;
        SourceText = sourceText;
        ApplyTranslation = applyTranslation;
        Status = TranslationItemStatus.Pending;
    }

    /// <summary>
    ///     The category of this translation item (e.g., "Dialogs", "Items", "Quests").
    /// </summary>
    internal string Category { get; }

    /// <summary>
    ///     A unique key for this translation item within the category.
    /// </summary>
    internal string Key { get; }

    /// <summary>
    ///     The source text to translate.
    /// </summary>
    internal string SourceText { get; }

    /// <summary>
    ///     The translated text (null if not yet translated).
    /// </summary>
    internal string TranslatedText { get; set; }

    /// <summary>
    ///     The current status of this translation item.
    /// </summary>
    internal TranslationItemStatus Status { get; set; }

    /// <summary>
    ///     Error message if translation failed.
    /// </summary>
    internal string ErrorMessage { get; set; }

    /// <summary>
    ///     Action to apply the translation to the target object.
    ///     This will be called on the main thread after translation completes.
    /// </summary>
    internal Action<string> ApplyTranslation { get; }
}

/// <summary>
///     Represents the status of an entire campaign translation task.
/// </summary>
internal enum CampaignTranslationStatus
{
    NotStarted,
    Running,
    Paused,
    Completed,
    Cancelled,
    Failed
}

/// <summary>
///     Progress information for a translation category.
/// </summary>
internal sealed class CategoryProgress
{
    internal string CategoryName { get; set; }
    internal int TotalItems { get; set; }
    internal int CompletedItems { get; set; }
    internal int FailedItems { get; set; }

    internal float PercentageComplete =>
        TotalItems > 0 ? (float)(CompletedItems + FailedItems) / TotalItems : 0f;
}

/// <summary>
///     Manages the translation of an entire user campaign.
/// </summary>
internal sealed class CampaignTranslationTask
{
    private readonly List<TranslationItem> _allItems = [];
    private readonly Dictionary<string, CategoryProgress> _categoryProgress = new();
    private readonly object _lock = new();

    internal CampaignTranslationTask(string campaignTitle, string targetLanguageCode)
    {
        CampaignTitle = campaignTitle;
        TargetLanguageCode = targetLanguageCode;
        Status = CampaignTranslationStatus.NotStarted;
        CancellationTokenSource = new CancellationTokenSource();
        PauseEvent = new ManualResetEventSlim(true); // Initially not paused
    }

    internal string CampaignTitle { get; }
    internal string TargetLanguageCode { get; }
    internal CampaignTranslationStatus Status { get; set; }
    internal CancellationTokenSource CancellationTokenSource { get; private set; }
    internal ManualResetEventSlim PauseEvent { get; }

    /// <summary>
    ///     Recreates the cancellation token source for retry operations.
    /// </summary>
    internal void RecreateCancellationTokenSource()
    {
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = new CancellationTokenSource();
    }

    internal bool IsPaused => !PauseEvent.IsSet;
    internal IReadOnlyList<TranslationItem> AllItems => _allItems;
    internal IReadOnlyDictionary<string, CategoryProgress> CategoryProgress => _categoryProgress;
    internal TranslationItem CurrentItem { get; set; }
    internal int CurrentIndex { get; set; }
    internal int TotalItems => _allItems.Count;
    internal int CompletedItems { get; set; }
    internal int FailedItems { get; set; }
    internal float PercentageComplete =>
        TotalItems > 0 ? (float)(CompletedItems + FailedItems) / TotalItems : 0f;
    internal string ErrorMessage { get; set; }

    internal void AddItem(TranslationItem item)
    {
        lock (_lock)
        {
            _allItems.Add(item);

            if (!_categoryProgress.TryGetValue(item.Category, out var progress))
            {
                progress = new CategoryProgress { CategoryName = item.Category };
                _categoryProgress[item.Category] = progress;
            }

            progress.TotalItems++;
        }
    }

    internal void MarkItemCompleted(TranslationItem item)
    {
        lock (_lock)
        {
            item.Status = TranslationItemStatus.Completed;
            CompletedItems++;

            if (_categoryProgress.TryGetValue(item.Category, out var progress))
            {
                progress.CompletedItems++;
            }
        }
    }

    internal void MarkItemFailed(TranslationItem item, string errorMessage)
    {
        lock (_lock)
        {
            item.Status = TranslationItemStatus.Failed;
            item.ErrorMessage = errorMessage;
            FailedItems++;

            if (_categoryProgress.TryGetValue(item.Category, out var progress))
            {
                progress.FailedItems++;
            }
        }
    }

    internal void Pause()
    {
        if (Status == CampaignTranslationStatus.Running)
        {
            PauseEvent.Reset();
            Status = CampaignTranslationStatus.Paused;
        }
    }

    internal void Resume()
    {
        if (Status == CampaignTranslationStatus.Paused)
        {
            PauseEvent.Set();
            Status = CampaignTranslationStatus.Running;
        }
    }

    internal void Cancel()
    {
        CancellationTokenSource.Cancel();
        PauseEvent.Set(); // Release pause if paused
        Status = CampaignTranslationStatus.Cancelled;
    }

    internal IEnumerable<TranslationItem> GetFailedItems()
    {
        lock (_lock)
        {
            foreach (var item in _allItems)
            {
                if (item.Status == TranslationItemStatus.Failed)
                {
                    yield return item;
                }
            }
        }
    }

    internal void ResetFailedItems()
    {
        lock (_lock)
        {
            foreach (var item in _allItems)
            {
                if (item.Status == TranslationItemStatus.Failed)
                {
                    item.Status = TranslationItemStatus.Pending;
                    item.ErrorMessage = null;
                    FailedItems--;

                    if (_categoryProgress.TryGetValue(item.Category, out var progress))
                    {
                        progress.FailedItems--;
                    }
                }
            }
        }
    }
}
