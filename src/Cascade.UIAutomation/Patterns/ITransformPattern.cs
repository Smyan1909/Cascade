namespace Cascade.UIAutomation.Patterns;

/// <summary>
/// Provides access to controls that can be moved, resized, or rotated.
/// </summary>
public interface ITransformPattern
{
    /// <summary>
    /// Gets whether the element can be moved.
    /// </summary>
    bool CanMove { get; }

    /// <summary>
    /// Gets whether the element can be resized.
    /// </summary>
    bool CanResize { get; }

    /// <summary>
    /// Gets whether the element can be rotated.
    /// </summary>
    bool CanRotate { get; }

    /// <summary>
    /// Moves the element to the specified screen coordinates.
    /// </summary>
    Task MoveAsync(double x, double y);

    /// <summary>
    /// Resizes the element.
    /// </summary>
    Task ResizeAsync(double width, double height);

    /// <summary>
    /// Rotates the element by the specified degrees.
    /// </summary>
    Task RotateAsync(double degrees);
}

