using System;
using UnityEngine;

public abstract class PokerNetworkTransport : MonoBehaviour
{
    public event Action Connected;
    public event Action<string> Disconnected;
    public event Action<PokerTableSnapshot> SnapshotReceived;
    public event Action<PokerNetworkError> ErrorReceived;

    public abstract bool IsConnected { get; }

    public abstract void Connect(PokerConnectionSettings settings);

    public abstract void Disconnect();

    public abstract void SendAction(PokerActionRequest request);

    protected void RaiseConnected()
    {
        Connected?.Invoke();
    }

    protected void RaiseDisconnected(string reason)
    {
        Disconnected?.Invoke(reason);
    }

    protected void RaiseSnapshotReceived(PokerTableSnapshot snapshot)
    {
        SnapshotReceived?.Invoke(snapshot);
    }

    protected void RaiseErrorReceived(PokerNetworkError error)
    {
        ErrorReceived?.Invoke(error);
    }
}
