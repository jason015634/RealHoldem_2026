using System;
using UnityEngine;

public sealed class NetworkPokerGameManager : MonoBehaviour
{
    [SerializeField] private PokerNetworkClient networkClient;

    public event Action<PokerTableSnapshot> SnapshotChanged;
    public event Action<PokerNetworkError> NetworkError;

    public PokerTableSnapshot Snapshot => networkClient != null ? networkClient.CurrentSnapshot : null;
    public bool IsConnected => networkClient != null && networkClient.IsConnected;
    public bool IsLocalPlayersTurn => Snapshot != null && Snapshot.IsLocalPlayersTurn();

    private void Awake()
    {
        ResolveNetworkClient();
    }

    private void OnEnable()
    {
        ResolveNetworkClient();
        if (networkClient == null)
        {
            return;
        }

        networkClient.SnapshotReceived += HandleSnapshotReceived;
        networkClient.ErrorReceived += HandleErrorReceived;
    }

    private void OnDisable()
    {
        if (networkClient == null)
        {
            return;
        }

        networkClient.SnapshotReceived -= HandleSnapshotReceived;
        networkClient.ErrorReceived -= HandleErrorReceived;
    }

    public void Connect()
    {
        ResolveNetworkClient();
        networkClient?.Connect();
    }

    public void Disconnect()
    {
        networkClient?.Disconnect();
    }

    public void Sit(int seatIndex)
    {
        networkClient?.Sit(seatIndex);
    }

    public void Leave()
    {
        networkClient?.Leave();
    }

    public void StartNewHand()
    {
        if (Snapshot != null && !Snapshot.CanStartHand)
        {
            return;
        }

        networkClient?.StartHand();
    }

    public void OnPlayerFold()
    {
        if (!CanSendLocalAction())
        {
            return;
        }

        networkClient?.Fold();
    }

    public void OnPlayerCheckOrCall()
    {
        if (!CanSendLocalAction())
        {
            return;
        }

        PokerActionOptionsSnapshot options = Snapshot.LocalActionOptions;
        if (options != null && options.CanCheck)
        {
            networkClient?.Check();
            return;
        }

        if (options == null || options.CanCall)
        {
            networkClient?.Call();
        }
    }

    public void OnPlayerBetOrRaise(int totalBetTarget)
    {
        if (!CanSendLocalAction())
        {
            return;
        }

        PokerActionOptionsSnapshot options = Snapshot.LocalActionOptions;
        if (options != null)
        {
            totalBetTarget = Mathf.Clamp(totalBetTarget, options.MinBetTarget, options.MaxBetTarget);
            bool canSpend = Snapshot.CurrentBet > 0 ? options.CanRaise : options.CanBet;
            if (!canSpend)
            {
                return;
            }
        }

        networkClient?.BetOrRaise(totalBetTarget);
    }

    public float GetActionTimerNormalized()
    {
        if (Snapshot == null || Snapshot.ActionDeadlineUtcTicks <= 0 || Snapshot.ServerTimeUtcTicks <= 0)
        {
            return 0f;
        }

        long remainingTicks = Snapshot.ActionDeadlineUtcTicks - DateTime.UtcNow.Ticks;
        long totalTicks = Snapshot.ActionDeadlineUtcTicks - Snapshot.ServerTimeUtcTicks;
        if (totalTicks <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01((float)remainingTicks / totalTicks);
    }

    private bool CanSendLocalAction()
    {
        return networkClient != null
            && networkClient.IsConnected
            && Snapshot != null
            && Snapshot.IsLocalPlayersTurn();
    }

    private void ResolveNetworkClient()
    {
        if (networkClient == null)
        {
            networkClient = GetComponent<PokerNetworkClient>();
        }
    }

    private void HandleSnapshotReceived(PokerTableSnapshot snapshot)
    {
        SnapshotChanged?.Invoke(snapshot);
    }

    private void HandleErrorReceived(PokerNetworkError error)
    {
        NetworkError?.Invoke(error);
    }
}
