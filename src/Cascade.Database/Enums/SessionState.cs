namespace Cascade.Database.Enums;

/// <summary>
/// Tracks the lifecycle of an automation session.
/// </summary>
public enum SessionState
{
    Active,
    Draining,
    Released,
    Failed
}



