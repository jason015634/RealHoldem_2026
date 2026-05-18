public static class LobbyRoomSelection
{
    public static RoomData SelectedRoom { get; private set; }

    public static bool HasSelection => SelectedRoom != null;

    public static void SetSelectedRoom(RoomData roomData)
    {
        SelectedRoom = roomData;
    }

    public static void Clear()
    {
        SelectedRoom = null;
    }
}
