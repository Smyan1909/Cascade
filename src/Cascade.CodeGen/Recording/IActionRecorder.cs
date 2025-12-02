namespace Cascade.CodeGen.Recording;

/// <summary>
/// Interface for recording UI automation actions.
/// </summary>
public interface IActionRecorder
{
    /// <summary>
    /// Starts a new recording session.
    /// </summary>
    Task<RecordingSession> StartRecordingAsync(RecordingOptions? options = null);

    /// <summary>
    /// Stops the recording session.
    /// </summary>
    Task StopRecordingAsync(Guid sessionId);

    /// <summary>
    /// Pauses the recording session.
    /// </summary>
    Task PauseRecordingAsync(Guid sessionId);

    /// <summary>
    /// Resumes a paused recording session.
    /// </summary>
    Task ResumeRecordingAsync(Guid sessionId);

    /// <summary>
    /// Records an action manually (for manual recording mode).
    /// </summary>
    Task RecordActionAsync(Guid sessionId, RecordedAction action);

    /// <summary>
    /// Whether a recording is currently active.
    /// </summary>
    bool IsRecording { get; }

    /// <summary>
    /// The current recording session (if any).
    /// </summary>
    RecordingSession? CurrentSession { get; }

    /// <summary>
    /// Event fired when an action is recorded.
    /// </summary>
    event EventHandler<RecordedAction>? ActionRecorded;

    /// <summary>
    /// Event fired when recording starts.
    /// </summary>
    event EventHandler<RecordingSession>? RecordingStarted;

    /// <summary>
    /// Event fired when recording stops.
    /// </summary>
    event EventHandler<RecordingSession>? RecordingStopped;
}

