using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 플레이어가 선택할 수 있는 베팅 액션입니다.
public enum BettingAction
{
    Fold,
    Check,
    Call,
    Bet,
    Raise
}

public readonly struct PotAward
{
    public readonly PlayerState Player;
    public readonly int Amount;

    public PotAward(PlayerState player, int amount)
    {
        Player = player;
        Amount = amount;
    }
}

// 베팅 라운드의 돈 흐름을 관리합니다.
// 블라인드, 콜/체크/폴드/베팅/레이즈, 팟 지급과 분배를 처리하고 칩 지불 이벤트를 발생시킵니다.
public readonly struct SidePotInfo
{
    public readonly int Amount;
    public readonly IReadOnlyList<PlayerState> EligiblePlayers;

    public SidePotInfo(int amount, IReadOnlyList<PlayerState> eligiblePlayers)
    {
        Amount = amount;
        EligiblePlayers = eligiblePlayers;
    }
}

public readonly struct BlindPositionInfo
{
    public readonly PlayerState Dealer;
    public readonly PlayerState SmallBlind;
    public readonly PlayerState BigBlind;
    public readonly PlayerState PreflopFirstActor;
    public readonly PlayerState PostflopFirstActor;

    public BlindPositionInfo(
        PlayerState dealer,
        PlayerState smallBlind,
        PlayerState bigBlind,
        PlayerState preflopFirstActor,
        PlayerState postflopFirstActor)
    {
        Dealer = dealer;
        SmallBlind = smallBlind;
        BigBlind = bigBlind;
        PreflopFirstActor = preflopFirstActor;
        PostflopFirstActor = postflopFirstActor;
    }
}

public static class PokerRuleUtility
{
    public static int NextSeatIndex(int seatIndex, int maxSeats)
    {
        return (seatIndex + 1 + maxSeats) % maxSeats;
    }

    /// <summary>
    /// “startSeatIndex 좌석부터 시작해서, 비어있는 좌석은 건너뛰고 다음 실제 플레이어를 찾아주는 함수
    /// </summary>
    /// <param name="startSeatIndex"></param>
    /// <param name="maxSeats"></param>
    /// <param name="activePlayers"></param>
    /// <returns></returns>
    public static PlayerState PlayerAtSeatOrNext(
        int startSeatIndex,
        int maxSeats,
        IReadOnlyList<PlayerState> activePlayers)
    {
        if (activePlayers == null || activePlayers.Count == 0)
        {
            return null;
        }

        for (int offset = 0; offset < maxSeats; offset++)
        {
            int index = (startSeatIndex + offset) % maxSeats;
            for (int i = 0; i < activePlayers.Count; i++)
            {
                PlayerState player = activePlayers[i];
                if (player != null && player.SeatIndex == index)
                {
                    return player;
                }
            }
        }

        return activePlayers[0];
    }

    /// <summary>
    /// 딜러, 스몰 블라인드, 빅 블라인드, 프리플랍 첫 액션 플레이어, 포스트플랍 첫 액션 플레이어를 계산하여 반환합니다.
    /// </summary>
    /// <param name="activePlayers"></param>
    /// <param name="dealerSeatIndex"></param>
    /// <param name="maxSeats"></param>
    /// <returns></returns>
    public static BlindPositionInfo CalculateBlindPositions(
        IReadOnlyList<PlayerState> activePlayers,
        int dealerSeatIndex,
        int maxSeats)
    {
        PlayerState dealer = PlayerAtSeatOrNext(dealerSeatIndex, maxSeats, activePlayers);
        if (dealer == null)
        {
            return default;
        }

        // 헤즈업에서는 딜러가 스몰 블라인드이며 프리플랍에서 먼저 액션합니다.
        if (activePlayers.Count == 2)
        {
            PlayerState bigBlind = PlayerAtSeatOrNext(NextSeatIndex(dealer.SeatIndex, maxSeats), maxSeats, activePlayers);
            return new BlindPositionInfo(dealer, dealer, bigBlind, dealer, bigBlind);
        }

        // 3명 이상일 때는 딜러 왼쪽부터 블라인드를 놓고, 포스트플랍 액션은 스몰 블라인드부터 시작합니다.
        // dealer -> small blind -> big blind -> preflop first actor 단 세명일 때는 다시 딜러로
        PlayerState smallBlind = PlayerAtSeatOrNext(NextSeatIndex(dealer.SeatIndex, maxSeats), maxSeats, activePlayers);            
        PlayerState bigBlindPlayer = PlayerAtSeatOrNext(NextSeatIndex(smallBlind.SeatIndex, maxSeats), maxSeats, activePlayers);
        PlayerState preflopFirst = PlayerAtSeatOrNext(NextSeatIndex(bigBlindPlayer.SeatIndex, maxSeats), maxSeats, activePlayers);
        return new BlindPositionInfo(dealer, smallBlind, bigBlindPlayer, preflopFirst, smallBlind);
    }
}

public sealed class BettingManager : MonoBehaviour
{
    public event Action<PlayerState, int> PlayerPaidChips;

    public int SmallBlind { get; private set; } // 스몰 블라인드 금액
    public int BigBlind { get; private set; }   // 빅 블라인드 금액
    public int Pot { get; private set; }
    public int CurrentBet { get; private set; } // 기준 베팅액, 현재까지 나온 가장 큰 총 베팅액

    public int LastFullRaiseAmount { get; private set; }
    public bool LastActionIncreasedBet { get; private set; }
    public bool LastActionWasFullRaise { get; private set; }
    private PlayerState[] players = Array.Empty<PlayerState>();
    private readonly Dictionary<PlayerState, int> handContributions = new Dictionary<PlayerState, int>();   // 현재 핸드에서 각 플레이어가 팟에 기여한 총액을 기록하는 맵입니다. 사이드팟 계산 시에 사용됩니다.

    public void Initialize(IReadOnlyList<PlayerState> handPlayers, int smallBlind, int bigBlind)
    {
        players = new PlayerState[handPlayers.Count];
        for (int i = 0; i < handPlayers.Count; i++)
        {
            players[i] = handPlayers[i];
        }

        SmallBlind = smallBlind;
        BigBlind = bigBlind;
    }

    public void ResetForNewHand()
    {
        Pot = 0;
        CurrentBet = 0;
        LastFullRaiseAmount = BigBlind;
        LastActionIncreasedBet = false;
        LastActionWasFullRaise = false;

        foreach (PlayerState player in players)
        {
            player.ResetForHand();
        }

        handContributions.Clear();
        foreach (PlayerState player in players)
        {
            handContributions[player] = 0;
    }
}

public static class PokerRuleDebugScenarios
{
    public static string RunAll()
    {
        TestHeadsUpBlindOrder();
        TestMultiwayBlindOrder();
        TestSidePotConstruction();
        TestMinimumRaiseTracking();
        TestShortAllInDoesNotFullRaise();
        TestAllInAutoRunoutRule();
        TestSidePotAwardsWithDifferentWinners();
        TestFoldLeavesSingleWinner();
        return "Poker rule debug scenarios passed.";
    }

    private static void TestHeadsUpBlindOrder()
    {
        PlayerState dealer = Player("A", 1000, 0);
        PlayerState opponent = Player("B", 1000, 3);
        BlindPositionInfo info = PokerRuleUtility.CalculateBlindPositions(new[] { dealer, opponent }, dealer.SeatIndex, 6);

        Require(info.Dealer == dealer, "Heads-up dealer should stay on button.");
        Require(info.SmallBlind == dealer, "Heads-up dealer should be small blind.");
        Require(info.BigBlind == opponent, "Heads-up opponent should be big blind.");
        Require(info.PreflopFirstActor == dealer, "Heads-up preflop first actor should be dealer/small blind.");
        Require(info.PostflopFirstActor == opponent, "Heads-up postflop first actor should be big blind.");
    }

    private static void TestMultiwayBlindOrder()
    {
        PlayerState dealer = Player("A", 1000, 0);
        PlayerState smallBlind = Player("B", 1000, 2);
        PlayerState bigBlind = Player("C", 1000, 5);
        BlindPositionInfo info = PokerRuleUtility.CalculateBlindPositions(new[] { dealer, smallBlind, bigBlind }, dealer.SeatIndex, 6);

        Require(info.SmallBlind == smallBlind, "3+ small blind should be next active left of dealer.");
        Require(info.BigBlind == bigBlind, "3+ big blind should be next active after small blind.");
        Require(info.PreflopFirstActor == dealer, "3+ preflop first actor should be after big blind.");
        Require(info.PostflopFirstActor == smallBlind, "3+ postflop first actor should be left of dealer.");
    }

    private static void TestSidePotConstruction()
    {
        PlayerState a = Player("A", 1000, 0);
        PlayerState b = Player("B", 3000, 1);
        PlayerState c = Player("C", 3000, 2);
        BettingManager betting = CreateBetting(a, b, c);

        betting.ApplyBetOrRaise(a, 1000);
        betting.ApplyCall(b);
        betting.ApplyCall(c);
        betting.BeginNewStreet();
        betting.ApplyBetOrRaise(b, 2000);
        betting.ApplyCall(c);

        IReadOnlyList<SidePotInfo> pots = betting.GetSidePots();
        Require(pots.Count == 2, "One all-in plus side betting should create main pot and side pot.");
        Require(pots[0].Amount == 3000, "Main pot should be 1000 * 3.");
        Require(pots[1].Amount == 4000, "Side pot should be 2000 * 2.");
        Require(pots[0].EligiblePlayers.Contains(a), "All-in player should be eligible for main pot.");
        Require(!pots[1].EligiblePlayers.Contains(a), "All-in player should not be eligible for later side pot.");
        DestroyBetting(betting);
    }

    private static void TestMinimumRaiseTracking()
    {
        PlayerState smallBlind = Player("SB", 1000, 0);
        PlayerState bigBlind = Player("BB", 1000, 1);
        PlayerState raiser = Player("R", 1000, 2);
        BettingManager betting = CreateBetting(50, 100, smallBlind, bigBlind, raiser);

        betting.PostBlinds(smallBlind, bigBlind);
        betting.ApplyBetOrRaise(raiser, 300);

        Require(betting.LastActionWasFullRaise, "Raise from 100 to 300 should be a full raise.");
        Require(betting.LastFullRaiseAmount == 200, "Last full raise amount should become 200.");
        Require(betting.FullRaiseMinimumTarget() == 500, "Next minimum full raise target should be 500.");
        DestroyBetting(betting);
    }

    private static void TestShortAllInDoesNotFullRaise()
    {
        PlayerState smallBlind = Player("SB", 1000, 0);
        PlayerState bigBlind = Player("BB", 1000, 1);
        PlayerState raiser = Player("R", 1000, 2);
        PlayerState shortStack = Player("S", 450, 3);
        BettingManager betting = CreateBetting(50, 100, smallBlind, bigBlind, raiser, shortStack);

        betting.PostBlinds(smallBlind, bigBlind);
        betting.ApplyBetOrRaise(raiser, 300);
        betting.ApplyBetOrRaise(shortStack, 450);

        Require(betting.LastActionIncreasedBet, "Short all-in should still increase current bet.");
        Require(!betting.LastActionWasFullRaise, "Short all-in below previous raise size should not be a full raise.");
        Require(betting.LastFullRaiseAmount == 200, "Short all-in should not replace last full raise amount.");
        DestroyBetting(betting);
    }

    private static void TestAllInAutoRunoutRule()
    {
        PlayerState allIn = Player("A", 0, 0);
        PlayerState caller = Player("B", 1000, 1);
        PlayerState sidePotOne = Player("C", 1000, 2);
        PlayerState sidePotTwo = Player("D", 1000, 3);

        Require(
            PokerGameManager.ShouldAutoRunoutForRuleDebug(new[] { allIn, caller }, 3, PokerGameState.FlopBetting),
            "All-in plus one caller should auto-runout after the betting round closes.");

        Require(
            !PokerGameManager.ShouldAutoRunoutForRuleDebug(new[] { allIn, sidePotOne, sidePotTwo }, 3, PokerGameState.FlopBetting),
            "All-in plus two players with chips should continue side-pot betting.");
    }

    private static void TestSidePotAwardsWithDifferentWinners()
    {
        PlayerState a = Player("A", 1000, 0);
        PlayerState b = Player("B", 3000, 1);
        PlayerState c = Player("C", 3000, 2);
        BettingManager betting = CreateBetting(a, b, c);

        betting.ApplyBetOrRaise(a, 1000);
        betting.ApplyCall(b);
        betting.ApplyCall(c);
        betting.BeginNewStreet();
        betting.ApplyBetOrRaise(b, 2000);
        betting.ApplyCall(c);

        Dictionary<PlayerState, PokerHandResult> results = new Dictionary<PlayerState, PokerHandResult>
        {
            [a] = Result(PokerHandCategory.Flush, 14),
            [b] = Result(PokerHandCategory.OnePair, 2),
            [c] = Result(PokerHandCategory.TwoPair, 8, 4)
        };

        IReadOnlyList<PotAward> awards = betting.CalculateShowdownAwards(results);
        Require(awards.First(award => award.Player == a).Amount == 3000, "Main pot should go to all-in player A.");
        Require(awards.First(award => award.Player == c).Amount == 4000, "Side pot should go to player C.");
        DestroyBetting(betting);
    }

    private static void TestFoldLeavesSingleWinner()
    {
        PlayerState a = Player("A", 1000, 0);
        PlayerState b = Player("B", 1000, 1);
        PlayerState c = Player("C", 1000, 2);
        b.Fold();
        c.Fold();

        int unfolded = new[] { a, b, c }.Count(player => !player.HasFolded);
        Require(unfolded == 1, "When only one player has not folded, the hand should end by fold win.");
    }

    private static BettingManager CreateBetting(params PlayerState[] players)
    {
        return CreateBetting(50, 100, players);
    }

    private static BettingManager CreateBetting(int smallBlind, int bigBlind, params PlayerState[] players)
    {
        GameObject host = new GameObject("PokerRuleDebugBetting");
        BettingManager betting = host.AddComponent<BettingManager>();
        betting.Initialize(players, smallBlind, bigBlind);
        betting.ResetForNewHand();
        return betting;
    }

    private static void DestroyBetting(BettingManager betting)
    {
        if (betting != null)
        {
            UnityEngine.Object.DestroyImmediate(betting.gameObject);
        }
    }

    private static PlayerState Player(string name, int chips, int seat)
    {
        return new PlayerState(name.ToLowerInvariant(), name, chips, false, seat);
    }

    private static PokerHandResult Result(PokerHandCategory category, params int[] tiebreakers)
    {
        return new PokerHandResult(category, tiebreakers, Array.Empty<Card>());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

    public void BeginNewStreet()
    {
        CurrentBet = 0;
        LastFullRaiseAmount = BigBlind;
        LastActionIncreasedBet = false;
        LastActionWasFullRaise = false;
        foreach (PlayerState player in players)
        {
            player.ResetForBettingRound();
        }
    }

    public void PostBlinds(PlayerState smallBlindPlayer, PlayerState bigBlindPlayer)
    {
        int smallBlindPaid = smallBlindPlayer.PayChips(SmallBlind);
        Pot += smallBlindPaid;
        RecordContribution(smallBlindPlayer, smallBlindPaid);
        smallBlindPlayer.SetLastAction($"Small Blind {SmallBlind}");
        RaisePlayerPaidChips(smallBlindPlayer, smallBlindPaid);

        int bigBlindPaid = bigBlindPlayer.PayChips(BigBlind);
        Pot += bigBlindPaid;
        RecordContribution(bigBlindPlayer, bigBlindPaid);
        bigBlindPlayer.SetLastAction($"Big Blind {BigBlind}");
        RaisePlayerPaidChips(bigBlindPlayer, bigBlindPaid);
        CurrentBet = bigBlindPlayer.CurrentBet;
        LastFullRaiseAmount = BigBlind;
        LastActionIncreasedBet = CurrentBet > 0;
        LastActionWasFullRaise = true;

        Debug.Log($"[Betting] Blinds posted. SB={smallBlindPlayer.Nickname}:{SmallBlind}, BB={bigBlindPlayer.Nickname}:{BigBlind}, Pot={Pot}");
    }

    public int CallAmountFor(PlayerState player)
    {
        return Mathf.Max(0, CurrentBet - player.CurrentBet);    // 콜까지 남은 금액, 이미 기준 베팅액 이상 베팅한 경우 0
    }

    public int MinimumBetTarget(PlayerState player)
    {
        int maxTarget = MaximumBetTarget(player);
        int minTarget = FullRaiseMinimumTarget();
        return Mathf.Clamp(minTarget, 0, maxTarget);
    }

    public int FullRaiseMinimumTarget()
    {
        return CurrentBet <= 0 ? BigBlind : CurrentBet + Mathf.Max(BigBlind, LastFullRaiseAmount);
    }

    public int MaximumBetTarget(PlayerState player)
    {
        int stackTarget = player.CurrentBet + player.Chips;
        int opponentMaxTarget = players
            .Where(opponent => opponent != null && opponent != player && !opponent.HasFolded)
            .Select(opponent => opponent.CurrentBet + opponent.Chips)
            .DefaultIfEmpty(stackTarget)
            .Max();

        return Mathf.Min(stackTarget, opponentMaxTarget);
    }

    public bool CanCheck(PlayerState player)
    {
        return CallAmountFor(player) == 0;
    }

    public bool CanBetOrRaise(PlayerState player)
    {
        if (player == null || player.Chips <= 0 || player.HasFolded)
        {
            return false;
        }

        int maxTarget = MaximumBetTarget(player);
        return CurrentBet <= 0 ? maxTarget >= BigBlind : maxTarget > CurrentBet;
    }

    public void ApplyFold(PlayerState player)
    {
        LastActionIncreasedBet = false;
        LastActionWasFullRaise = false;
        player.Fold();
        Debug.Log($"[Betting] {player.Nickname} folds.");
    }

    public void ApplyCheck(PlayerState player)
    {
        if (!CanCheck(player))
        {
            ApplyCall(player);
            return;
        }

        player.SetLastAction("Check");
        LastActionIncreasedBet = false;
        LastActionWasFullRaise = false;
        Debug.Log($"[Betting] {player.Nickname} checks.");
    }

    public int ApplyCall(PlayerState player)
    {
        int callAmount = CallAmountFor(player);
        int paid = player.PayChips(callAmount);
        Pot += paid;
        RecordContribution(player, paid);
        player.SetLastAction(paid > 0 ? $"Call {paid}" : "Check");
        LastActionIncreasedBet = false;
        LastActionWasFullRaise = false;
        RaisePlayerPaidChips(player, paid);
        Debug.Log($"[Betting] {player.Nickname} calls {paid}. Pot={Pot}");
        return paid;
    }

    public int ApplyBetOrRaise(PlayerState player, int totalBetTarget)
    {
        int maxTarget = MaximumBetTarget(player);
        int fullRaiseMinimumTarget = FullRaiseMinimumTarget();
        int minTarget = Mathf.Min(fullRaiseMinimumTarget, maxTarget);
        int clampedTarget = Mathf.Clamp(totalBetTarget, minTarget, maxTarget);
        int previousCurrentBet = CurrentBet;

        if (CurrentBet > 0 && clampedTarget <= CurrentBet)
        {
            return ApplyCall(player);
        }

        int toPay = clampedTarget - player.CurrentBet;
        int paid = player.PayChips(toPay);
        Pot += paid;
        RecordContribution(player, paid);
        CurrentBet = Mathf.Max(CurrentBet, player.CurrentBet);
        int raiseAmount = CurrentBet - previousCurrentBet;
        LastActionIncreasedBet = raiseAmount > 0;
        LastActionWasFullRaise = LastActionIncreasedBet && raiseAmount >= Mathf.Max(BigBlind, LastFullRaiseAmount);
        if (LastActionWasFullRaise)
        {
            // 최소 레이즈는 빅블라인드 고정이 아니라 직전 유효 레이즈 폭을 기준으로 갱신합니다.
            LastFullRaiseAmount = raiseAmount;
        }

        RaisePlayerPaidChips(player, paid);

        string action = previousCurrentBet <= 0
            ? $"Bet {paid}"
            : LastActionWasFullRaise ? $"Raise to {player.CurrentBet}" : $"All-in {player.CurrentBet}";
        player.SetLastAction(action);
        Debug.Log($"[Betting] {player.Nickname} {action}. Pot={Pot}, CurrentBet={CurrentBet}, FullRaise={LastActionWasFullRaise}");
        return paid;
    }

    public IReadOnlyList<SidePotInfo> GetSidePots()
    {
        return BuildSidePots();
    }

    public IReadOnlyList<PotAward> CalculateShowdownAwards(IReadOnlyDictionary<PlayerState, PokerHandResult> handResults)
    {
        if (handResults == null || handResults.Count == 0)
        {
            return Array.Empty<PotAward>();
        }

        Dictionary<PlayerState, int> awards = new Dictionary<PlayerState, int>();
        foreach (SidePotInfo sidePot in BuildSidePots())
        {
            List<PlayerState> eligiblePlayers = sidePot.EligiblePlayers // 사이드팟에 기여한 플레이어 중에서 폴드하지 않은 플레이어만 승리 후보로 고려합니다.
                .Where(handResults.ContainsKey)
                .ToList();

            if (eligiblePlayers.Count == 0) // 현재 팟에 참가한 사람이 없으면 continue. 실제 게임에서는 있을 수 없는 상황이지만, 방어적으로 처리합니다.
            {
                continue;
            }

            PokerHandResult best = handResults[eligiblePlayers[0]];
            for (int i = 1; i < eligiblePlayers.Count; i++)
            {
                PokerHandResult result = handResults[eligiblePlayers[i]];
                if (result.CompareTo(best) > 0)
                {
                    best = result;
                }
            }

            List<PlayerState> winners = eligiblePlayers
                .Where(player => handResults[player].CompareTo(best) == 0)
                .ToList();

            int share = sidePot.Amount / winners.Count;
            int remainder = sidePot.Amount % winners.Count;
            for (int i = 0; i < winners.Count; i++)
            {
                // 추후 남는 칩은 버튼 기준 다음 플레이어에게 주는 것이 정식 규칙입니다. 현재는 참여 가능한 순서 앞쪽에 배정합니다.
                int amount = share + (i < remainder ? 1 : 0);
                if (!awards.ContainsKey(winners[i]))
                {
                    awards[winners[i]] = 0;
                }

                awards[winners[i]] += amount;
            }
        }

        return awards
            .Where(entry => entry.Value > 0)
            .Select(entry => new PotAward(entry.Key, entry.Value))
            .ToList();
    }

    public int ApplyPotAwards(IReadOnlyList<PotAward> awards)
    {
        if (awards == null || awards.Count == 0)
        {
            ClearPotState();
            return 0;
        }

        int total = 0;
        foreach (PotAward award in awards)
        {
            if (award.Player == null || award.Amount <= 0)
            {
                continue;
            }

            award.Player.WinChips(award.Amount);
            total += award.Amount;
            Debug.Log($"[Betting] {award.Player.Nickname} wins pot share {award.Amount}.");
        }

        ClearPotState();
        return total;
    }

    public int AwardPotTo(PlayerState winner)
    {
        int amount = Pot;
        winner.WinChips(amount);
        ClearPotState();
        Debug.Log($"[Betting] {winner.Nickname} wins pot {amount}.");
        return amount;
    }

    public int SplitPot(PlayerState first, PlayerState second)
    {
        int share = Pot / 2;
        first.WinChips(share);
        second.WinChips(Pot - share);
        int total = Pot;
        ClearPotState();
        Debug.Log($"[Betting] Split pot {total}. {first.Nickname}={share}, {second.Nickname}={total - share}");
        return total;
    }

    public int SplitPot(IReadOnlyList<PlayerState> winners)
    {
        if (winners == null || winners.Count == 0)
        {
            return 0;
        }

        int total = Pot;
        int share = Pot / winners.Count;
        int remainder = Pot % winners.Count;
        for (int i = 0; i < winners.Count; i++)
        {
            winners[i].WinChips(share + (i < remainder ? 1 : 0));
        }

        ClearPotState();
        Debug.Log($"[Betting] Split pot {total} between {winners.Count} players.");
        return total;
    }

    // contribution = Pot에 칩을 넣는 행위
    private void RecordContribution(PlayerState player, int paid)
    {
        if (player == null || paid <= 0)
        {
            return;
        }

        if (!handContributions.ContainsKey(player))
        {
            handContributions[player] = 0;
        }

        handContributions[player] += paid;
    }

    /// <summary>
    /// 사이드팟구조를 계산하여 반환합니다. 각 사이드팟은 해당 팟에 기여한 플레이어들과 폴드하지 않은 플레이어들을 기준으로 승리 후보가 결정됩니다.
    /// </summary>
    /// <returns></returns>
    private List<SidePotInfo> BuildSidePots()                                
    {
        List<int> levels = handContributions
            .Where(entry => entry.Key != null && entry.Value > 0)
            .Select(entry => entry.Value)
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        List<SidePotInfo> sidePots = new List<SidePotInfo>();
        int previousLevel = 0;
        foreach (int level in levels)
        {
            List<PlayerState> contributors = handContributions                  // 현재 레벨 이상 베팅한 플레이어들만 찾기
                .Where(entry => entry.Key != null && entry.Value >= level)
                .Select(entry => entry.Key)
                .ToList();

            int amount = (level - previousLevel) * contributors.Count;          // 현재 구간 팟 금액 계산
            List<PlayerState> eligiblePlayers = contributors                    // 폴드 안 한 플레이어만 승리 후보로
                .Where(player => !player.HasFolded)
                .ToList();

            if (amount > 0 && eligiblePlayers.Count > 0)
            {
                sidePots.Add(new SidePotInfo(amount, eligiblePlayers));         // 사이드팟 객체 생성
            }

            previousLevel = level;                                              // 다음 구간 계산을 위해 이전 레벨 업데이트
        }

        return sidePots;
    }

    private void ClearPotState()
    {
        Pot = 0;
        CurrentBet = 0;
        LastFullRaiseAmount = BigBlind;
        LastActionIncreasedBet = false;
        LastActionWasFullRaise = false;
        handContributions.Clear();
    }

    private void RaisePlayerPaidChips(PlayerState player, int paid)
    {
        if (paid > 0)
        {
            PlayerPaidChips?.Invoke(player, paid);
        }
    }

}
