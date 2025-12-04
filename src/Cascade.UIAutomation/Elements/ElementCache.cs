using Cascade.UIAutomation.Session;
using System.Collections.Concurrent;

namespace Cascade.UIAutomation.Elements;

public sealed class ElementCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();
    private readonly Func<IUIElement, Task<IUIElement?>> _refresher;

    public ElementCache(SessionHandle session, Func<IUIElement, Task<IUIElement?>> refresher)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        _refresher = refresher ?? throw new ArgumentNullException(nameof(refresher));
    }

    public SessionHandle Session { get; }

    public TimeSpan DefaultCacheDuration { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxCachedElements { get; set; } = 1000;
    public bool EnableCaching { get; set; } = true;

    public IUIElement? GetCached(string runtimeId)
    {
        if (!EnableCaching)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(runtimeId))
        {
            return null;
        }

        if (_entries.TryGetValue(runtimeId, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            return entry.Element;
        }

        _entries.TryRemove(runtimeId, out _);
        return null;
    }

    public void Cache(IUIElement element, TimeSpan? duration = null)
    {
        if (!EnableCaching)
        {
            return;
        }

        var key = element?.RuntimeId;
        if (element is null || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var expiresAt = DateTimeOffset.UtcNow + (duration ?? DefaultCacheDuration);
        _entries[key] = new CacheEntry(element, expiresAt);
        EvictIfNeeded();
    }

    public void Invalidate(string runtimeId)
    {
        if (string.IsNullOrWhiteSpace(runtimeId))
        {
            return;
        }

        _entries.TryRemove(runtimeId, out _);
    }

    public void InvalidateAll() => _entries.Clear();

    public bool IsStale(IUIElement element)
    {
        if (!EnableCaching)
        {
            return true;
        }

        if (element is null || string.IsNullOrWhiteSpace(element.RuntimeId))
        {
            return true;
        }

        return !_entries.TryGetValue(element.RuntimeId, out var entry) || entry.ExpiresAt <= DateTimeOffset.UtcNow;
    }

    public Task<IUIElement?> RefreshAsync(IUIElement element)
    {
        return _refresher(element);
    }

    private void EvictIfNeeded()
    {
        if (!EnableCaching)
        {
            return;
        }

        while (_entries.Count > MaxCachedElements)
        {
            var oldest = _entries.OrderBy(kvp => kvp.Value.ExpiresAt).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(oldest.Key))
            {
                _entries.TryRemove(oldest.Key, out _);
            }
            else
            {
                break;
            }
        }
    }

    private sealed record CacheEntry(IUIElement Element, DateTimeOffset ExpiresAt);
}


