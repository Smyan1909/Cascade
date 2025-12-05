namespace Cascade.CodeGen.Recording;

public sealed class ActionRecorder : IActionRecorder
{
    private readonly object _gate = new();
    private RecordingSession? _session;

    public event EventHandler<RecordedAction>? ActionRecorded;
    public event EventHandler<RecordingSession>? RecordingStarted;
    public event EventHandler<RecordingSession>? RecordingStopped;

    public bool IsRecording => _session?.State == RecordingState.Recording;
    public RecordingSession? CurrentSession => _session;

    public Task<RecordingSession> StartRecordingAsync(RecordingOptions? options = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_session is not null && _session.State is RecordingState.Recording or RecordingState.Paused)
            {
                throw new InvalidOperationException("A recording session is already active.");
            }

            _session = new RecordingSession
            {
                State = RecordingState.Recording,
                Options = options ?? new RecordingOptions()
            };
        }

        RecordingStarted?.Invoke(this, _session);
        return Task.FromResult(_session!);
    }

    public Task StopRecordingAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        RecordingSession? session;
        lock (_gate)
        {
            session = EnsureSession(sessionId);
            session.State = RecordingState.Stopped;
            session.EndedAt = DateTime.UtcNow;
            _session = null;
        }

        RecordingStopped?.Invoke(this, session);
        return Task.CompletedTask;
    }

    public Task PauseRecordingAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var session = EnsureSession(sessionId);
            if (session.State != RecordingState.Recording)
            {
                throw new InvalidOperationException("Session is not recording.");
            }

            session.State = RecordingState.Paused;
        }

        return Task.CompletedTask;
    }

    public Task ResumeRecordingAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var session = EnsureSession(sessionId);
            if (session.State != RecordingState.Paused)
            {
                throw new InvalidOperationException("Session is not paused.");
            }

            session.State = RecordingState.Recording;
        }

        return Task.CompletedTask;
    }

    public Task RecordActionAsync(Guid sessionId, RecordedAction action, CancellationToken cancellationToken = default)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        RecordingSession session;

        lock (_gate)
        {
            session = EnsureSession(sessionId);
            if (session.State != RecordingState.Recording)
            {
                throw new InvalidOperationException("Session is not actively recording.");
            }

            action.Index = session.Actions.Count + 1;
            if (session.Actions.Count > 0)
            {
                action.DurationSincePrevious = DateTime.UtcNow - session.Actions.Last().Timestamp;
            }

            session.Actions.Add(action);
        }

        ActionRecorded?.Invoke(this, action);
        return Task.CompletedTask;
    }

    private RecordingSession EnsureSession(Guid sessionId)
    {
        if (_session is null || _session.SessionId != sessionId)
        {
            throw new InvalidOperationException("Recording session not found.");
        }

        return _session;
    }
}

