namespace Cascade.UIAutomation.Patterns;

/// <summary>
/// Provides access to controls that initiate or perform an action when activated.
/// </summary>
public interface IInvokePattern
{
    /// <summary>
    /// Invokes the action associated with the control.
    /// </summary>
    Task InvokeAsync();
}

