using EasyLog.Contracts.Models;

namespace EasyLog.Engine;

public sealed class SessionStateChangedEventArgs : EventArgs
{
    public SessionStateChangedEventArgs(SessionState state, Exception? error = null)
    {
        State = state;
        Error = error;
    }

    public SessionState State { get; }

    public Exception? Error { get; }
}

