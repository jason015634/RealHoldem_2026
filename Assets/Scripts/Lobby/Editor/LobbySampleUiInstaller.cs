using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class LobbySampleUiInstaller
{
    private const string LobbyScenePath = "Assets/Scenes/Game/SampleLobbyScene.unity";
    private const string RoomSlotPrefabPath = "Assets/Resources/Prefabs/RoomSlot.prefab";
    private const string ControllerRootName = "LobbyRuntimeControllers";
    private const string FilterPanelName = "LobbyFilterPanel";
    private const string AutoInstallSessionKey = "RealHoldem_2026.LobbySampleUiInstaller.AutoInstalled";

    static LobbySampleUiInstaller()
    {
        EditorApplication.delayCall += AutoInstallIfNeeded;
    }

    [MenuItem("Tools/Lobby Sample/Install Lobby UI")]
    public static void InstallLobbyUi()
    {
        InstallRoomSlotPrefab();
        InstallLobbyScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LobbySample] Installed lobby filter UI and room slot fields.");
    }

    private static void AutoInstallIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != LobbyScenePath)
        {
            return;
        }

        bool sceneHasFilterUi = GameObject.Find(FilterPanelName) != null
            && GameObject.Find(ControllerRootName) != null;
        bool prefabHasExpandedFields = PrefabHasChild(RoomSlotPrefabPath, "RoomName_Value")
            && PrefabHasChild(RoomSlotPrefabPath, "Status_Value");
        bool prefabHasLegacyFields = PrefabHasChild(RoomSlotPrefabPath, "RoomNumber_Value")
            || PrefabHasChild(RoomSlotPrefabPath, "MaxBetMoney_Value")
            || PrefabHasChild(RoomSlotPrefabPath, "Big/Small Blind_Value");

        if (sceneHasFilterUi && prefabHasExpandedFields && !prefabHasLegacyFields)
        {
            SessionState.SetBool(AutoInstallSessionKey, true);
            return;
        }

        InstallLobbyUi();
        SessionState.SetBool(AutoInstallSessionKey, true);
    }

    private static void InstallRoomSlotPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(RoomSlotPrefabPath);
        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(980f, 260f);

            LayoutElement layoutElement = root.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = root.AddComponent<LayoutElement>();
            }

            layoutElement.minHeight = 260f;
            layoutElement.preferredHeight = 260f;
            layoutElement.flexibleWidth = 1f;

            RenameChild(root.transform, "RoomNumber_Value", "RoomId_Value");
            RenameChild(root.transform, "MaxBetMoney_Value", "BuyIn_Value");
            RenameChild(root.transform, "Big/Small Blind_Value", "Blind_Value");

            TMP_Text roomName = EnsureSlotText(root.transform, "RoomName_Value", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -24f), new Vector2(-220f, -78f), 34f, TextAlignmentOptions.Left);
            TMP_Text roomId = EnsureSlotText(root.transform, "RoomId_Value", new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(32f, -82f), new Vector2(-16f, -128f), 24f, TextAlignmentOptions.Left);
            TMP_Text blind = EnsureSlotText(root.transform, "Blind_Value", new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(32f, -130f), new Vector2(-16f, -176f), 24f, TextAlignmentOptions.Left);
            TMP_Text buyIn = EnsureSlotText(root.transform, "BuyIn_Value", new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(32f, -178f), new Vector2(-16f, -224f), 24f, TextAlignmentOptions.Left);
            TMP_Text players = EnsureSlotText(root.transform, "Players_Value", new Vector2(0.5f, 1f), new Vector2(0.78f, 1f), new Vector2(20f, -82f), new Vector2(-12f, -128f), 24f, TextAlignmentOptions.Left);
            TMP_Text modeRegion = EnsureSlotText(root.transform, "ModeRegion_Value", new Vector2(0.5f, 1f), new Vector2(0.78f, 1f), new Vector2(20f, -130f), new Vector2(-12f, -176f), 24f, TextAlignmentOptions.Left);
            TMP_Text privacy = EnsureSlotText(root.transform, "Privacy_Value", new Vector2(0.5f, 1f), new Vector2(0.78f, 1f), new Vector2(20f, -178f), new Vector2(-12f, -224f), 24f, TextAlignmentOptions.Left);
            TMP_Text status = EnsureSlotText(root.transform, "Status_Value", new Vector2(0.78f, 1f), new Vector2(1f, 1f), new Vector2(10f, -92f), new Vector2(-32f, -166f), 28f, TextAlignmentOptions.Center);
            RemoveLegacySlotText(root.transform, "RoomNumber_Value");
            RemoveLegacySlotText(root.transform, "MaxBetMoney_Value");
            RemoveLegacySlotText(root.transform, "Big/Small Blind_Value");

            roomName.text = "Sample Room";
            roomId.text = "Room ID: 1000";
            blind.text = "Blind: 100 / 200";
            buyIn.text = "Buy-In: 10,000 - 100,000";
            players.text = "Players: 3 / 6";
            modeRegion.text = "NL Hold'em / KR";
            privacy.text = "Public";
            status.text = "Available";
            status.color = new Color(0.1f, 0.45f, 0.18f, 1f);

            RoomSlotView view = root.GetComponent<RoomSlotView>();
            if (view == null)
            {
                view = root.AddComponent<RoomSlotView>();
            }

            SerializedObject serializedView = new SerializedObject(view);
            SetReference(serializedView, "roomNameText", roomName);
            SetReference(serializedView, "roomIdText", roomId);
            SetReference(serializedView, "blindText", blind);
            SetReference(serializedView, "buyInText", buyIn);
            SetReference(serializedView, "playersText", players);
            SetReference(serializedView, "modeRegionText", modeRegion);
            SetReference(serializedView, "privacyText", privacy);
            SetReference(serializedView, "statusText", status);
            SetReference(serializedView, "button", root.GetComponent<Button>());
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, RoomSlotPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void InstallLobbyScene()
    {
        Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[LobbySample] SampleLobbyScene has no Canvas.");
            return;
        }

        EnsureEventSystem();
        RectTransform filterPanel = EnsureFilterPanel(canvas.transform);
        TMP_InputField searchInput = EnsureInputField(filterPanel, "LobbySearchInput", "Search room name or ID", new Vector2(0f, 1f), new Vector2(0.36f, 1f), new Vector2(0f, -88f), new Vector2(-12f, -140f));
        TMP_Dropdown sortDropdown = EnsureDropdown(filterPanel, "LobbySortDropdown", new Vector2(0.36f, 1f), new Vector2(0.56f, 1f), new Vector2(8f, -88f), new Vector2(-8f, -140f));
        TMP_Dropdown regionDropdown = EnsureDropdown(filterPanel, "LobbyRegionDropdown", new Vector2(0.56f, 1f), new Vector2(0.72f, 1f), new Vector2(8f, -88f), new Vector2(-8f, -140f));
        TMP_Dropdown gameModeDropdown = EnsureDropdown(filterPanel, "LobbyGameModeDropdown", new Vector2(0.72f, 1f), new Vector2(1f, 1f), new Vector2(8f, -88f), new Vector2(0f, -140f));
        Toggle openSeatsToggle = EnsureToggle(filterPanel, "LobbyOpenSeatsOnlyToggle", "Open Seats Only", new Vector2(0f, 1f), new Vector2(0.28f, 1f), new Vector2(0f, -162f), new Vector2(-8f, -210f));
        Toggle hidePrivateToggle = EnsureToggle(filterPanel, "LobbyHidePrivateRoomsToggle", "Hide Private", new Vector2(0.28f, 1f), new Vector2(0.52f, 1f), new Vector2(8f, -162f), new Vector2(-8f, -210f));
        TMP_Text roomCountText = EnsureSceneText(filterPanel, "LobbyRoomCountText", "Rooms: 0 / 0", new Vector2(0.72f, 1f), new Vector2(1f, 1f), new Vector2(8f, -162f), new Vector2(0f, -210f), 26f, TextAlignmentOptions.Right);

        EnsureSceneText(filterPanel, "LobbyFilterTitleText", "Lobby Rooms", new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(0f, -62f), 36f, TextAlignmentOptions.Left);
        EnsureSceneText(filterPanel, "LobbySearchLabel", "Search", new Vector2(0f, 1f), new Vector2(0.36f, 1f), new Vector2(0f, -62f), new Vector2(-12f, -86f), 20f, TextAlignmentOptions.Left);
        EnsureSceneText(filterPanel, "LobbySortLabel", "Sort", new Vector2(0.36f, 1f), new Vector2(0.56f, 1f), new Vector2(8f, -62f), new Vector2(-8f, -86f), 20f, TextAlignmentOptions.Left);
        EnsureSceneText(filterPanel, "LobbyRegionLabel", "Region", new Vector2(0.56f, 1f), new Vector2(0.72f, 1f), new Vector2(8f, -62f), new Vector2(-8f, -86f), 20f, TextAlignmentOptions.Left);
        EnsureSceneText(filterPanel, "LobbyGameModeLabel", "Game Mode", new Vector2(0.72f, 1f), new Vector2(1f, 1f), new Vector2(8f, -62f), new Vector2(0f, -86f), 20f, TextAlignmentOptions.Left);

        RectTransform scrollView = GameObject.Find("Scroll View")?.GetComponent<RectTransform>();
        if (scrollView != null)
        {
            scrollView.anchorMin = new Vector2(0f, 0f);
            scrollView.anchorMax = new Vector2(1f, 1f);
            scrollView.offsetMin = new Vector2(40f, 60f);
            scrollView.offsetMax = new Vector2(-40f, -360f);
            scrollView.pivot = new Vector2(0.5f, 0.5f);
        }

        RectTransform contentRoot = GameObject.Find("Content")?.GetComponent<RectTransform>();
        GameObject controllerRoot = GameObject.Find(ControllerRootName);
        if (controllerRoot == null)
        {
            controllerRoot = new GameObject(ControllerRootName);
        }

        LobbyRoomListController listController = controllerRoot.GetComponent<LobbyRoomListController>();
        if (listController == null)
        {
            listController = controllerRoot.AddComponent<LobbyRoomListController>();
        }

        LobbyRoomFilterController filterController = controllerRoot.GetComponent<LobbyRoomFilterController>();
        if (filterController == null)
        {
            filterController = controllerRoot.AddComponent<LobbyRoomFilterController>();
        }

        SerializedObject listObject = new SerializedObject(listController);
        SetReference(listObject, "contentRoot", contentRoot);
        SetReference(listObject, "roomSlotPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(RoomSlotPrefabPath));
        listObject.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject filterObject = new SerializedObject(filterController);
        SetReference(filterObject, "roomListController", listController);
        SetReference(filterObject, "searchInput", searchInput);
        SetReference(filterObject, "sortDropdown", sortDropdown);
        SetReference(filterObject, "regionDropdown", regionDropdown);
        SetReference(filterObject, "gameModeDropdown", gameModeDropdown);
        SetReference(filterObject, "openSeatsOnlyToggle", openSeatsToggle);
        SetReference(filterObject, "hidePrivateRoomsToggle", hidePrivateToggle);
        SetReference(filterObject, "roomCountText", roomCountText);
        filterObject.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static RectTransform EnsureFilterPanel(Transform canvasTransform)
    {
        GameObject panel = FindDirectChild(canvasTransform, FilterPanelName);
        if (panel == null)
        {
            panel = new GameObject(FilterPanelName, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasTransform, false);
        }

        RectTransform rectTransform = panel.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 1f);
        rectTransform.offsetMin = new Vector2(40f, -330f);
        rectTransform.offsetMax = new Vector2(-40f, -40f);

        Image image = panel.GetComponent<Image>();
        image.color = new Color(0.08f, 0.09f, 0.1f, 0.88f);
        return rectTransform;
    }

    private static TMP_Text EnsureSlotText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject target = FindDirectChild(parent, name);
        if (target == null)
        {
            target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            target.transform.SetParent(parent, false);
        }

        TMP_Text text = target.GetComponent<TMP_Text>();
        ConfigureText(text, fontSize, alignment, Color.black);
        ConfigureRect(target.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
        return text;
    }

    private static TMP_Text EnsureSceneText(Transform parent, string name, string value, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject target = FindDirectChild(parent, name);
        if (target == null)
        {
            target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            target.transform.SetParent(parent, false);
        }

        TMP_Text text = target.GetComponent<TMP_Text>();
        ConfigureText(text, fontSize, alignment, Color.white);
        text.text = value;
        ConfigureRect(target.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
        return text;
    }

    private static TMP_InputField EnsureInputField(Transform parent, string name, string placeholder, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject target = FindDirectChild(parent, name);
        if (target == null)
        {
            target = TMP_DefaultControls.CreateInputField(GetStandardResources());
            target.name = name;
            target.transform.SetParent(parent, false);
        }

        TMP_InputField input = target.GetComponent<TMP_InputField>();
        input.text = string.Empty;
        input.textComponent.fontSize = 20f;
        input.textComponent.color = Color.black;

        if (input.placeholder is TMP_Text placeholderText)
        {
            placeholderText.text = placeholder;
            placeholderText.fontSize = 20f;
        }

        ConfigureRect(target.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
        return input;
    }

    private static TMP_Dropdown EnsureDropdown(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject target = FindDirectChild(parent, name);
        if (target == null)
        {
            target = TMP_DefaultControls.CreateDropdown(GetStandardResources());
            target.name = name;
            target.transform.SetParent(parent, false);
        }

        TMP_Dropdown dropdown = target.GetComponent<TMP_Dropdown>();
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string> { "All" });
        dropdown.value = 0;
        dropdown.RefreshShownValue();

        if (dropdown.captionText != null)
        {
            dropdown.captionText.fontSize = 20f;
        }

        if (dropdown.itemText != null)
        {
            dropdown.itemText.fontSize = 20f;
        }

        ConfigureRect(target.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
        return dropdown;
    }

    private static Toggle EnsureToggle(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject target = FindDirectChild(parent, name);
        if (target == null)
        {
            target = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            target.transform.SetParent(parent, false);
            GameObject background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(target.transform, false);
            GameObject checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkmark.transform.SetParent(background.transform, false);
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(target.transform, false);
        }

        Toggle toggle = target.GetComponent<Toggle>();
        toggle.isOn = false;

        RectTransform backgroundRect = target.transform.Find("Background").GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.anchoredPosition = new Vector2(20f, 0f);
        backgroundRect.sizeDelta = new Vector2(32f, 32f);

        Image backgroundImage = backgroundRect.GetComponent<Image>();
        backgroundImage.color = Color.white;

        RectTransform checkmarkRect = backgroundRect.Find("Checkmark").GetComponent<RectTransform>();
        checkmarkRect.anchorMin = Vector2.zero;
        checkmarkRect.anchorMax = Vector2.one;
        checkmarkRect.offsetMin = new Vector2(6f, 6f);
        checkmarkRect.offsetMax = new Vector2(-6f, -6f);

        Image checkmarkImage = checkmarkRect.GetComponent<Image>();
        checkmarkImage.color = new Color(0.1f, 0.5f, 0.2f, 1f);

        TMP_Text labelText = target.transform.Find("Label").GetComponent<TMP_Text>();
        ConfigureText(labelText, 22f, TextAlignmentOptions.Left, Color.white);
        labelText.text = label;
        ConfigureRect(labelText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(48f, 0f), Vector2.zero);

        toggle.targetGraphic = backgroundImage;
        toggle.graphic = checkmarkImage;
        ConfigureRect(target.GetComponent<RectTransform>(), anchorMin, anchorMax, offsetMin, offsetMax);
        return toggle;
    }

    private static void ConfigureText(TMP_Text text, float fontSize, TextAlignmentOptions alignment, Color color)
    {
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void ConfigureRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private static void RenameChild(Transform parent, string oldName, string newName)
    {
        GameObject oldChild = FindDirectChild(parent, oldName);
        GameObject newChild = FindDirectChild(parent, newName);
        if (oldChild != null && newChild == null)
        {
            oldChild.name = newName;
        }
    }

    private static void RemoveLegacySlotText(Transform parent, string name)
    {
        GameObject child = FindDirectChild(parent, name);
        if (child != null)
        {
            Object.DestroyImmediate(child);
        }
    }

    private static GameObject FindDirectChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        return child != null ? child.gameObject : null;
    }

    private static bool PrefabHasChild(string prefabPath, string childName)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            return FindDirectChild(root.transform, childName) != null;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    private static TMP_DefaultControls.Resources GetStandardResources()
    {
        return new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
        };
    }
}
