namespace Cascade.UIAutomation.Services;

/// <summary>
/// Configuration options for the UI Automation service.
/// </summary>
public class UIAutomationOptions
{
    /// <summary>
    /// Gets or sets the default timeout for waiting operations.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the polling interval when waiting for elements.
    /// </summary>
    public TimeSpan ElementWaitPollingInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets whether element caching is enabled.
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache duration for elements.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum number of cached elements.
    /// </summary>
    public int MaxCachedElements { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum tree depth for tree walking operations.
    /// </summary>
    public int MaxTreeDepth { get; set; } = 50;

    /// <summary>
    /// Gets or sets whether to use control view by default.
    /// </summary>
    public bool UseControlView { get; set; } = true;

    /// <summary>
    /// Gets or sets the delay after click operations in milliseconds.
    /// </summary>
    public int DefaultClickDelay { get; set; } = 50;

    /// <summary>
    /// Gets or sets the delay between typed characters in milliseconds.
    /// </summary>
    public int DefaultTypeDelay { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum retry attempts for failed operations.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);
}

