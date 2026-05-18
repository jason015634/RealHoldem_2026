using System.Collections.Generic;

public interface IPokerAI
{
    PokerDecision Decide(
        PlayerState ai,
        IReadOnlyList<Card> communityCards,
        BettingManager betting,
        int raiseCount,
        int maxRaisesPerRound,
        IReadOnlyList<PlayerState> handPlayers);

    float ChooseThinkDelay(
        PlayerState ai,
        IReadOnlyList<Card> communityCards,
        BettingManager betting,
        float fallbackDelay,
        float actionTimeLimit,
        IReadOnlyList<PlayerState> handPlayers);
}
