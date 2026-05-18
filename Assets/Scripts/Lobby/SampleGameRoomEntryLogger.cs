using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class SampleGameRoomEntryLogger : MonoBehaviour
{
    [SerializeField] private string lobbySceneName = "SampleLobbyScene";
    [SerializeField] private bool createBackButtonOnStart = true;

    private void Start()
    {
        LogSelectedRoom();

        if (createBackButtonOnStart)
        {
            CreateBackButtonIfNeeded();
        }
    }

    private void LogSelectedRoom()
    {
        RoomData roomData = LobbyRoomSelection.SelectedRoom;

        if (roomData == null)
        {
            Debug.LogWarning($"[{nameof(SampleGameRoomEntryLogger)}] Entered SampleGameScene without selected RoomData.");
            return;
        }

        Debug.Log($"Entered Room: {roomData.RoomId} / Blind {roomData.SmallBlind}-{roomData.BigBlind} / MaxBuyIn {roomData.MaxBuyIn}");
    }

    private void CreateBackButtonIfNeeded()
    {
        if (GameObject.Find("LobbyBackButton") != null)
        {
            return;
        }

        Canvas canvas = FindObjectOfType<Canvas>(true);
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("SampleGameRuntimeCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        GameObject buttonObject = new GameObject("LobbyBackButton");
        buttonObject.transform.SetParent(canvas.transform, false);

        RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(24f, -24f);
        rectTransform.sizeDelta = new Vector2(160f, 56f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.72f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(ReturnToLobby);

        GameObject labelObject = new GameObject("Label");
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRectTransform = labelObject.AddComponent<RectTransform>();
        labelRectTransform.anchorMin = Vector2.zero;
        labelRectTransform.anchorMax = Vector2.one;
        labelRectTransform.offsetMin = Vector2.zero;
        labelRectTransform.offsetMax = Vector2.zero;

        TMP_Text label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "Lobby";
        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
    }

    private void ReturnToLobby()
    {
        if (!Application.CanStreamedLevelBeLoaded(lobbySceneName))
        {
            Debug.LogWarning($"[{nameof(SampleGameRoomEntryLogger)}] Scene '{lobbySceneName}' is not available in Build Settings.");
            return;
        }

        SceneManager.LoadScene(lobbySceneName);
    }
}
