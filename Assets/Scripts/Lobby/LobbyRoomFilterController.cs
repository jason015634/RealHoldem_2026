using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LobbyRoomFilterController : MonoBehaviour
{
    private const string AllOption = "All";

    [Header("Controllers")]
    [SerializeField] private LobbyRoomListController roomListController;

    [Header("Filter UI")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private TMP_Dropdown sortDropdown;
    [SerializeField] private TMP_Dropdown regionDropdown;
    [SerializeField] private TMP_Dropdown gameModeDropdown;
    [SerializeField] private Toggle openSeatsOnlyToggle;
    [SerializeField] private Toggle hidePrivateRoomsToggle;
    [SerializeField] private TMP_Text roomCountText;

    private readonly List<string> regionOptions = new List<string>();
    private readonly List<string> gameModeOptions = new List<string>();
    private bool isRefreshingOptions;

    private void Awake()
    {
        ResolveReferences();
        ConfigureStaticOptions();
        BindEvents();
    }

    private void Start()
    {
        ResolveReferences();
        RefreshDynamicOptions();
        ApplyCurrentFilter();
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    public void ApplyCurrentFilter()
    {
        if (roomListController == null)
        {
            Debug.LogWarning($"[{nameof(LobbyRoomFilterController)}] Room list controller is missing.", this);
            return;
        }

        roomListController.ApplyFilter(ReadFilterState());
    }

    private LobbyRoomFilterState ReadFilterState()
    {
        return new LobbyRoomFilterState
        {
            SearchText = searchInput != null ? searchInput.text : string.Empty,
            SortType = ReadSortType(),
            OpenSeatsOnly = openSeatsOnlyToggle != null && openSeatsOnlyToggle.isOn,
            HidePrivateRooms = hidePrivateRoomsToggle != null && hidePrivateRoomsToggle.isOn,
            Region = ReadOptionValue(regionDropdown, regionOptions),
            GameMode = ReadOptionValue(gameModeDropdown, gameModeOptions)
        };
    }

    private LobbyRoomSortType ReadSortType()
    {
        if (sortDropdown == null)
        {
            return LobbyRoomSortType.RoomIdAscending;
        }

        switch (sortDropdown.value)
        {
            case 1:
                return LobbyRoomSortType.BlindAscending;
            case 2:
                return LobbyRoomSortType.BlindDescending;
            case 3:
                return LobbyRoomSortType.MaxBuyInAscending;
            case 4:
                return LobbyRoomSortType.MaxBuyInDescending;
            case 5:
                return LobbyRoomSortType.PlayersDescending;
            case 0:
            default:
                return LobbyRoomSortType.RoomIdAscending;
        }
    }

    private void HandleFilterChanged(string _)
    {
        ApplyCurrentFilter();
    }

    private void HandleFilterChanged(int _)
    {
        if (!isRefreshingOptions)
        {
            ApplyCurrentFilter();
        }
    }

    private void HandleFilterChanged(bool _)
    {
        ApplyCurrentFilter();
    }

    private void HandleRoomCountChanged(int visibleCount, int totalCount)
    {
        RefreshDynamicOptions();
        UpdateRoomCount(visibleCount, totalCount);
    }

    private void UpdateRoomCount(int visibleCount, int totalCount)
    {
        if (roomCountText != null)
        {
            roomCountText.text = $"Rooms: {visibleCount} / {totalCount}";
        }
    }

    private void ConfigureStaticOptions()
    {
        if (sortDropdown == null)
        {
            Debug.LogWarning($"[{nameof(LobbyRoomFilterController)}] Sort dropdown is missing.", this);
            return;
        }

        sortDropdown.ClearOptions();
        sortDropdown.AddOptions(new List<string>
        {
            "Room ID",
            "Blind Low",
            "Blind High",
            "Max Buy-In Low",
            "Max Buy-In High",
            "Players High"
        });
        sortDropdown.SetValueWithoutNotify(0);
    }

    private void RefreshDynamicOptions()
    {
        if (roomListController == null)
        {
            return;
        }

        isRefreshingOptions = true;
        RefreshDropdown(regionDropdown, regionOptions, roomListController.GetAvailableRegions());
        RefreshDropdown(gameModeDropdown, gameModeOptions, roomListController.GetAvailableGameModes());
        isRefreshingOptions = false;
    }

    private void RefreshDropdown(TMP_Dropdown dropdown, List<string> cache, List<string> values)
    {
        if (dropdown == null)
        {
            return;
        }

        string previousValue = ReadOptionValue(dropdown, cache);
        cache.Clear();
        cache.AddRange(values);

        List<string> labels = new List<string>(cache.Count + 1) { AllOption };
        labels.AddRange(cache);

        dropdown.ClearOptions();
        dropdown.AddOptions(labels);

        int selectedIndex = 0;
        if (!string.IsNullOrEmpty(previousValue))
        {
            int valueIndex = cache.IndexOf(previousValue);
            if (valueIndex >= 0)
            {
                selectedIndex = valueIndex + 1;
            }
        }

        dropdown.SetValueWithoutNotify(selectedIndex);
    }

    private static string ReadOptionValue(TMP_Dropdown dropdown, List<string> cache)
    {
        if (dropdown == null || dropdown.value <= 0)
        {
            return string.Empty;
        }

        int cacheIndex = dropdown.value - 1;
        if (cacheIndex < 0 || cacheIndex >= cache.Count)
        {
            return string.Empty;
        }

        return cache[cacheIndex];
    }

    private void ResolveReferences()
    {
        if (roomListController == null)
        {
            roomListController = FindObjectOfType<LobbyRoomListController>(true);
        }

        if (searchInput == null) searchInput = FindObjectByName<TMP_InputField>("LobbySearchInput");
        if (sortDropdown == null) sortDropdown = FindObjectByName<TMP_Dropdown>("LobbySortDropdown");
        if (regionDropdown == null) regionDropdown = FindObjectByName<TMP_Dropdown>("LobbyRegionDropdown");
        if (gameModeDropdown == null) gameModeDropdown = FindObjectByName<TMP_Dropdown>("LobbyGameModeDropdown");
        if (openSeatsOnlyToggle == null) openSeatsOnlyToggle = FindObjectByName<Toggle>("LobbyOpenSeatsOnlyToggle");
        if (hidePrivateRoomsToggle == null) hidePrivateRoomsToggle = FindObjectByName<Toggle>("LobbyHidePrivateRoomsToggle");
        if (roomCountText == null) roomCountText = FindTextByName("LobbyRoomCountText");
    }

    private void BindEvents()
    {
        if (roomListController != null)
        {
            roomListController.RoomCountChanged -= HandleRoomCountChanged;
            roomListController.RoomCountChanged += HandleRoomCountChanged;
        }

        if (searchInput != null)
        {
            searchInput.onValueChanged.RemoveListener(HandleFilterChanged);
            searchInput.onValueChanged.AddListener(HandleFilterChanged);
        }

        if (sortDropdown != null)
        {
            sortDropdown.onValueChanged.RemoveListener(HandleFilterChanged);
            sortDropdown.onValueChanged.AddListener(HandleFilterChanged);
        }

        if (regionDropdown != null)
        {
            regionDropdown.onValueChanged.RemoveListener(HandleFilterChanged);
            regionDropdown.onValueChanged.AddListener(HandleFilterChanged);
        }

        if (gameModeDropdown != null)
        {
            gameModeDropdown.onValueChanged.RemoveListener(HandleFilterChanged);
            gameModeDropdown.onValueChanged.AddListener(HandleFilterChanged);
        }

        if (openSeatsOnlyToggle != null)
        {
            openSeatsOnlyToggle.onValueChanged.RemoveListener(HandleFilterChanged);
            openSeatsOnlyToggle.onValueChanged.AddListener(HandleFilterChanged);
        }

        if (hidePrivateRoomsToggle != null)
        {
            hidePrivateRoomsToggle.onValueChanged.RemoveListener(HandleFilterChanged);
            hidePrivateRoomsToggle.onValueChanged.AddListener(HandleFilterChanged);
        }
    }

    private void UnbindEvents()
    {
        if (roomListController != null)
        {
            roomListController.RoomCountChanged -= HandleRoomCountChanged;
        }
    }

    private static T FindObjectByName<T>(string objectName) where T : Component
    {
        T[] components = FindObjectsOfType<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i].name == objectName)
            {
                return components[i];
            }
        }

        return null;
    }

    private static TMP_Text FindTextByName(string objectName)
    {
        TMP_Text[] texts = FindObjectsOfType<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == objectName)
            {
                return texts[i];
            }
        }

        return null;
    }
}
