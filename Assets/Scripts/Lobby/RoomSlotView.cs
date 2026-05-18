using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class RoomSlotView : MonoBehaviour
{
    [Header("Text Fields")]
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text roomIdText;
    [SerializeField] private TMP_Text blindText;
    [SerializeField] private TMP_Text buyInText;
    [SerializeField] private TMP_Text playersText;
    [SerializeField] private TMP_Text modeRegionText;
    [SerializeField] private TMP_Text privacyText;
    [SerializeField] private TMP_Text statusText;

    [Header("Interaction")]
    [SerializeField] private Button button;
    [SerializeField] private string gameSceneName = "SampleGameScene";

    private Action clickHandler;

    public RoomData RoomData { get; private set; }

    private void Awake()
    {
        ResolveReferences();
        EnsureButton();
    }

    private void OnDestroy()
    {
        if (button != null && clickHandler != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    public void Initialize(RoomData roomData)
    {
        RoomData = roomData;
        ResolveReferences();
        EnsureButton();
        Refresh();
        BindClick();
    }

    public void Refresh()
    {
        if (RoomData == null)
        {
            Debug.LogWarning($"[{nameof(RoomSlotView)}] RoomData is null on {name}.", this);
            return;
        }

        if (!HasAnyTextField())
        {
            Debug.LogWarning($"[{nameof(RoomSlotView)}] No TMP_Text fields found on {name}.", this);
            return;
        }

        SetText(roomNameText, string.IsNullOrEmpty(RoomData.RoomName) ? $"Room {RoomData.RoomId}" : RoomData.RoomName);
        SetText(roomIdText, $"Room ID: {RoomData.RoomId}");
        SetText(blindText, $"Blind: {FormatNumber(RoomData.SmallBlind)} / {FormatNumber(RoomData.BigBlind)}");
        SetText(buyInText, $"Buy-In: {FormatNumber(RoomData.MinBuyIn)} - {FormatNumber(RoomData.MaxBuyIn)}");
        SetText(playersText, $"Players: {RoomData.CurrentPlayerCount} / {RoomData.MaxPlayerCount}");
        SetText(modeRegionText, $"{SafeText(RoomData.GameMode)} / {SafeText(RoomData.Region)}");
        SetText(privacyText, RoomData.IsPrivate ? "Private" : "Public");
        SetText(statusText, GetStatusLabel(RoomData));
    }

    private void BindClick()
    {
        if (button == null)
        {
            Debug.LogWarning($"[{nameof(RoomSlotView)}] Button is missing on {name}; this slot cannot be clicked.", this);
            return;
        }

        if (clickHandler != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }

        clickHandler = HandleClick;
        button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        if (RoomData == null)
        {
            Debug.LogWarning($"[{nameof(RoomSlotView)}] Cannot enter because RoomData is null on {name}.", this);
            return;
        }

        LobbyRoomSelection.SetSelectedRoom(RoomData);
        SceneManager.LoadScene(gameSceneName);
    }

    private void ResolveReferences()
    {
        if (roomNameText == null) roomNameText = FindTextByName("RoomName_Value", false);
        if (roomIdText == null) roomIdText = FindTextByName("RoomId_Value", false);
        if (roomIdText == null) roomIdText = FindTextByName("RoomNumber_Value", false);
        if (blindText == null) blindText = FindTextByName("Blind_Value", false);
        if (blindText == null) blindText = FindTextByName("Big/Small Blind_Value", false);
        if (buyInText == null) buyInText = FindTextByName("BuyIn_Value", false);
        if (buyInText == null) buyInText = FindTextByName("MaxBetMoney_Value", false);
        if (playersText == null) playersText = FindTextByName("Players_Value", false);
        if (modeRegionText == null) modeRegionText = FindTextByName("ModeRegion_Value", false);
        if (privacyText == null) privacyText = FindTextByName("Privacy_Value", false);
        if (statusText == null) statusText = FindTextByName("Status_Value", false);

        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }

    private void EnsureButton()
    {
        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
            Graphic graphic = GetComponent<Graphic>();

            if (graphic != null)
            {
                button.targetGraphic = graphic;
            }
            else
            {
                Debug.LogWarning($"[{nameof(RoomSlotView)}] Added a Button to {name}, but no Graphic target was found.", this);
            }
        }
    }

    private bool HasAnyTextField()
    {
        return roomNameText != null
            || roomIdText != null
            || blindText != null
            || buyInText != null
            || playersText != null
            || modeRegionText != null
            || privacyText != null
            || statusText != null;
    }

    private TMP_Text FindTextByName(string targetName, bool warnIfMissing = true)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == targetName)
            {
                return texts[i];
            }
        }

        if (warnIfMissing)
        {
            Debug.LogWarning($"[{nameof(RoomSlotView)}] TMP_Text named '{targetName}' was not found under {name}.", this);
        }

        return null;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static string GetStatusLabel(RoomData roomData)
    {
        if (roomData.IsPrivate)
        {
            return "Private";
        }

        if (roomData.CurrentPlayerCount >= roomData.MaxPlayerCount)
        {
            return "Full";
        }

        if (roomData.IsInProgress)
        {
            return "In Progress";
        }

        return "Available";
    }

    private static string SafeText(string value)
    {
        return string.IsNullOrEmpty(value) ? "-" : value;
    }

    private static string FormatNumber(long value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
