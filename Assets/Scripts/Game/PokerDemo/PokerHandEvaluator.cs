using System;
using System.Collections.Generic;

public enum PokerHandCategory
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

public sealed class PokerHandResult : IComparable<PokerHandResult>
{
    public PokerHandCategory Category { get; }
    public IReadOnlyList<int> Tiebreakers { get; }
    public IReadOnlyList<Card> Cards { get; }

    public PokerHandResult(PokerHandCategory category, IEnumerable<int> tiebreakers, IEnumerable<Card> cards)
    {
        Category = category;
        Tiebreakers = CopyToList(tiebreakers);
        Cards = CopyToList(cards);
    }

    public int CompareTo(PokerHandResult other)
    {
        if (other == null)
        {
            return 1;
        }

        int categoryCompare = Category.CompareTo(other.Category);       // enum 결과값 비교, 값이 일치하면 0, other이 더 크면 -1, this가 더 크면 1 반환
        if (categoryCompare != 0)
        {
            return categoryCompare;
        }

        int count = Math.Max(Tiebreakers.Count, other.Tiebreakers.Count);
        for (int i = 0; i < count; i++)
        {
            int left = i < Tiebreakers.Count ? Tiebreakers[i] : 0;
            int right = i < other.Tiebreakers.Count ? other.Tiebreakers[i] : 0;
            int compare = left.CompareTo(right);
            if (compare != 0)
            {
                return compare;
            }
        }

        return 0;
    }

    public override string ToString()
    {
        return Category.ToDisplayName();
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

public static class PokerHandCategoryExtensions
{
    public static string ToDisplayName(this PokerHandCategory category) // static 클래스 안 static 매서드에 this 키워드를 붙이면 확장 메서드가 된다. 확장 메서드는 해당 타입의 인스턴스에서 마치 인스턴스 메서드인 것처럼 호출할 수 있다.
    {
        switch (category)
        {
            case PokerHandCategory.RoyalFlush:
                return "Royal Flush";
            case PokerHandCategory.StraightFlush:
                return "Straight Flush";
            case PokerHandCategory.FourOfAKind:
                return "Four of a Kind";
            case PokerHandCategory.FullHouse:
                return "Full House";
            case PokerHandCategory.Flush:
                return "Flush";
            case PokerHandCategory.Straight:
                return "Straight";
            case PokerHandCategory.ThreeOfAKind:
                return "Three of a Kind";
            case PokerHandCategory.TwoPair:
                return "Two Pair";
            case PokerHandCategory.OnePair:
                return "One Pair";
            default:
                return "High Card";
        }
    }
}

public static class PokerHandEvaluator
{
    public static PokerHandResult EvaluateBest(IEnumerable<Card> sevenCards)
    {
        List<Card> cards = new List<Card>(7);
        foreach (Card card in sevenCards)
        {
            cards.Add(card);
        }

        if (cards.Count < 5)
        {
            throw new ArgumentException("At least five cards are required to evaluate a poker hand.");
        }

        PokerHandResult best = null;
        Card[] combo = new Card[5];

        for (int a = 0; a < cards.Count - 4; a++)
        for (int b = a + 1; b < cards.Count - 3; b++)
        for (int c = b + 1; c < cards.Count - 2; c++)
        for (int d = c + 1; d < cards.Count - 1; d++)
        for (int e = d + 1; e < cards.Count; e++)
        {
            combo[0] = cards[a];
            combo[1] = cards[b];
            combo[2] = cards[c];
            combo[3] = cards[d];
            combo[4] = cards[e];

            PokerHandResult result = EvaluateFive(combo);
            if (best == null || result.CompareTo(best) > 0)
            {
                best = result;
            }
        }

        return best;
    }

    private static PokerHandResult EvaluateFive(IReadOnlyList<Card> cards)
    {
        int[] ranks = new int[5];
        int[] rankCounts = new int[15];
        bool[] rankPresent = new bool[15];

        bool isFlush = true;
        Suit firstSuit = cards[0].Suit;
        for (int i = 0; i < cards.Count; i++)
        {
            int rank = cards[i].RankValue;
            ranks[i] = rank;
            rankCounts[rank]++;
            rankPresent[rank] = true;

            if (cards[i].Suit != firstSuit)
            {
                isFlush = false;
            }
        }

        SortDescending(ranks, cards.Count);
        int straightHigh = GetStraightHigh(rankPresent);

        if (isFlush && straightHigh > 0)
        {
            PokerHandCategory category = straightHigh == 14
                ? PokerHandCategory.RoyalFlush
                : PokerHandCategory.StraightFlush;
            return new PokerHandResult(category, new[] { straightHigh }, cards);
        }

        int fourRank = 0;
        int tripRank = 0;
        int secondTripRank = 0;
        int firstPairRank = 0;
        int secondPairRank = 0;

        for (int rank = 14; rank >= 2; rank--)
        {
            int count = rankCounts[rank];
            if (count == 4)
            {
                fourRank = rank;
            }
            else if (count == 3)
            {
                if (tripRank == 0)
                {
                    tripRank = rank;
                }
                else
                {
                    secondTripRank = rank;
                }
            }
            else if (count == 2)
            {
                if (firstPairRank == 0)
                {
                    firstPairRank = rank;
                }
                else
                {
                    secondPairRank = rank;
                }
            }
        }

        if (fourRank > 0)
        {
            return new PokerHandResult(
                PokerHandCategory.FourOfAKind,
                new[] { fourRank, HighestRankExcept(rankCounts, fourRank) },
                cards);
        }

        if (tripRank > 0 && (firstPairRank > 0 || secondTripRank > 0))
        {
            return new PokerHandResult(
                PokerHandCategory.FullHouse,
                new[] { tripRank, secondTripRank > 0 ? secondTripRank : firstPairRank },
                cards);
        }

        if (isFlush)
        {
            return new PokerHandResult(PokerHandCategory.Flush, ranks, cards);
        }

        if (straightHigh > 0)
        {
            return new PokerHandResult(PokerHandCategory.Straight, new[] { straightHigh }, cards);
        }

        if (tripRank > 0)
        {
            int[] tiebreakers = new int[3];
            tiebreakers[0] = tripRank;
            FillKickers(rankCounts, tiebreakers, 1, tripRank, 0, 2);
            return new PokerHandResult(PokerHandCategory.ThreeOfAKind, tiebreakers, cards);
        }

        if (firstPairRank > 0 && secondPairRank > 0)
        {
            return new PokerHandResult(
                PokerHandCategory.TwoPair,
                new[] { firstPairRank, secondPairRank, HighestRankExcept(rankCounts, firstPairRank, secondPairRank) },
                cards);
        }

        if (firstPairRank > 0)
        {
            int[] tiebreakers = new int[4];
            tiebreakers[0] = firstPairRank;
            FillKickers(rankCounts, tiebreakers, 1, firstPairRank, 0, 3);
            return new PokerHandResult(PokerHandCategory.OnePair, tiebreakers, cards);
        }

        return new PokerHandResult(PokerHandCategory.HighCard, ranks, cards);
    }

    private static void SortDescending(int[] values, int count)
    {
        for (int i = 1; i < count; i++)
        {
            int value = values[i];
            int j = i - 1;
            while (j >= 0 && values[j] < value)
            {
                values[j + 1] = values[j];
                j--;
            }

            values[j + 1] = value;
        }
    }

    private static int GetStraightHigh(bool[] rankPresent)
    {
        for (int high = 14; high >= 5; high--)
        {
            if (rankPresent[high]
                && rankPresent[high - 1]
                && rankPresent[high - 2]
                && rankPresent[high - 3]
                && rankPresent[high - 4])
            {
                return high;
            }
        }

        return rankPresent[14]
            && rankPresent[5]
            && rankPresent[4]
            && rankPresent[3]
            && rankPresent[2]
            ? 5
            : 0;
    }

    private static int HighestRankExcept(int[] rankCounts, int excludedA, int excludedB = 0)
    {
        for (int rank = 14; rank >= 2; rank--)
        {
            if (rank != excludedA && rank != excludedB && rankCounts[rank] > 0)
            {
                return rank;
            }
        }

        return 0;
    }

    private static void FillKickers(
        int[] rankCounts,
        int[] destination,
        int destinationIndex,
        int excludedA,
        int excludedB,
        int maxKickers)
    {
        int filled = 0;
        for (int rank = 14; rank >= 2 && filled < maxKickers; rank--)
        {
            if (rank == excludedA || rank == excludedB || rankCounts[rank] == 0)
            {
                continue;
            }

            destination[destinationIndex + filled] = rank;
            filled++;
        }
    }
}
