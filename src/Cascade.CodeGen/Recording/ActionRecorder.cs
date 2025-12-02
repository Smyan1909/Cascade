namespace Cascade.CodeGen.Recording;

/// <summary>
/// Manual action recorder - records actions explicitly provided via API.
/// </summary>
public class ActionRecorder : IActionRecorder
{
    private RecordingSession? _currentSession;
    private readonly List<RecordedAction> _actions = new();

    /// <inheritdoc />
    public bool IsRecording => _currentSession?.State == RecordingState.Recording;

    /// <inheritdoc />
    public RecordingSession? CurrentSession => _currentSession;

    /// <inheritdoc />
    public event EventHandler<RecordedAction>? ActionRecorded;

    /// <inheritdoc />
    public event EventHandler<RecordingSession>? RecordingStarted;

    /// <inheritdoc />
    public event EventHandler<RecordingSession>? RecordingStopped;

    /// <inheritdoc />
    public Task<RecordingSession> StartRecordingAsync(RecordingOptions? options = null)
    {
        if (_currentSession != null && _currentSession.State == RecordingState.Recording)
            throw new InvalidOperationException("A recording session is already in progress");

        _currentSession = new RecordingSession
        {
            SessionId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            State = RecordingState.Recording,
            Options = options ?? new RecordingOptions(),
            Actions = _actions
        };

        RecordingStarted?.Invoke(this, _currentSession);
        return Task.FromResult(_currentSession);
    }

    /// <inheritdoc />
    public Task StopRecordingAsync(Guid sessionId)
    {
        if (_currentSession == null || _currentSession.SessionId != sessionId)
            throw new InvalidOperationException($"Recording session {sessionId} not found");

        if (_currentSession.State != RecordingState.Recording && _currentSession.State != RecordingState.Paused)
            throw new InvalidOperationException($"Cannot stop session in state {_currentSession.State}");

        _currentSession.State = RecordingState.Stopped;
        _currentSession.EndedAt = DateTime.UtcNow;

        RecordingStopped?.Invoke(this, _currentSession);

        var session = _currentSession;
        _currentSession = null;
        _actions.Clear();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PauseRecordingAsync(Guid sessionId)
    {
        if (_currentSession == null || _currentSession.SessionId != sessionId)
            throw new InvalidOperationException($"Recording session {sessionId} not found");

        if (_currentSession.State != RecordingState.Recording)
            throw new InvalidOperationException($"Cannot pause session in state {_currentSession.State}");

        _currentSession.State = RecordingState.Paused;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumeRecordingAsync(Guid sessionId)
    {
        if (_currentSession == null || _currentSession.SessionId != sessionId)
            throw new InvalidOperationException($"Recording session {sessionId} not found");

        if (_currentSession.State != RecordingState.Paused)
            throw new InvalidOperationException($"Cannot resume session in state {_currentSession.State}");

        _currentSession.State = RecordingState.Recording;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordActionAsync(Guid sessionId, RecordedAction action)
    {
        if (_currentSession == null || _currentSession.SessionId != sessionId)
            throw new InvalidOperationException($"Recording session {sessionId} not found");

        if (_currentSession.State != RecordingState.Recording)
            throw new InvalidOperationException($"Cannot record action in state {_currentSession.State}");

        action.Index = _actions.Count + 1;
        action.Timestamp = DateTime.UtcNow;

        if (_actions.Any())
        {
            var lastAction = _actions.Last();
            action.DurationSincePrevious = action.Timestamp - lastAction.Timestamp;
        }

        _actions.Add(action);

        ActionRecorded?.Invoke(this, action);

        return Task.CompletedTask;
    }
}

