using System;
using System.Collections.Generic;

// 52장 표준 덱을 생성하고 섞고 뽑는 책임만 가진 단순한 덱 모델입니다.
public sealed class Deck
{
    private readonly List<Card> cards = new List<Card>(52);
    private readonly Random random = new Random();

    public int Count => cards.Count;

    public Deck()
    {
        Reset();
    }

    public void Reset()
    {
        cards.Clear();

        foreach (Suit suit in Enum.GetValues(typeof(Suit)))
        {
            for (int rank = (int)Rank.Two; rank <= (int)Rank.Ace; rank++)
            {
                cards.Add(new Card(suit, (Rank)rank));
            }
        }
    }

    public void Shuffle()
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            Card temp = cards[i];
            cards[i] = cards[j];
            cards[j] = temp;
        }
    }

    public Card Draw()
    {
        if (cards.Count == 0)
        {
            throw new InvalidOperationException("Cannot draw from an empty deck.");
        }

        int lastIndex = cards.Count - 1;
        Card card = cards[lastIndex];
        cards.RemoveAt(lastIndex);
        return card;
    }

    public List<Card> Draw(int count)
    {
        List<Card> drawnCards = new List<Card>(count);
        for (int i = 0; i < count; i++)
        {
            drawnCards.Add(Draw());
        }

        return drawnCards;
    }
}
