using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 봇이 이번 턴에 선택한 액션과, 베팅/레이즈일 경우 목표 총 베팅액입니다.
public readonly struct PokerDecision
{
    public readonly BettingAction Action;
    public readonly int TotalBetTarget;

    public PokerDecision(BettingAction action, int totalBetTarget = 0)
    {
        Action = action;
        TotalBetTarget = totalBetTarget;
    }
}

// 간단한 확률 기반 포커 AI입니다.
// 현재 패 강도, 콜 금액, 블러프 확률, 레이즈 제한을 보고 폴드/체크/콜/베팅/레이즈 중 하나를 고릅니다.
public sealed class SimplePokerAI : MonoBehaviour, IPokerAI
{
    [Range(0f, 1f)]
    [SerializeField] private float bluffChance = 0.12f;

    [Range(0f, 1f)]
    [SerializeField] private float caution = 0.25f;

    public PokerDecision Decide(
        PlayerState ai,
        IReadOnlyList<Card> communityCards,
        BettingManager betting,
        int raiseCount,
        int maxRaisesPerRound)
    {
        return Decide(ai, communityCards, betting, raiseCount, maxRaisesPerRound, null);
    }

    public PokerDecision Decide(
        PlayerState ai,
        IReadOnlyList<Card> communityCards,
        BettingManager betting,
        int raiseCount,
        int maxRaisesPerRound,
        IReadOnlyList<PlayerState> handPlayers)
    {
        int callAmount = betting.CallAmountFor(ai); 
        float strength = EstimateStrength(ai.HoleCards, communityCards);
        bool canRaise = betting.CanBetOrRaise(ai) && raiseCount < maxRaisesPerRound;
        float roll = Random.value;

        if (callAmount > 0)
        {
            float foldThreshold = Mathf.Lerp(0.55f, 0.02f, strength) + caution * 0.2f;
            if (roll < foldThreshold && callAmount > betting.BigBlind && strength < 0.55f)
            {
                return new PokerDecision(BettingAction.Fold);
            }

            if (canRaise && (strength > 0.68f || roll < bluffChance))
            {
                return new PokerDecision(BettingAction.Raise, ChooseRaiseTarget(ai, betting, strength));
            }

            return new PokerDecision(BettingAction.Call);
        }

        if (canRaise && (strength > 0.62f || roll < bluffChance * 0.75f))
        {
            return new PokerDecision(BettingAction.Bet, ChooseRaiseTarget(ai, betting, strength));
        }

        return new PokerDecision(BettingAction.Check);
    }

    public float ChooseThinkDelay(
        PlayerState ai,
        IReadOnlyList<Card> communityCards,
        BettingManager betting,
        float fallbackDelay,
        float actionTimeLimit,
        IReadOnlyList<PlayerState> handPlayers)
    {
        return actionTimeLimit > 0f
            ? Mathf.Clamp(fallbackDelay, 0f, actionTimeLimit)
            : Mathf.Max(0f, fallbackDelay);
    }

    private static int ChooseRaiseTarget(PlayerState ai, BettingManager betting, float strength)
    {
        int minTarget = betting.MinimumBetTarget(ai);
        int maxTarget = betting.MaximumBetTarget(ai);
        int extra = Mathf.RoundToInt(Mathf.Lerp(0f, betting.BigBlind * 3f, strength) / betting.BigBlind) * betting.BigBlind;
        return Mathf.Clamp(minTarget + extra, minTarget, maxTarget);
    }

    private static float EstimateStrength(IReadOnlyList<Card> holeCards, IReadOnlyList<Card> communityCards)
    {
        List<Card> cards = new List<Card>(holeCards.Count + communityCards.Count);
        cards.AddRange(holeCards);
        cards.AddRange(communityCards);

        if (cards.Count >= 5)
        {
            PokerHandResult result = PokerHandEvaluator.EvaluateBest(cards);
            return Mathf.InverseLerp((float)PokerHandCategory.HighCard, (float)PokerHandCategory.RoyalFlush, (float)result.Category);
        }

        int high = holeCards.Max(card => card.RankValue);
        int low = holeCards.Min(card => card.RankValue);
        bool pair = holeCards[0].Rank == holeCards[1].Rank;
        bool suited = holeCards[0].Suit == holeCards[1].Suit;
        bool connected = Mathf.Abs(holeCards[0].RankValue - holeCards[1].RankValue) <= 2;

        float score = Mathf.InverseLerp(7f, 14f, high) * 0.45f + Mathf.InverseLerp(2f, 14f, low) * 0.2f;
        if (pair)
        {
            score += 0.35f;
        }

        if (suited)
        {
            score += 0.08f;
        }

        if (connected)
        {
            score += 0.07f;
        }

        return Mathf.Clamp01(score);
    }
}
