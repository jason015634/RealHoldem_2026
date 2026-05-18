using System.Collections.Generic;

// 한 플레이어의 손패, 칩, 현재 베팅액, 폴드/올인 상태를 들고 있는 런타임 상태 객체입니다.
public sealed class PlayerState
{
    public string PlayerId { get; }
    public string Nickname { get; }
    public int SeatIndex { get; private set; }
    public bool IsHuman { get; }
    public bool IsBot => !IsHuman;
    public int Chips { get; private set; }
    public int CurrentBet { get; private set; }
    public bool HasFolded { get; private set; }
    public bool IsAllIn => Chips <= 0;
    public string LastAction { get; private set; }
    public string CharacterSpritePath { get; private set; }
    public List<Card> HoleCards { get; } = new List<Card>(2);

    public PlayerState(string nickname, int startingChips, bool isHuman)
        : this(nickname.ToLowerInvariant(), nickname, startingChips, isHuman, -1)
    {
    }

    public PlayerState(string playerId, string nickname, int startingChips, bool isHuman, int seatIndex)
    {
        PlayerId = playerId;
        Nickname = nickname;
        Chips = startingChips;
        IsHuman = isHuman;
        SeatIndex = seatIndex;
        LastAction = string.Empty;
        CharacterSpritePath = string.Empty;
    }

    public void AssignSeat(int seatIndex)
    {
        SeatIndex = seatIndex;
    }

    public void RefillChips(int amount)
    {
        Chips = UnityEngine.Mathf.Max(0, amount);
    }

    public void ResetForHand()
    {
        HoleCards.Clear();
        CurrentBet = 0;
        HasFolded = false;
        LastAction = string.Empty;
    }

    public void ResetForBettingRound()
    {
        CurrentBet = 0;
        LastAction = string.Empty;
    }

    public int PayChips(int amount)
    {
        int paid = UnityEngine.Mathf.Clamp(amount, 0, Chips);
        Chips -= paid;
        CurrentBet += paid;
        return paid;
    }

    public void WinChips(int amount)
    {
        Chips += UnityEngine.Mathf.Max(0, amount);
    }

    public void Fold()
    {
        HasFolded = true;
        LastAction = "Fold";
    }

    public void SetLastAction(string action)
    {
        LastAction = action;
    }

    public void SetCharacterSpritePath(string path)
    {
        CharacterSpritePath = path ?? string.Empty;
    }
}
