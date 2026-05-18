using System;

[Serializable]
public struct LobbyRoomFilterState
{
    public string SearchText;
    public LobbyRoomSortType SortType;
    public bool OpenSeatsOnly;
    public bool HidePrivateRooms;
    public string Region;
    public string GameMode;

    public static LobbyRoomFilterState Default => new LobbyRoomFilterState
    {
        SearchText = string.Empty,
        SortType = LobbyRoomSortType.RoomIdAscending,
        OpenSeatsOnly = false,
        HidePrivateRooms = false,
        Region = string.Empty,
        GameMode = string.Empty
    };
}
