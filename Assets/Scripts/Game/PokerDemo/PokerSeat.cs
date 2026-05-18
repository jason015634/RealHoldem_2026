// 좌석이 비어 있는지, 사람인지, 봇인지 UI에서 구분하기 위한 상태값입니다.
public enum PokerSeatOccupancy
{
    Empty,
    Human,
    Bot
}

// 테이블의 한 좌석입니다. 플레이어를 앉히거나 내보내며 PlayerState의 SeatIndex를 함께 동기화합니다.
public sealed class PokerSeat
{
    public int SeatIndex { get; }
    public PlayerState Player { get; private set; }
    public bool IsEmpty => Player == null;
    public bool HasHuman => Player != null && Player.IsHuman;
    public bool HasBot => Player != null && Player.IsBot;
    public PokerSeatOccupancy Occupancy => IsEmpty
        ? PokerSeatOccupancy.Empty
        : HasHuman ? PokerSeatOccupancy.Human : PokerSeatOccupancy.Bot;

    public PokerSeat(int seatIndex)
    {
        SeatIndex = seatIndex;
    }

    public void Sit(PlayerState player)
    {
        Player = player;
        Player?.AssignSeat(SeatIndex);
    }

    public PlayerState Leave()
    {
        PlayerState leaving = Player;
        Player = null;
        leaving?.AssignSeat(-1);
        return leaving;
    }
}
