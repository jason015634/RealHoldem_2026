using System;
using System.Collections.Generic;

public enum PokerNetworkActionType
{
    None,
    Connect,
    Sit,
    Leave,
    StartHand,
    Fold,
    Check,
    Call,
    Bet,
    Raise
}

[Serializable]
public sealed class PokerConnectionSettings
{
    public string ServerUrl;
    public string AuthToken;
    public string TableId;
    public string PlayerId;
}

[Serializable]
public sealed class PokerActionRequest
{
    public string RequestId;
    public string TableId;
    public string PlayerId;
    public PokerNetworkActionType Action;
    public int SeatIndex = -1;
    public int TotalBetTarget;
    public long ClientSequence;
}

[Serializable]
public sealed class PokerNetworkError
{
    public string Code;
    public string Message;
    public string RequestId;
}

[Serializable]
public sealed class PokerCardSnapshot
{
    public bool IsKnown;
    public Suit Suit;
    public Rank Rank;

    public static PokerCardSnapshot Hidden()
    {
        return new PokerCardSnapshot { IsKnown = false };
    }

    public static PokerCardSnapshot Known(Card card)
    {
        return new PokerCardSnapshot
        {
            IsKnown = true,
            Suit = card.Suit,
            Rank = card.Rank
        };
    }

    public Card ToCard()
    {
        return new Card(Suit, Rank);
    }
}

[Serializable]
public sealed class PokerPlayerSnapshot
{
    public string PlayerId;
    public string Nickname;
    public int SeatIndex = -1;
    public bool IsLocalPlayer;
    public bool IsBot;
    public int Chips;
    public int CurrentBet;
    public bool HasFolded;
    public bool IsAllIn;
    public string LastAction;
    public string CharacterSpritePath;
    public List<PokerCardSnapshot> HoleCards = new List<PokerCardSnapshot>(2);
}

[Serializable]
public sealed class PokerSeatSnapshot
{
    public int SeatIndex;
    public bool IsOccupied;
    public PokerPlayerSnapshot Player;
}

[Serializable]
public sealed class PokerActionOptionsSnapshot
{
    public bool CanFold;
    public bool CanCheck;
    public bool CanCall;
    public bool CanBet;
    public bool CanRaise;
    public int CallAmount;
    public int MinBetTarget;
    public int MaxBetTarget;
}

[Serializable]
public sealed class PokerTableSnapshot
{
    public string TableId;
    public string HandId;
    public PokerGameState State;
    public string LocalPlayerId;
    public string ActingPlayerId;
    public string StatusMessage;
    public int Pot;
    public int CurrentBet;
    public int DealerSeatIndex;
    public long ServerTimeUtcTicks;
    public long ActionDeadlineUtcTicks;
    public bool CanStartHand;
    public bool RevealAllHoleCards;
    public PokerActionOptionsSnapshot LocalActionOptions = new PokerActionOptionsSnapshot();
    public List<PokerSeatSnapshot> Seats = new List<PokerSeatSnapshot>(6);
    public List<PokerCardSnapshot> CommunityCards = new List<PokerCardSnapshot>(5);

    public bool IsLocalPlayersTurn()
    {
        return !string.IsNullOrEmpty(LocalPlayerId)
            && !string.IsNullOrEmpty(ActingPlayerId)
            && LocalPlayerId == ActingPlayerId;
    }
}
