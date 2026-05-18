using System;

[Serializable]
public class RoomData
{
    public int RoomId;
    public string RoomName;
    public int SmallBlind;
    public int BigBlind;
    public long MinBuyIn;
    public long MaxBuyIn;
    public int CurrentPlayerCount;
    public int MaxPlayerCount;
    public bool IsPrivate;
    public bool IsInProgress;
    public string GameMode;
    public string Region;
}
