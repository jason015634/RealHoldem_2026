using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed class LobbyRoomListController : MonoBehaviour
{
    private const int DummyRoomCount = 20;
    private const string DefaultRoomSlotResourcesPath = "Prefabs/RoomSlot";

    [Header("Scene References")]
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private GameObject roomSlotPrefab;

    [Header("Runtime")]
    [SerializeField] private string roomSlotResourcesPath = DefaultRoomSlotResourcesPath;
    [SerializeField] private bool populateDummyRoomsOnStart = true;

    private readonly List<RoomData> allRooms = new List<RoomData>();
    private readonly List<RoomData> visibleRooms = new List<RoomData>();
    private readonly List<RoomSlotView> spawnedSlots = new List<RoomSlotView>();
    private LobbyRoomFilterState currentFilter = LobbyRoomFilterState.Default;
    private bool hasPopulatedInitialRooms;

    public event Action<int, int> RoomCountChanged;

    public IReadOnlyList<RoomData> AllRooms => allRooms;
    public IReadOnlyList<RoomData> VisibleRooms => visibleRooms;
    public LobbyRoomFilterState CurrentFilter => currentFilter;

    private void Awake()
    {
        ResolveReferences();
        ConfigureContentLayout();
    }

    private void OnEnable()
    {
        PopulateInitialRooms();
    }

    private void Start()
    {
        PopulateInitialRooms();
    }

    public void SetRooms(IReadOnlyList<RoomData> rooms)
    {
        hasPopulatedInitialRooms = true;
        allRooms.Clear();

        if (rooms == null)
        {
            Debug.LogWarning($"[{nameof(LobbyRoomListController)}] Room list is null.", this);
        }
        else
        {
            allRooms.AddRange(rooms.Where(room => room != null));
        }

        RebuildFilteredRooms();
    }

    public void ApplyFilter(LobbyRoomFilterState filterState)
    {
        currentFilter = filterState;
        RebuildFilteredRooms();
    }

    public List<string> GetAvailableRegions()
    {
        return GetDistinctValues(room => room.Region);
    }

    public List<string> GetAvailableGameModes()
    {
        return GetDistinctValues(room => room.GameMode);
    }

    public void ClearRooms()
    {
        for (int i = spawnedSlots.Count - 1; i >= 0; i--)
        {
            if (spawnedSlots[i] != null)
            {
                DestroySlotObject(spawnedSlots[i].gameObject);
            }
        }

        spawnedSlots.Clear();
        visibleRooms.Clear();
        RoomCountChanged?.Invoke(0, allRooms.Count);
    }

    public static List<RoomData> CreateDummyRooms()
    {
        long[] maxBuyIns =
        {
            10000L, 30000L, 50000L, 100000L, 300000L, 500000L, 1000000L
        };

        int[] smallBlinds =
        {
            50, 100, 200, 500, 1000, 2500, 5000
        };

        string[] roomPrefixes =
        {
            "Seoul Rush", "Tokyo Deep", "Vegas Main", "Marina Quick", "River Prime",
            "Diamond Table", "Night Stack", "Ace Harbor", "Royal Orbit", "Turbo Seat"
        };

        string[] regions =
        {
            "KR", "JP", "US", "SG"
        };

        string[] gameModes =
        {
            "NL Hold'em", "Short Deck", "Tournament"
        };

        List<RoomData> rooms = new List<RoomData>(DummyRoomCount);

        for (int i = 0; i < DummyRoomCount; i++)
        {
            int stakeIndex = i % maxBuyIns.Length;
            int smallBlind = smallBlinds[stakeIndex];
            int bigBlind = smallBlind * 2;
            long maxBuyIn = maxBuyIns[stakeIndex];
            bool isFull = i == 5 || i == 12 || i == 18;

            rooms.Add(new RoomData
            {
                RoomId = 1000 + i,
                RoomName = $"{roomPrefixes[i % roomPrefixes.Length]} {i + 1:00}",
                SmallBlind = smallBlind,
                BigBlind = bigBlind,
                MinBuyIn = Math.Max(bigBlind * 20L, maxBuyIn / 10L),
                MaxBuyIn = maxBuyIn,
                CurrentPlayerCount = isFull ? 6 : ((i * 3) % 5) + 1,
                MaxPlayerCount = 6,
                IsPrivate = i % 7 == 3,
                IsInProgress = i % 6 == 4,
                GameMode = gameModes[i % gameModes.Length],
                Region = regions[i % regions.Length]
            });
        }

        return rooms;
    }

    private void RebuildFilteredRooms()
    {
        ResolveReferences();
        ConfigureContentLayout();
        ClearSpawnedSlots();

        IEnumerable<RoomData> query = allRooms;
        query = ApplySearch(query, currentFilter.SearchText);

        if (currentFilter.OpenSeatsOnly)
        {
            query = query.Where(room => room.CurrentPlayerCount < room.MaxPlayerCount);
        }

        if (currentFilter.HidePrivateRooms)
        {
            query = query.Where(room => !room.IsPrivate);
        }

        if (!string.IsNullOrWhiteSpace(currentFilter.Region))
        {
            query = query.Where(room => string.Equals(room.Region, currentFilter.Region, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(currentFilter.GameMode))
        {
            query = query.Where(room => string.Equals(room.GameMode, currentFilter.GameMode, StringComparison.OrdinalIgnoreCase));
        }

        query = ApplySort(query, currentFilter.SortType);

        visibleRooms.Clear();
        visibleRooms.AddRange(query);
        SpawnVisibleRooms();
        RoomCountChanged?.Invoke(visibleRooms.Count, allRooms.Count);
    }

    private void ClearSpawnedSlots()
    {
        for (int i = spawnedSlots.Count - 1; i >= 0; i--)
        {
            if (spawnedSlots[i] != null)
            {
                DestroySlotObject(spawnedSlots[i].gameObject);
            }
        }

        spawnedSlots.Clear();
    }

    private void SpawnVisibleRooms()
    {
        if (contentRoot == null)
        {
            Debug.LogWarning($"[{nameof(LobbyRoomListController)}] Content root is missing; room slots cannot be created.", this);
            return;
        }

        if (roomSlotPrefab == null)
        {
            Debug.LogWarning($"[{nameof(LobbyRoomListController)}] RoomSlot prefab is missing. Expected Resources/{roomSlotResourcesPath}.prefab.", this);
            return;
        }

        for (int i = 0; i < visibleRooms.Count; i++)
        {
            RoomData room = visibleRooms[i];
            GameObject slotObject = Instantiate(roomSlotPrefab, contentRoot);
            slotObject.name = $"RoomSlot_{room.RoomId}";

            RoomSlotView slotView = slotObject.GetComponent<RoomSlotView>();
            if (slotView == null)
            {
                slotView = slotObject.AddComponent<RoomSlotView>();
            }

            slotView.Initialize(room);
            spawnedSlots.Add(slotView);
        }
    }

    private void PopulateInitialRooms()
    {
        if (!Application.isPlaying || !populateDummyRoomsOnStart || hasPopulatedInitialRooms)
        {
            return;
        }

        SetRooms(CreateDummyRooms());
    }

    private List<string> GetDistinctValues(Func<RoomData, string> selector)
    {
        return allRooms
            .Select(selector)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct()
            .OrderBy(value => value)
            .ToList();
    }

    private static IEnumerable<RoomData> ApplySearch(IEnumerable<RoomData> rooms, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return rooms;
        }

        string normalizedSearch = searchText.Trim();
        return rooms.Where(room =>
            (!string.IsNullOrEmpty(room.RoomName) && room.RoomName.IndexOf(normalizedSearch, StringComparison.OrdinalIgnoreCase) >= 0)
            || room.RoomId.ToString().IndexOf(normalizedSearch, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static IEnumerable<RoomData> ApplySort(IEnumerable<RoomData> rooms, LobbyRoomSortType sortType)
    {
        switch (sortType)
        {
            case LobbyRoomSortType.BlindAscending:
                return rooms.OrderBy(room => room.SmallBlind).ThenBy(room => room.RoomId);
            case LobbyRoomSortType.BlindDescending:
                return rooms.OrderByDescending(room => room.SmallBlind).ThenBy(room => room.RoomId);
            case LobbyRoomSortType.MaxBuyInAscending:
                return rooms.OrderBy(room => room.MaxBuyIn).ThenBy(room => room.RoomId);
            case LobbyRoomSortType.MaxBuyInDescending:
                return rooms.OrderByDescending(room => room.MaxBuyIn).ThenBy(room => room.RoomId);
            case LobbyRoomSortType.PlayersDescending:
                return rooms.OrderByDescending(room => room.CurrentPlayerCount).ThenBy(room => room.RoomId);
            case LobbyRoomSortType.RoomIdAscending:
            default:
                return rooms.OrderBy(room => room.RoomId);
        }
    }

    private static void DestroySlotObject(GameObject slotObject)
    {
        if (Application.isPlaying)
        {
            Destroy(slotObject);
        }
        else
        {
            DestroyImmediate(slotObject);
        }
    }

    private void ResolveReferences()
    {
        if (contentRoot == null)
        {
            ScrollRect scrollRect = FindObjectOfType<ScrollRect>(true);
            if (scrollRect != null)
            {
                contentRoot = scrollRect.content;
            }
        }

        if (roomSlotPrefab == null)
        {
            roomSlotPrefab = Resources.Load<GameObject>(roomSlotResourcesPath);
        }
    }

    private void ConfigureContentLayout()
    {
        if (contentRoot == null)
        {
            return;
        }

        VerticalLayoutGroup layoutGroup = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (layoutGroup == null)
        {
            layoutGroup = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.padding.top = 20;
            layoutGroup.spacing = 20;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
        }

        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        }

        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
}
