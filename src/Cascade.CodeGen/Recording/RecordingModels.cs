using Cascade.CodeGen.Generation;

namespace Cascade.CodeGen.Recording;

public sealed class RecordingOptions
{
    public bool RecordMouseClicks { get; set; } = true;
    public bool RecordKeystrokes { get; set; } = true;
    public bool RecordScrolls { get; set; } = true;
    public bool CaptureScreenshots { get; set; } = true;
    public TimeSpan MinActionInterval { get; set; } = TimeSpan.FromMilliseconds(100);
    public IReadOnlyList<string> ExcludedProcesses { get; set; } = Array.Empty<string>();
}

public enum RecordingState
{
    NotStarted,
    Recording,
    Paused,
    Stopped
}

public sealed class RecordedAction
{
    public int Index { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ActionType Type { get; set; }
    public string TargetDescription { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public byte[]? Screenshot { get; set; }
    public TimeSpan? DurationSincePrevious { get; set; }
}

public sealed class RecordingSession
{
    public Guid SessionId { get; init; } = Guid.NewGuid();
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public RecordingState State { get; set; } = RecordingState.NotStarted;
    public RecordingOptions Options { get; init; } = new();
    public List<RecordedAction> Actions { get; } = new();
}

public interface IActionRecorder
{
    Task<RecordingSession> StartRecordingAsync(RecordingOptions? options = null, CancellationToken cancellationToken = default);
    Task StopRecordingAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task PauseRecordingAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task ResumeRecordingAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task RecordActionAsync(Guid sessionId, RecordedAction action, CancellationToken cancellationToken = default);

    bool IsRecording { get; }
    RecordingSession? CurrentSession { get; }

    event EventHandler<RecordedAction>? ActionRecorded;
    event EventHandler<RecordingSession>? RecordingStarted;
    event EventHandler<RecordingSession>? RecordingStopped;
}

