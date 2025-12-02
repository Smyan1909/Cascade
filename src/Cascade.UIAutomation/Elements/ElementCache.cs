using System.Collections.Concurrent;

namespace Cascade.UIAutomation.Elements;

/// <summary>
/// Provides caching for UI elements to improve performance.
/// </summary>
public class ElementCache
{
    private readonly ConcurrentDictionary<string, CachedElement> _cache = new();
    private readonly object _cleanupLock = new();
    private DateTime _lastCleanup = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the default cache duration.
    /// </summary>
    public TimeSpan DefaultCacheDuration { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum number of cached elements.
    /// </summary>
    public int MaxCachedElements { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the cleanup interval.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the current number of cached elements.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Gets a cached element by its runtime ID.
    /// </summary>
    /// <param name="runtimeId">The runtime ID of the element.</param>
    /// <returns>The cached element, or null if not found or expired.</returns>
    public IUIElement? GetCached(string runtimeId)
    {
        if (string.IsNullOrEmpty(runtimeId))
            return null;

        if (_cache.TryGetValue(runtimeId, out var cached))
        {
            if (cached.IsExpired)
            {
                _cache.TryRemove(runtimeId, out _);
                return null;
            }
            return cached.Element;
        }

        return null;
    }

    /// <summary>
    /// Caches an element.
    /// </summary>
    /// <param name="element">The element to cache.</param>
    /// <param name="duration">Optional custom cache duration.</param>
    public void Cache(IUIElement element, TimeSpan? duration = null)
    {
        if (element == null || string.IsNullOrEmpty(element.RuntimeId))
            return;

        TriggerCleanupIfNeeded();

        // Evict oldest entries if at capacity
        while (_cache.Count >= MaxCachedElements)
        {
            var oldest = _cache.OrderBy(kvp => kvp.Value.CachedAt).FirstOrDefault();
            if (oldest.Key != null)
                _cache.TryRemove(oldest.Key, out _);
        }

        var cached = new CachedElement(element, duration ?? DefaultCacheDuration);
        _cache.AddOrUpdate(element.RuntimeId, cached, (_, _) => cached);
    }

    /// <summary>
    /// Invalidates a cached element by its runtime ID.
    /// </summary>
    /// <param name="runtimeId">The runtime ID of the element to invalidate.</param>
    public void Invalidate(string runtimeId)
    {
        if (!string.IsNullOrEmpty(runtimeId))
            _cache.TryRemove(runtimeId, out _);
    }

    /// <summary>
    /// Invalidates all cached elements.
    /// </summary>
    public void InvalidateAll()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Checks if an element's cached data is stale.
    /// </summary>
    /// <param name="element">The element to check.</param>
    /// <returns>True if the cache is stale or element is not cached.</returns>
    public bool IsStale(IUIElement element)
    {
        if (element == null || string.IsNullOrEmpty(element.RuntimeId))
            return true;

        if (_cache.TryGetValue(element.RuntimeId, out var cached))
        {
            return cached.IsExpired;
        }

        return true;
    }

    /// <summary>
    /// Refreshes an element by removing it from cache and re-validating.
    /// </summary>
    /// <param name="element">The element to refresh.</param>
    /// <returns>The same element if still valid, or null if invalid.</returns>
    public Task<IUIElement?> RefreshAsync(IUIElement element)
    {
        return Task.Run(() =>
        {
            if (element == null)
                return null;

            // Invalidate current cache
            Invalidate(element.RuntimeId);

            // Try to verify the element is still valid by accessing a property
            try
            {
                _ = element.IsEnabled;
                Cache(element);
                return element;
            }
            catch
            {
                return null;
            }
        });
    }

    private void TriggerCleanupIfNeeded()
    {
        if (DateTime.UtcNow - _lastCleanup < CleanupInterval)
            return;

        lock (_cleanupLock)
        {
            if (DateTime.UtcNow - _lastCleanup < CleanupInterval)
                return;

            _lastCleanup = DateTime.UtcNow;

            // Remove expired entries
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }
        }
    }

    private class CachedElement
    {
        public IUIElement Element { get; }
        public DateTime CachedAt { get; }
        public DateTime ExpiresAt { get; }

        public CachedElement(IUIElement element, TimeSpan duration)
        {
            Element = element;
            CachedAt = DateTime.UtcNow;
            ExpiresAt = CachedAt + duration;
        }

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    }
}

