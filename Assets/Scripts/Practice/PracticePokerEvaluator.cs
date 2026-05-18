using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public enum PracticePokerHandCategory
{
    HighCard = 1,
    OnePair = 2,
    TwoPair = 3,
    ThreeOfAKind = 4,
    Straight = 5,
    Flush = 6,
    FullHouse = 7,
    FourOfAKind = 8,
    StraightFlush = 9,
    RoyalFlush = 10
}

public class PracticePokerResult : MonoBehaviour
{
    public PracticePokerHandCategory Category { get; }

    public IReadOnlyList<int> Tiebreakers { get; }

    public IReadOnlyList<Card> Cards { get; }

    public PracticePokerResult(PracticePokerHandCategory category, IEnumerable<int> tiebreakers, IEnumerable<Card> cards)
    {
        this.Category = category;
        this.Tiebreakers = CopyToList(tiebreakers);
        this.Cards = CopyToList(cards);
    }

    public int CompareTo(PracticePokerResult other)
    {
        if (other == null)
            return 1;

        int categoryCompare = Category.CompareTo(other.Category);

        if (categoryCompare != 0)
            return categoryCompare;

        int count = Mathf.Max(Tiebreakers.Count, other.Tiebreakers.Count);

        for(int i = 0; i < count; i++)
        {
            int left = i < Tiebreakers.Count ? Tiebreakers[i] : 0;
            int right = i < other.Tiebreakers.Count ? other.Tiebreakers[i] : 0;
            int compare = left.CompareTo(right);

            if (compare != 0)
                return compare;
        }

        return 0;
    }

    public override string ToString()
    {
        return PracticeExtensions.PracticeToDisplayName(this.Category);
    }

    private static List<T> CopyToList<T>(IEnumerable<T> values)
    {
        List<T> list = new List<T>();
        if (values == null)
        {
            return list;
        }

        foreach (T value in values)
        {
            list.Add(value);
        }

        return list;
    }
}

public static class PracticeExtensions
{
    public static string PracticeToDisplayName(this PracticePokerHandCategory category)
    {
        switch (category)
        {
            case PracticePokerHandCategory.RoyalFlush:
                return "Royal Flush";
            case PracticePokerHandCategory.StraightFlush:
                return "Straight Flush";
            case PracticePokerHandCategory.FourOfAKind:
                return "Four of a Kind";
            case PracticePokerHandCategory.FullHouse:
                return "Full House";
            case PracticePokerHandCategory.Flush:
                return "Flush";
            case PracticePokerHandCategory.Straight:
                return "Straight";
            case PracticePokerHandCategory.ThreeOfAKind:
                return "Three of a Kind";
            case PracticePokerHandCategory.TwoPair:
                return "Two Pair";
            case PracticePokerHandCategory.OnePair:
                return "One Pair";
            default:
                return "High Card";
        }
    }
}

public static class PracticePokerEvaluator
{
    //public static PracticePokerResult EvaluateBest(IEnumerable<Card> sevenCards)
    //{
    //    List<Card> cards = new List<Card>(7);

    //    foreach(Card card in sevenCards)
    //    {
    //        cards.Add(card);
    //    }

    //    if(cards.Count < 5)
    //    {
    //        throw new ArgumentException("At least five cards are required to evaluate a poker hand");
    //    }

    //    PracticePokerResult best = null;
    //    Card[] combo = new Card[5];

    //    void PickCards(int startIndex, int depth)       // local 함수
    //    {
    //        // 카드 5장을 다 골랐으면 평가
    //        if (depth == 5)
    //        {
    //            //PracticePokerResult result = PracticePokerHandEvaluator.Evaluate(combo);

    //            if (best == null || result.CompareTo(best) > 0)
    //            {
    //                best = result;
    //            }

    //            return;
    //        }

    //        // startIndex부터 남은 카드 중 하나 선택
    //        for (int i = startIndex; i < cards.Count; i++)
    //        {
    //            combo[depth] = cards[i];

    //            // 다음 카드는 현재 고른 카드 다음부터 선택
    //            PickCards(i + 1, depth + 1);
    //        }
    //    }

    //    PickCards(0, 0);
    //}
}


