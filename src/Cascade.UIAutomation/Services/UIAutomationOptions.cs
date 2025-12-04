using Cascade.UIAutomation.Session;

namespace Cascade.UIAutomation.Services;

public sealed class UIAutomationOptions
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ElementWaitPollingInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public TimeSpan SessionAcquireTimeout { get; set; } = TimeSpan.FromSeconds(15);

    public bool EnableCaching { get; set; } = true;
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromSeconds(5);

    public int MaxTreeDepth { get; set; } = 50;
    public bool UseControlView { get; set; } = true;

    public int DefaultClickDelay { get; set; } = 50;
    public int DefaultTypeDelay { get; set; } = 20;
    public VirtualInputProfile InputProfile { get; set; } = VirtualInputProfile.Balanced;

    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    public bool AllowSessionReuse { get; set; } = true;
}


