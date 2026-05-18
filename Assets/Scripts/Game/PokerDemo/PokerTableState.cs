using System.Collections.Generic;

// 6인 테이블의 좌석 목록을 관리합니다.
// 빈 좌석 찾기, 봇 제거, 착석 플레이어 순회 같은 테이블 단위 작업을 담당합니다.
public sealed class PokerTableState
{
    private readonly List<PokerSeat> seats;
    private readonly List<int> emptySeatIndices = new List<int>();

    public IReadOnlyList<PokerSeat> Seats => seats;
    public int MaxSeats => seats.Count;
    public int OccupiedCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < seats.Count; i++)
            {
                if (!seats[i].IsEmpty)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int EmptySeatCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].IsEmpty)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int BotCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < seats.Count; i++)
            {
                if (seats[i].HasBot)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public PokerTableState(int maxSeats)
    {
        seats = new List<PokerSeat>(maxSeats);
        for (int i = 0; i < maxSeats; i++)
        {
            seats.Add(new PokerSeat(i));
        }
    }

    public bool TrySitFirstEmpty(PlayerState player, out PokerSeat seat)
    {
        for (int i = 0; i < seats.Count; i++)
        {
            if (!seats[i].IsEmpty)
            {
                continue;
            }

            seat = seats[i];
            seat.Sit(player);
            return true;
        }

        seat = null;
        return false;
    }

    public bool TryGetLastBotSeat(out PokerSeat seat)
    {
        for (int i = seats.Count - 1; i >= 0; i--)
        {
            if (seats[i].HasBot)
            {
                seat = seats[i];
                return true;
            }
        }

        seat = null;
        return false;
    }

    public bool TrySitAtSeat(int seatIndex, PlayerState player, out PokerSeat seat)
    {
        seat = GetSeat(seatIndex);
        if (seat == null || !seat.IsEmpty)
        {
            return false;
        }

        seat.Sit(player);
        return true;
    }

    public PokerSeat GetSeat(int seatIndex)
    {
        return seatIndex >= 0 && seatIndex < seats.Count ? seats[seatIndex] : null;
    }

    public IEnumerable<PlayerState> OccupiedPlayers()
    {
        for (int i = 0; i < seats.Count; i++)
        {
            if (!seats[i].IsEmpty)
            {
                yield return seats[i].Player;
            }
        }
    }

    public IEnumerable<PlayerState> OccupiedPlayersInSeatOrder()
    {
        for (int i = 0; i < seats.Count; i++)
        {
            if (!seats[i].IsEmpty)
            {
                yield return seats[i].Player;
            }
        }
    }

    public IReadOnlyList<int> EmptySeatIndicesInSeatOrder()
    {
        emptySeatIndices.Clear();
        for (int i = 0; i < seats.Count; i++)
        {
            if (seats[i].IsEmpty)
            {
                emptySeatIndices.Add(seats[i].SeatIndex);
            }
        }

        return emptySeatIndices;
    }

    public PlayerState RemoveLastBot()
    {
        for (int i = seats.Count - 1; i >= 0; i--)
        {
            if (seats[i].HasBot)
            {
                return seats[i].Leave();
            }
        }

        return null;
    }

    public PlayerState RemoveBotAtSeat(int seatIndex)
    {
        PokerSeat seat = GetSeat(seatIndex);
        return seat != null && seat.HasBot ? seat.Leave() : null;
    }
}
