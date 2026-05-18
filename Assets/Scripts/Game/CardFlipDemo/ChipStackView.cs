using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
// 금액을 칩 단위로 쪼개어 2D UI 또는 월드 공간 칩 스택으로 시각화합니다.
// 베팅 칩 애니메이터가 런타임에 생성해서 좌석 앞 베팅액과 팟 지급 칩을 표시할 때 사용합니다.
public sealed class ChipStackView : MonoBehaviour
{
    private readonly struct UiChipLayer
    {
        public readonly RectTransform RectTransform;
        public readonly int SortOrder;
        public readonly int CreationOrder;

        public UiChipLayer(RectTransform rectTransform, int sortOrder, int creationOrder)
        {
            RectTransform = rectTransform;
            SortOrder = sortOrder;
            CreationOrder = creationOrder;
        }
    }

    [System.Serializable]
    private sealed class ChipDenomination
    {
        [SerializeField] private int amount;
        [SerializeField] private Sprite sprite;
        [SerializeField] private string resourcePath;

        public int Amount => amount;
        public Sprite Sprite => sprite != null ? sprite : Resources.Load<Sprite>(resourcePath);

        public ChipDenomination(int amount, string resourcePath)
        {
            this.amount = amount;
            this.resourcePath = resourcePath;
        }
    }

    [Header("Test Amount")]
    [Tooltip("에디터에서 칩 스택을 미리 볼 때 사용할 테스트 금액입니다.")]
    [SerializeField] private int amount = 1730;
    [Tooltip("인스펙터 값이 바뀔 때 에디터에서도 칩 스택을 즉시 다시 그릴지 여부입니다.")]
    [SerializeField] private bool rebuildInEditMode = true;

    [Header("Chip Sprites")]
    [Tooltip("금액 단위별 칩 이미지입니다. 큰 단위부터 작은 단위 순서로 계산됩니다.")]
    [SerializeField] private ChipDenomination[] chipDenominations =
    {
        new ChipDenomination(10000, "Sprites/Chips/Chips_10000"),
        new ChipDenomination(1000, "Sprites/Chips/Chips_1000"),
        new ChipDenomination(100, "Sprites/Chips/Chips_100"),
        new ChipDenomination(10, "Sprites/Chips/Chips_10")
    };

    [Header("Layout")]
    [Tooltip("켜져 있으면 SpriteRenderer 대신 UI Image/RectTransform으로 칩을 생성합니다.")]
    [SerializeField] private bool renderAsUi;
    [Tooltip("같은 금액 칩을 위로 쌓을 때 칩 하나마다 더해지는 오프셋입니다. UI 모드에서는 픽셀 좌표로 사용됩니다.")]
    [SerializeField] private Vector2 stackOffset = new Vector2(0f, 0.015f);
    [Tooltip("서로 다른 칩 단위 그룹을 대각선으로 벌릴 때 사용하는 오프셋입니다. UI 모드에서는 픽셀 좌표로 사용됩니다.")]
    [SerializeField] private Vector2 diagonalBackOffset = new Vector2(0.16f, 0.09f);
    [Tooltip("UI Image로 생성되는 칩 하나의 RectTransform 크기입니다.")]
    [SerializeField] private Vector2 uiChipSize = new Vector2(42f, 42f);
    [Tooltip("월드 SpriteRenderer 모드에서 칩의 X/Z 스케일입니다.")]
    [SerializeField] private float chipScale = 0.18f;
    [Tooltip("월드 SpriteRenderer 모드에서 칩의 Y 스케일입니다.")]
    [SerializeField] private float chipYScale = 0.25f;
    [Tooltip("월드 SpriteRenderer 모드에서 칩을 테이블 위에 눕혀 보이게 하는 X축 회전값입니다.")]
    [SerializeField] private float chipXRotation = 45f;
    [Tooltip("월드 SpriteRenderer 모드에서 첫 칩 그룹의 기준 정렬값입니다.")]
    [SerializeField] private int baseSortingOrder = 500;
    [Tooltip("월드 SpriteRenderer 모드에서 칩 단위 그룹 사이에 벌리는 정렬값 간격입니다.")]
    [SerializeField] private int groupSortingGap = 100;
    [Tooltip("월드 SpriteRenderer 모드에서 사용할 Sorting Layer 이름입니다.")]
    [SerializeField] private string sortingLayerName = "Default";

    private readonly List<GameObject> generatedChips = new List<GameObject>();
    private readonly List<UiChipLayer> generatedUiLayers = new List<UiChipLayer>();
    private int generatedUiLayerSerial;
#if UNITY_EDITOR
    private bool refreshQueued;
#endif

    private void OnEnable()
    {
        Refresh();
    }

    private void OnValidate()
    {
        amount = Mathf.Max(0, amount);
        uiChipSize = new Vector2(Mathf.Max(1f, uiChipSize.x), Mathf.Max(1f, uiChipSize.y));
        chipScale = Mathf.Max(0.01f, chipScale);
        chipYScale = Mathf.Max(0.01f, chipYScale);
        chipXRotation = Mathf.Clamp(chipXRotation, -89f, 89f);
        groupSortingGap = Mathf.Max(1, groupSortingGap);

        if (rebuildInEditMode || Application.isPlaying)
        {
            if (Application.isPlaying)
            {
                Refresh();
            }
            else
            {
                QueueEditorRefresh();
            }
        }
    }

    [ContextMenu("Refresh Chip Stack")]
    public void Refresh()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        ClearGeneratedChips();
        generatedUiLayers.Clear();
        generatedUiLayerSerial = 0;

        int remainingAmount = amount;
        int visibleGroupIndex = 0;

        foreach (ChipDenomination denomination in chipDenominations)
        {
            if (denomination == null || denomination.Amount <= 0)
            {
                continue;
            }

            int chipCount = remainingAmount / denomination.Amount;
            remainingAmount %= denomination.Amount;

            if (chipCount <= 0)
            {
                continue;
            }

            CreateGroup(denomination, chipCount, visibleGroupIndex);
            visibleGroupIndex++;
        }

        ApplyUiSiblingOrder();
    }

    public void SetAmount(int value)
    {
        amount = Mathf.Max(0, value);
        Refresh();
    }

    public void ConfigureLayout(
        Vector2 newStackOffset,
        Vector2 newDiagonalBackOffset,
        float newChipScale,
        float newChipYScale,
        float newChipXRotation,
        int newBaseSortingOrder,
        int newGroupSortingGap)
    {
        stackOffset = newStackOffset;
        diagonalBackOffset = newDiagonalBackOffset;
        chipScale = Mathf.Max(0.01f, newChipScale);
        chipYScale = Mathf.Max(0.01f, newChipYScale);
        chipXRotation = Mathf.Clamp(newChipXRotation, -89f, 89f);
        baseSortingOrder = newBaseSortingOrder;
        groupSortingGap = Mathf.Max(1, newGroupSortingGap);
        Refresh();
    }

    public void ConfigureUiLayout(
        Vector2 newStackOffset,
        Vector2 newDiagonalBackOffset,
        Vector2 newUiChipSize,
        int newBaseSortingOrder,
        int newGroupSortingGap)
    {
        renderAsUi = true;
        stackOffset = newStackOffset;
        diagonalBackOffset = newDiagonalBackOffset;
        uiChipSize = new Vector2(Mathf.Max(1f, newUiChipSize.x), Mathf.Max(1f, newUiChipSize.y));
        baseSortingOrder = newBaseSortingOrder;
        groupSortingGap = Mathf.Max(1, newGroupSortingGap);
        Refresh();
    }

    [ContextMenu("Sample Amount 560")]
    private void SetSampleAmount560()
    {
        SetAmount(560);
    }

    [ContextMenu("Sample Amount 1170")]
    private void SetSampleAmount1170()
    {
        SetAmount(1170);
    }

    [ContextMenu("Sample Amount 1730")]
    private void SetSampleAmount1730()
    {
        SetAmount(1730);
    }

    private void CreateGroup(ChipDenomination denomination, int chipCount, int groupIndex)
    {
        Sprite chipSprite = denomination.Sprite;
        if (chipSprite == null)
        {
            Debug.LogWarning($"[ChipStackView] Missing chip sprite for {denomination.Amount}.", this);
            return;
        }

        Vector3 groupOffset = GetGroupOffset(groupIndex);
        int groupBaseSortingOrder = baseSortingOrder - groupIndex * groupSortingGap;

        for (int chipIndex = 0; chipIndex < chipCount; chipIndex++)
        {
            GameObject chipObject = new GameObject($"{denomination.Amount}_Chip_{chipIndex + 1:00}");
            int visualSortOrder = groupBaseSortingOrder + chipIndex;
            if (renderAsUi)
            {
                CreateUiChip(chipObject, chipSprite, groupOffset, chipIndex, visualSortOrder);
            }
            else
            {
                CreateWorldChip(chipObject, chipSprite, groupOffset, chipIndex, groupBaseSortingOrder);
            }

            generatedChips.Add(chipObject);
        }
    }

    private void CreateUiChip(GameObject chipObject, Sprite chipSprite, Vector3 groupOffset, int chipIndex, int visualSortOrder)
    {
        RectTransform rectTransform = chipObject.AddComponent<RectTransform>();
        rectTransform.SetParent(transform, false);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = (Vector2)groupOffset + stackOffset * chipIndex;
        rectTransform.sizeDelta = uiChipSize;
        rectTransform.localScale = Vector3.one;

        Image image = chipObject.AddComponent<Image>();
        image.sprite = chipSprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        generatedUiLayers.Add(new UiChipLayer(rectTransform, visualSortOrder, generatedUiLayerSerial));
        generatedUiLayerSerial++;
    }

    private void ApplyUiSiblingOrder()
    {
        if (!renderAsUi || generatedUiLayers.Count == 0)
        {
            return;
        }

        generatedUiLayers.Sort((left, right) =>
        {
            int sortCompare = left.SortOrder.CompareTo(right.SortOrder);
            return sortCompare != 0 ? sortCompare : left.CreationOrder.CompareTo(right.CreationOrder);
        });

        for (int i = 0; i < generatedUiLayers.Count; i++)
        {
            if (generatedUiLayers[i].RectTransform != null)
            {
                generatedUiLayers[i].RectTransform.SetSiblingIndex(i);
            }
        }
    }

    private void CreateWorldChip(
        GameObject chipObject,
        Sprite chipSprite,
        Vector3 groupOffset,
        int chipIndex,
        int groupBaseSortingOrder)
    {
        chipObject.transform.SetParent(transform, false);
        chipObject.transform.localPosition = groupOffset + (Vector3)(stackOffset * chipIndex);
        chipObject.transform.localRotation = Quaternion.Euler(chipXRotation, 0f, 0f);
        chipObject.transform.localScale = new Vector3(chipScale, chipYScale, chipScale);

        SpriteRenderer spriteRenderer = chipObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = chipSprite;
        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = groupBaseSortingOrder + chipIndex;
    }

    private Vector3 GetGroupOffset(int groupIndex)
    {
        if (groupIndex == 0)
        {
            return Vector3.zero;
        }

        int side = groupIndex % 2 == 1 ? -1 : 1;
        int depth = (groupIndex + 1) / 2;
        return new Vector3(
            diagonalBackOffset.x * side * depth,
            diagonalBackOffset.y * depth,
            0.01f * depth);
    }

    private void ClearGeneratedChips()
    {
        generatedChips.RemoveAll(chip => chip == null);

        for (int i = generatedChips.Count - 1; i >= 0; i--)
        {
            DestroyChip(generatedChips[i]);
        }

        generatedChips.Clear();
        generatedUiLayers.Clear();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.GetComponent<SpriteRenderer>() != null || child.GetComponent<Image>() != null)
            {
                DestroyChip(child.gameObject);
            }
        }
    }

    private static void DestroyChip(GameObject chipObject)
    {
        if (chipObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(chipObject);
        }
        else
        {
            DestroyImmediate(chipObject);
        }
    }

    private void QueueEditorRefresh()
    {
#if UNITY_EDITOR
        if (refreshQueued)
        {
            return;
        }

        refreshQueued = true;
        UnityEditor.EditorApplication.delayCall += RefreshAfterValidation;
#else
        Refresh();
#endif
    }

#if UNITY_EDITOR
    private void RefreshAfterValidation()
    {
        UnityEditor.EditorApplication.delayCall -= RefreshAfterValidation;
        refreshQueued = false;

        if (this == null)
        {
            return;
        }

        Refresh();
    }
#endif
}
