using System;
using UnityEngine;

public sealed class PokerNetworkClient : MonoBehaviour
{
    [SerializeField] private PokerNetworkTransport transport;
    [SerializeField] private PokerConnectionSettings connectionSettings = new PokerConnectionSettings();

    private long clientSequence;

    public event Action Connected;
    public event Action<string> Disconnected;
    public event Action<PokerTableSnapshot> SnapshotReceived;
    public event Action<PokerNetworkError> ErrorReceived;

    public PokerTableSnapshot CurrentSnapshot { get; private set; }
    public bool IsConnected => transport != null && transport.IsConnected;
    public PokerConnectionSettings ConnectionSettings => connectionSettings;

    private void Awake()
    {
        ResolveTransport();
    }

    private void OnEnable()
    {
        BindTransport();
    }

    private void OnDisable()
    {
        UnbindTransport();
    }

    public void Connect()
    {
        ResolveTransport();
        if (transport == null)
        {
            RaiseError("missing_transport", "PokerNetworkTransport is not assigned.", string.Empty);
            return;
        }

        transport.Connect(connectionSettings);
    }

    public void Disconnect()
    {
        if (transport != null && transport.IsConnected)
        {
            transport.Disconnect();
        }
    }

    public void Sit(int seatIndex)
    {
        SendAction(PokerNetworkActionType.Sit, seatIndex, 0);
    }

    public void Leave()
    {
        SendAction(PokerNetworkActionType.Leave, -1, 0);
    }

    public void StartHand()
    {
        SendAction(PokerNetworkActionType.StartHand, -1, 0);
    }

    public void Fold()
    {
        SendAction(PokerNetworkActionType.Fold, -1, 0);
    }

    public void Check()
    {
        SendAction(PokerNetworkActionType.Check, -1, 0);
    }

    public void Call()
    {
        SendAction(PokerNetworkActionType.Call, -1, 0);
    }

    public void BetOrRaise(int totalBetTarget)
    {
        PokerNetworkActionType action = CurrentSnapshot != null && CurrentSnapshot.CurrentBet > 0
            ? PokerNetworkActionType.Raise
            : PokerNetworkActionType.Bet;
        SendAction(action, -1, totalBetTarget);
    }

    public void ApplySnapshot(PokerTableSnapshot snapshot)
    {
        CurrentSnapshot = snapshot;
        SnapshotReceived?.Invoke(snapshot);
    }

    private void SendAction(PokerNetworkActionType action, int seatIndex, int totalBetTarget)
    {
        ResolveTransport();
        if (transport == null || !transport.IsConnected)
        {
            RaiseError("not_connected", "Cannot send poker action before the network transport is connected.", string.Empty);
            return;
        }

        PokerActionRequest request = new PokerActionRequest
        {
            RequestId = Guid.NewGuid().ToString("N"),
            TableId = connectionSettings.TableId,
            PlayerId = connectionSettings.PlayerId,
            Action = action,
            SeatIndex = seatIndex,
            TotalBetTarget = totalBetTarget,
            ClientSequence = ++clientSequence
        };

        transport.SendAction(request);
    }

    private void ResolveTransport()
    {
        if (transport == null)
        {
            transport = GetComponent<PokerNetworkTransport>();
        }
    }

    private void BindTransport()
    {
        ResolveTransport();
        if (transport == null)
        {
            return;
        }

        transport.Connected += HandleConnected;
        transport.Disconnected += HandleDisconnected;
        transport.SnapshotReceived += HandleSnapshotReceived;
        transport.ErrorReceived += HandleErrorReceived;
    }

    private void UnbindTransport()
    {
        if (transport == null)
        {
            return;
        }

        transport.Connected -= HandleConnected;
        transport.Disconnected -= HandleDisconnected;
        transport.SnapshotReceived -= HandleSnapshotReceived;
        transport.ErrorReceived -= HandleErrorReceived;
    }

    private void HandleConnected()
    {
        Connected?.Invoke();
    }

    private void HandleDisconnected(string reason)
    {
        Disconnected?.Invoke(reason);
    }

    private void HandleSnapshotReceived(PokerTableSnapshot snapshot)
    {
        ApplySnapshot(snapshot);
    }

    private void HandleErrorReceived(PokerNetworkError error)
    {
        ErrorReceived?.Invoke(error);
    }

    private void RaiseError(string code, string message, string requestId)
    {
        ErrorReceived?.Invoke(new PokerNetworkError
        {
            Code = code,
            Message = message,
            RequestId = requestId
        });
    }
}
