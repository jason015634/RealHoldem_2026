using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum BotMood
{
    Normal,
    Confident,
    Tilted,
    Cautious
}

[System.Serializable]
public class PokerBotProfile
{
    [Range(0f, 1f)] public float aggression = 0.5f;
    [Range(0f, 1f)] public float bluffChance = 0.12f;
    [Range(0f, 1f)] public float caution = 0.25f;
    [Range(0f, 1f)] public float looseCall = 0.4f;
    [Range(0f, 1f)] public float slowPlayChance = 0.1f;
    [Range(0f, 1f)] public float mistakeChance = 0.03f;
    [Range(0f, 1f)] public float memoryInfluence = 0.45f;
    [Range(0f, 1f)] public float storyContinuationChance = 0.55f;
    [Range(0f, 1f)] public float scareCardBluffChance = 0.18f;
    [Min(0f)] public float minimumThinkDelay = 0.25f;
    [Min(0f)] public float maximumThinkDelay = 1.8f;
    [Range(0f, 1f)] public float snapDecisionChance = 0.12f;
    [Range(0f, 1f)] public float distractedChance = 0.16f;
    [Min(0f)] public float distractedExtraDelayMin = 0.75f;
    [Min(0f)] public float distractedExtraDelayMax = 2.8f;
}

public sealed class HumanLikePokerAI : MonoBehaviour, IPokerAI
{
    [SerializeField] private PokerBotProfile profile = new PokerBotProfile();
    [SerializeField] private BotMood mood = BotMood.Normal;

    private readonly Dictionary<string, BotRuntimeMemory> botMemories = new Dictionary<string, BotRuntimeMemory>();
    private readonly Dictionary<string, OpponentProfile> opponentProfiles = new Dictionary<string, OpponentProfile>();
    private readonly HashSet<string> observedActionKeys = new HashSet<string>();

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
        BotRuntimeMemory memory = PrepareMemory(ai, communityCards, handPlayers);
        ObserveTableActions(handPlayers, communityCards);
        BotMood effectiveMood = UpdateMoodFromStack(ai, memory);

        int callAmount = betting.CallAmountFor(ai);
        bool canBetOrRaise = CanBetOrRaise(ai, betting, raiseCount, maxRaisesPerRound);

        float madeStrength = EstimateMadeStrength(ai.HoleCards, communityCards);
        float drawPotential = EstimateDrawPotential(ai.HoleCards, communityCards);
        float potOdds = CalculatePotOdds(betting, callAmount);
        float strength = Mathf.Clamp01(madeStrength + drawPotential);

        ApplyMood(ref strength, effectiveMood);

        if (Random.value < profile.mistakeChance)
        {
            PokerDecision mistake = ChooseHumanMistake(ai, betting, callAmount, canBetOrRaise);
            RememberDecision(memory, mistake, communityCards, strength);
            return mistake;
        }

        if (strength > 0.85f && Random.value < profile.slowPlayChance)
        {
            PokerDecision slowPlay = callAmount > 0
                ? new PokerDecision(BettingAction.Call)
                : new PokerDecision(BettingAction.Check);
            RememberDecision(memory, slowPlay, communityCards, strength);
            return slowPlay;
        }

        float foldPressure = EstimateOpponentFoldPressure(ai, handPlayers);
        bool scareCard = IsScareCard(communityCards, memory);
        PokerDecision decision = callAmount > 0
            ? DecideFacingBet(ai, betting, callAmount, canBetOrRaise, strength, potOdds, effectiveMood, foldPressure, scareCard, memory)
            : DecideNoBet(ai, betting, canBetOrRaise, strength, effectiveMood, foldPressure, scareCard, memory);

        RememberDecision(memory, decision, communityCards, strength);
        return decision;
    }

    public float ChooseThinkDelay(
        PlayerState ai,
        IReadOnlyList<Card> communityCards,
        BettingManager betting,
        float fallbackDelay,
        float actionTimeLimit)
    {
        return ChooseThinkDelay(ai, communityCards, betting, fallbackDelay, actionTimeLimit, null);
    }

    public float ChooseThinkDelay(
        PlayerState ai,
        IReadOnlyList<Card> communityCards,
        BettingManager betting,
        float fallbackDelay,
        float actionTimeLimit,
        IReadOnlyList<PlayerState> handPlayers)
    {
        BotRuntimeMemory memory = PrepareMemory(ai, communityCards, handPlayers);
        BotMood effectiveMood = UpdateMoodFromStack(ai, memory);
        float minDelay = Mathf.Max(0f, profile.minimumThinkDelay);
        float maxDelay = Mathf.Max(minDelay, profile.maximumThinkDelay);
        float delay = maxDelay > minDelay ? Random.Range(minDelay, maxDelay) : minDelay;

        int callAmount = betting.CallAmountFor(ai);
        float madeStrength = EstimateMadeStrength(ai.HoleCards, communityCards);
        float drawPotential = EstimateDrawPotential(ai.HoleCards, communityCards);
        float strength = Mathf.Clamp01(madeStrength + drawPotential);
        float potOdds = CalculatePotOdds(betting, callAmount);

        float uncertainty = 1f - Mathf.Abs(strength - 0.5f) * 2f;
        float streetComplexity = communityCards != null ? Mathf.InverseLerp(0f, 5f, communityCards.Count) : 0f;
        float pressure = callAmount > 0 ? Mathf.Clamp01(potOdds * 2f) : 0f;
        float decisionWeight = Mathf.Clamp01(uncertainty * 0.45f + streetComplexity * 0.25f + pressure * 0.3f);
        delay += Mathf.Lerp(0f, maxDelay - minDelay, decisionWeight);

        if (memory.DecisionsThisHand > 2 && memory.WasAggressor)
        {
            delay *= 0.92f;
        }

        if (effectiveMood == BotMood.Confident)
        {
            delay *= 0.85f;
        }
        else if (effectiveMood == BotMood.Cautious)
        {
            delay *= 1.2f;
        }
        else if (effectiveMood == BotMood.Tilted)
        {
            delay *= Random.value < 0.55f ? 0.65f : 1.25f;
        }

        if (Random.value < profile.snapDecisionChance)
        {
            delay = Random.Range(minDelay, Mathf.Lerp(minDelay, maxDelay, 0.3f));
        }
        else if (Random.value < profile.distractedChance)
        {
            float extraMin = Mathf.Max(0f, profile.distractedExtraDelayMin);
            float extraMax = Mathf.Max(extraMin, profile.distractedExtraDelayMax);
            delay += extraMax > extraMin ? Random.Range(extraMin, extraMax) : extraMin;
        }

        if (float.IsNaN(delay) || float.IsInfinity(delay))
        {
            delay = Mathf.Max(0f, fallbackDelay);
        }

        if (actionTimeLimit > 0f)
        {
            delay = Mathf.Min(delay, Mathf.Max(0.1f, actionTimeLimit * 0.85f));
        }

        return Mathf.Max(0f, delay);
    }

    private PokerDecision DecideFacingBet(
        PlayerState ai,
        BettingManager betting,
        int callAmount,
        bool canRaise,
        float strength,
        float potOdds,
        BotMood effectiveMood,
        float foldPressure,
        bool scareCard,
        BotRuntimeMemory memory)
    {
        float adjustedBluffChance = profile.bluffChance;
        float callComfort = strength + profile.looseCall * 0.15f;

        if (effectiveMood == BotMood.Tilted)
        {
            adjustedBluffChance += 0.1f;
            callComfort += 0.08f;
        }
        else if (effectiveMood == BotMood.Cautious)
        {
            adjustedBluffChance *= 0.5f;
            callComfort -= 0.06f;
        }

        adjustedBluffChance += foldPressure * profile.memoryInfluence * 0.12f;
        if (scareCard && memory.WasAggressor)
        {
            adjustedBluffChance += profile.scareCardBluffChance;
        }

        bool shouldFold = callComfort < potOdds
            && strength < 0.55f
            && callAmount > betting.BigBlind;

        if (shouldFold)
        {
            float foldChance = Mathf.Lerp(0.85f, 0.15f, strength);
            foldChance += profile.caution * 0.25f;

            if (Random.value < foldChance)
            {
                return new PokerDecision(BettingAction.Fold);
            }
        }

        bool valueRaise = strength > Mathf.Lerp(0.75f, 0.58f, profile.aggression);
        bool storyRaise = memory.WasAggressor
            && memory.LastAggressiveStreet < GetStreetIndex(memory.LastCommunityCount)
            && strength > 0.42f
            && Random.value < profile.storyContinuationChance * profile.memoryInfluence;
        bool bluffRaise = Random.value < adjustedBluffChance && strength < 0.55f;

        if (canRaise && (valueRaise || bluffRaise || storyRaise))
        {
            return new PokerDecision(
                BettingAction.Raise,
                ChooseRaiseTarget(ai, betting, strength, bluffRaise || storyRaise));
        }

        return new PokerDecision(BettingAction.Call);
    }

    private PokerDecision DecideNoBet(
        PlayerState ai,
        BettingManager betting,
        bool canBet,
        float strength,
        BotMood effectiveMood,
        float foldPressure,
        bool scareCard,
        BotRuntimeMemory memory)
    {
        float adjustedBluffChance = profile.bluffChance * 0.75f;

        if (effectiveMood == BotMood.Confident)
        {
            adjustedBluffChance += 0.05f;
        }
        else if (effectiveMood == BotMood.Cautious)
        {
            adjustedBluffChance *= 0.65f;
        }

        adjustedBluffChance += foldPressure * profile.memoryInfluence * 0.1f;
        if (scareCard && memory.WasAggressor)
        {
            adjustedBluffChance += profile.scareCardBluffChance;
        }

        bool valueBet = strength > Mathf.Lerp(0.7f, 0.52f, profile.aggression);
        bool continuationBet = memory.WasAggressor
            && memory.LastAggressiveStreet < GetStreetIndex(memory.LastCommunityCount)
            && strength > 0.38f
            && Random.value < profile.storyContinuationChance;
        bool bluffBet = Random.value < adjustedBluffChance && strength < 0.5f;

        if (canBet && (valueBet || bluffBet || continuationBet))
        {
            return new PokerDecision(
                BettingAction.Bet,
                ChooseRaiseTarget(ai, betting, strength, bluffBet || continuationBet));
        }

        return new PokerDecision(BettingAction.Check);
    }

    private void ApplyMood(ref float strength, BotMood effectiveMood)
    {
        switch (effectiveMood)
        {
            case BotMood.Confident:
                strength += 0.05f;
                break;
            case BotMood.Tilted:
                strength += 0.08f;
                break;
            case BotMood.Cautious:
                strength -= 0.05f;
                break;
        }

        strength = Mathf.Clamp01(strength);
    }

    private PokerDecision ChooseHumanMistake(
        PlayerState ai,
        BettingManager betting,
        int callAmount,
        bool canBetOrRaise)
    {
        if (callAmount > 0)
        {
            if (Random.value < 0.65f)
            {
                return new PokerDecision(BettingAction.Call);
            }

            return new PokerDecision(BettingAction.Fold);
        }

        if (canBetOrRaise && Random.value < 0.25f)
        {
            return new PokerDecision(
                BettingAction.Bet,
                betting.MinimumBetTarget(ai));
        }

        return new PokerDecision(BettingAction.Check);
    }

    private int ChooseRaiseTarget(
        PlayerState ai,
        BettingManager betting,
        float strength,
        bool isBluff)
    {
        int minTarget = betting.MinimumBetTarget(ai);
        int maxTarget = betting.MaximumBetTarget(ai);

        if (maxTarget <= minTarget)
        {
            return maxTarget;
        }

        float baseMultiplier = Mathf.Lerp(1.2f, 5f, strength);
        baseMultiplier *= Mathf.Lerp(0.75f, 1.45f, profile.aggression);

        if (isBluff)
        {
            baseMultiplier *= Random.Range(0.75f, 1.15f);
        }
        else
        {
            baseMultiplier *= Random.Range(0.85f, 1.35f);
        }

        int extra = Mathf.RoundToInt(betting.BigBlind * baseMultiplier);
        extra = RoundToBlind(extra, betting.BigBlind);

        return Mathf.Clamp(minTarget + extra, minTarget, maxTarget);
    }

    private static bool CanBetOrRaise(
        PlayerState ai,
        BettingManager betting,
        int raiseCount,
        int maxRaisesPerRound)
    {
        if (raiseCount >= maxRaisesPerRound || ai.Chips <= 0 || !betting.CanBetOrRaise(ai))
        {
            return false;
        }

        int minTarget = betting.MinimumBetTarget(ai);
        int maxTarget = betting.MaximumBetTarget(ai);
        return maxTarget >= minTarget && maxTarget > betting.CurrentBet;
    }

    private static int RoundToBlind(int value, int bigBlind)
    {
        if (bigBlind <= 0)
        {
            return value;
        }

        return Mathf.Max(bigBlind, Mathf.RoundToInt(value / (float)bigBlind) * bigBlind);
    }

    private BotRuntimeMemory PrepareMemory(
        PlayerState ai,
        IReadOnlyList<Card> communityCards,
        IReadOnlyList<PlayerState> handPlayers)
    {
        string key = ai.PlayerId ?? ai.Nickname;
        if (!botMemories.TryGetValue(key, out BotRuntimeMemory memory))
        {
            memory = new BotRuntimeMemory { CurrentMood = mood, LastKnownChips = ai.Chips };
            botMemories[key] = memory;
        }

        string holeSignature = GetHoleSignature(ai.HoleCards);
        memory.NewHandStarted = ai.HoleCards.Count >= 2 && memory.LastHoleSignature != holeSignature;
        if (memory.NewHandStarted)
        {
            memory.LastHoleSignature = holeSignature;
            memory.WasAggressor = false;
            memory.LastAggressiveStreet = -1;
            memory.DecisionsThisHand = 0;
            memory.LastOwnAction = string.Empty;
            memory.LastStrength = 0f;
        }

        memory.LastCommunityCount = communityCards != null ? communityCards.Count : 0;
        memory.ActiveOpponentCount = handPlayers != null
            ? handPlayers.Count(player => player != null && player != ai && !player.HasFolded)
            : 0;

        return memory;
    }

    private void ObserveTableActions(
        IReadOnlyList<PlayerState> handPlayers,
        IReadOnlyList<Card> communityCards)
    {
        if (handPlayers == null)
        {
            return;
        }

        int street = GetStreetIndex(communityCards != null ? communityCards.Count : 0);
        foreach (PlayerState player in handPlayers)
        {
            if (player == null || string.IsNullOrEmpty(player.LastAction))
            {
                continue;
            }

            string action = player.LastAction;
            if (action.StartsWith("Small Blind") || action.StartsWith("Big Blind"))
            {
                continue;
            }

            string key = $"{player.PlayerId}:{street}:{player.CurrentBet}:{action}";
            if (!observedActionKeys.Add(key))
            {
                continue;
            }

            OpponentProfile observed = GetOpponentProfile(player);
            if (action == "Fold")
            {
                observed.Folds++;
            }
            else if (action.StartsWith("Call"))
            {
                observed.Calls++;
            }
            else if (action == "Check")
            {
                observed.Checks++;
            }
            else if (action.StartsWith("Bet") || action.StartsWith("Raise"))
            {
                observed.BetsOrRaises++;
            }
        }
    }

    private BotMood UpdateMoodFromStack(PlayerState ai, BotRuntimeMemory memory)
    {
        if (memory.LastKnownChips < 0)
        {
            memory.LastKnownChips = ai.Chips;
        }

        if (memory.NewHandStarted)
        {
            int delta = ai.Chips - memory.LastKnownChips;
            if (delta <= -Mathf.Max(200, memory.LastKnownChips / 5))
            {
                memory.CurrentMood = BotMood.Tilted;
                memory.MoodTurnsRemaining = 3;
            }
            else if (delta >= Mathf.Max(200, memory.LastKnownChips / 5))
            {
                memory.CurrentMood = BotMood.Confident;
                memory.MoodTurnsRemaining = 2;
            }
            else if (ai.Chips <= 300)
            {
                memory.CurrentMood = BotMood.Cautious;
                memory.MoodTurnsRemaining = 2;
            }
            else if (memory.MoodTurnsRemaining > 0)
            {
                memory.MoodTurnsRemaining--;
            }
            else
            {
                memory.CurrentMood = mood;
            }
        }

        memory.LastKnownChips = ai.Chips;
        return memory.CurrentMood;
    }

    private float EstimateOpponentFoldPressure(
        PlayerState ai,
        IReadOnlyList<PlayerState> handPlayers)
    {
        if (handPlayers == null)
        {
            return 0f;
        }

        List<OpponentProfile> opponents = handPlayers
            .Where(player => player != null && player != ai && !player.HasFolded && !player.IsAllIn)
            .Select(GetOpponentProfile)
            .ToList();

        if (opponents.Count == 0)
        {
            return 0f;
        }

        return opponents.Average(profile => profile.FoldPressure);
    }

    private OpponentProfile GetOpponentProfile(PlayerState player)
    {
        string key = player.PlayerId ?? player.Nickname;
        if (!opponentProfiles.TryGetValue(key, out OpponentProfile observed))
        {
            observed = new OpponentProfile();
            opponentProfiles[key] = observed;
        }

        return observed;
    }

    private static bool IsScareCard(
        IReadOnlyList<Card> communityCards,
        BotRuntimeMemory memory)
    {
        if (communityCards == null || communityCards.Count == 0 || communityCards.Count == memory.PreviousCommunityCount)
        {
            return false;
        }

        Card newest = communityCards[communityCards.Count - 1];
        bool highCard = newest.RankValue >= 11;
        bool pairedBoard = communityCards.Count(card => card.Rank == newest.Rank) >= 2;
        bool flushThreat = communityCards.Count(card => card.Suit == newest.Suit) >= 3;
        bool straightThreat = HasStraightDraw(communityCards.ToList());
        return highCard || pairedBoard || flushThreat || straightThreat;
    }

    private static int GetStreetIndex(int communityCount)
    {
        if (communityCount <= 0)
        {
            return 0;
        }

        if (communityCount <= 3)
        {
            return 1;
        }

        return communityCount == 4 ? 2 : 3;
    }

    private static string GetHoleSignature(IReadOnlyList<Card> holeCards)
    {
        if (holeCards == null || holeCards.Count < 2)
        {
            return string.Empty;
        }

        return string.Join("|", holeCards.Select(card => card.ShortName()).OrderBy(value => value));
    }

    private static void RememberDecision(
        BotRuntimeMemory memory,
        PokerDecision decision,
        IReadOnlyList<Card> communityCards,
        float strength)
    {
        int communityCount = communityCards != null ? communityCards.Count : 0;
        int street = GetStreetIndex(communityCount);

        memory.DecisionsThisHand++;
        memory.LastOwnAction = decision.Action.ToString();
        memory.LastStrength = strength;
        memory.PreviousCommunityCount = communityCount;

        if (decision.Action == BettingAction.Bet || decision.Action == BettingAction.Raise)
        {
            memory.WasAggressor = true;
            memory.LastAggressiveStreet = street;
        }
    }

    private static float CalculatePotOdds(BettingManager betting, int callAmount)
    {
        if (callAmount <= 0)
        {
            return 0f;
        }

        return callAmount / (float)(betting.Pot + callAmount);
    }

    private static float EstimateMadeStrength(
        IReadOnlyList<Card> holeCards,
        IReadOnlyList<Card> communityCards)
    {
        if (holeCards == null || holeCards.Count < 2)
        {
            return 0f;
        }

        List<Card> cards = new List<Card>(holeCards.Count + communityCards.Count);
        cards.AddRange(holeCards);
        cards.AddRange(communityCards);

        if (cards.Count >= 5)
        {
            PokerHandResult result = PokerHandEvaluator.EvaluateBest(cards);
            float categoryStrength = Mathf.InverseLerp(
                (float)PokerHandCategory.HighCard,
                (float)PokerHandCategory.RoyalFlush,
                (float)result.Category);

            float kickerStrength = result.Tiebreakers.Count > 0
                ? Mathf.InverseLerp(2f, 14f, result.Tiebreakers[0]) * 0.08f
                : 0f;

            float boardPenalty = EstimateBoardPenalty(result, communityCards);
            return Mathf.Clamp01(categoryStrength + kickerStrength - boardPenalty);
        }

        int high = holeCards.Max(card => card.RankValue);
        int low = holeCards.Min(card => card.RankValue);

        bool pair = holeCards[0].Rank == holeCards[1].Rank;
        bool suited = holeCards[0].Suit == holeCards[1].Suit;
        bool connected = Mathf.Abs(holeCards[0].RankValue - holeCards[1].RankValue) <= 2;

        float score =
            Mathf.InverseLerp(7f, 14f, high) * 0.45f +
            Mathf.InverseLerp(2f, 14f, low) * 0.2f;

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

    private static float EstimateBoardPenalty(
        PokerHandResult fullResult,
        IReadOnlyList<Card> communityCards)
    {
        if (communityCards == null || communityCards.Count < 5)
        {
            return 0f;
        }

        PokerHandResult boardResult = PokerHandEvaluator.EvaluateBest(communityCards);
        if (fullResult.CompareTo(boardResult) <= 0)
        {
            return 0.22f;
        }

        return fullResult.Category == boardResult.Category ? 0.08f : 0f;
    }

    private static float EstimateDrawPotential(
        IReadOnlyList<Card> holeCards,
        IReadOnlyList<Card> communityCards)
    {
        if (holeCards == null || communityCards == null || communityCards.Count < 3 || communityCards.Count >= 5)
        {
            return 0f;
        }

        List<Card> cards = new List<Card>(holeCards.Count + communityCards.Count);
        cards.AddRange(holeCards);
        cards.AddRange(communityCards);

        float score = 0f;

        if (HasFlushDraw(cards))
        {
            score += 0.22f;
        }

        if (HasStraightDraw(cards))
        {
            score += 0.18f;
        }

        return Mathf.Clamp01(score);
    }

    private static bool HasFlushDraw(List<Card> cards)
    {
        return cards
            .GroupBy(card => card.Suit)
            .Any(group => group.Count() >= 4);
    }

    private static bool HasStraightDraw(List<Card> cards)
    {
        List<int> ranks = cards
            .Select(card => card.RankValue)
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        if (ranks.Contains(14))
        {
            ranks.Insert(0, 1);
        }

        for (int i = 0; i < ranks.Count; i++)
        {
            int count = 1;
            int previous = ranks[i];

            for (int j = i + 1; j < ranks.Count; j++)
            {
                if (ranks[j] == previous + 1)
                {
                    count++;
                    previous = ranks[j];

                    if (count >= 4)
                    {
                        return true;
                    }
                }
                else if (ranks[j] > previous + 1)
                {
                    break;
                }
            }
        }

        return false;
    }

    private sealed class BotRuntimeMemory
    {
        public string LastHoleSignature = string.Empty;
        public int LastCommunityCount;
        public int PreviousCommunityCount;
        public int LastKnownChips = -1;
        public int DecisionsThisHand;
        public int ActiveOpponentCount;
        public int MoodTurnsRemaining;
        public int LastAggressiveStreet = -1;
        public float LastStrength;
        public bool NewHandStarted;
        public bool WasAggressor;
        public string LastOwnAction = string.Empty;
        public BotMood CurrentMood = BotMood.Normal;
    }

    private sealed class OpponentProfile
    {
        public int Folds;
        public int Calls;
        public int Checks;
        public int BetsOrRaises;

        public float FoldPressure
        {
            get
            {
                int passiveActions = Folds + Calls + Checks;
                int total = passiveActions + BetsOrRaises;
                if (total <= 0)
                {
                    return 0.35f;
                }

                float foldRate = Folds / (float)total;
                float passiveRate = (Folds + Checks) / (float)total;
                float aggressionPenalty = BetsOrRaises / (float)total * 0.35f;
                return Mathf.Clamp01(foldRate * 0.7f + passiveRate * 0.3f - aggressionPenalty);
            }
        }
    }
}
