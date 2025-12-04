namespace Cascade.UIAutomation.Session;

/// <summary>
/// Describes the virtual input channel wired to a hidden desktop session.
/// </summary>
public sealed record VirtualInputChannel
{
    public Guid ChannelId { get; init; } = Guid.NewGuid();
    public string Transport { get; init; } = "hid";
    public string DevicePath { get; init; } = string.Empty;
    public VirtualInputProfile Profile { get; init; } = VirtualInputProfile.Balanced;
    public TimeSpan LatencyBudget { get; init; } = TimeSpan.FromMilliseconds(50);

    public bool IsValid => ChannelId != Guid.Empty && !string.IsNullOrWhiteSpace(DevicePath);
}


