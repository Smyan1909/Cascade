using Cascade.UIAutomation.Models;

namespace Cascade.UIAutomation.Session;

/// <summary>
/// Represents a unique handle to a hidden Windows Virtual Desktop session.
/// </summary>
public sealed record SessionHandle
{
    public Guid SessionId { get; init; }
    public Guid RunId { get; init; }
    public IntPtr VirtualDesktopId { get; init; }
    public string UserProfilePath { get; init; } = string.Empty;
    public VirtualDesktopProfile DesktopProfile { get; init; } = VirtualDesktopProfile.Default;
    public DateTimeOffset AcquiredAt { get; init; } = DateTimeOffset.UtcNow;

    public bool IsValid => SessionId != Guid.Empty && RunId != Guid.Empty;

    public static SessionHandle Empty => new()
    {
        SessionId = Guid.Empty,
        RunId = Guid.Empty,
        VirtualDesktopId = IntPtr.Zero,
        UserProfilePath = string.Empty,
        DesktopProfile = VirtualDesktopProfile.Default,
        AcquiredAt = DateTimeOffset.MinValue
    };

    public void EnsureValid()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException("SessionHandle is not valid. Ensure the SessionService issued a handle before invoking UI automation.");
        }
    }

    public override string ToString() => $"SessionId={SessionId}, RunId={RunId}, Desktop={VirtualDesktopId}";
}


