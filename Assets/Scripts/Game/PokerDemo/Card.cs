using System;

// 카드의 문양입니다. 리소스 경로와 UI 약어를 만들 때도 이 값을 사용합니다.
public enum Suit
{
    Spade,
    Heart,
    Diamond,
    Club
}

// 카드의 숫자/랭크입니다. 포커 족보 비교를 쉽게 하려고 실제 랭크 값을 정수로 맞춰 둡니다.
public enum Rank
{
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13,
    Ace = 14
}

// 포커 게임 전체에서 공유하는 카드 값 객체입니다.
// 카드 스프라이트 경로, 화면 표시용 짧은 이름, 족보 계산용 랭크 값을 제공합니다.
[Serializable]
public readonly struct Card
{
    public readonly Suit Suit;
    public readonly Rank Rank;

    public Card(Suit suit, Rank rank)
    {
        Suit = suit;
        Rank = rank;
    }

    public int RankValue => (int)Rank;

    public string ResourcePath => $"Sprites/Cards/{ResourceSuitName}_{ResourceRankName}";

    public override string ToString()
    {
        return $"{Suit} {Rank}";
    }

    public string ShortName()
    {
        return $"{RankLabel()}{SuitLabel()}";
    }

    private string ResourceSuitName => Suit.ToString();

    private string ResourceRankName
    {
        get
        {
            switch (Rank)
            {
                case Rank.Jack:
                    return "Jack";
                case Rank.Queen:
                    return "Queen";
                case Rank.King:
                    return "King";
                case Rank.Ace:
                    return "Ace";
                default:
                    return ((int)Rank).ToString();
            }
        }
    }

    private string RankLabel()
    {
        switch (Rank)
        {
            case Rank.Ten:
                return "T";
            case Rank.Jack:
                return "J";
            case Rank.Queen:
                return "Q";
            case Rank.King:
                return "K";
            case Rank.Ace:
                return "A";
            default:
                return ((int)Rank).ToString();
        }
    }

    private string SuitLabel()
    {
        switch (Suit)
        {
            case Suit.Spade:
                return "S";
            case Suit.Heart:
                return "H";
            case Suit.Diamond:
                return "D";
            case Suit.Club:
                return "C";
            default:
                return "?";
        }
    }
}
