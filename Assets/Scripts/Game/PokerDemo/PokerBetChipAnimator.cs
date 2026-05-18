using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 베팅 칩의 UI 애니메이션을 담당합니다.
// 베팅 매니저의 칩 지불 이벤트를 받아 좌석 앞 칩 스택을 만들고, 라운드 종료 시 팟으로 모으거나 승자에게 지급합니다.
public sealed class PokerBetChipAnimator : MonoBehaviour
{
    private const int SeatCount = 6;

    [Header("Scene References")]
    [Tooltip("포커 UI 배치와 플레이어 좌석 정보를 가져오는 UI 매니저입니다.")]
    [SerializeField] private PokerUIManager ui;
    [Tooltip("플레이어가 칩을 지불했을 때 이벤트를 받는 베팅 매니저입니다.")]
    [SerializeField] private BettingManager betting;
    [Tooltip("런타임에 생성되는 베팅 칩 UI들이 들어갈 부모 RectTransform입니다.")]
    [SerializeField] private RectTransform chipRoot;
    [Tooltip("베팅 칩을 팟으로 모을 때 도착 지점으로 쓰는 UI 앵커입니다.")]
    [SerializeField] private RectTransform potBetAnchor;
    [Tooltip("0번부터 5번 좌석까지 각 좌석 앞 베팅 칩이 표시될 UI 앵커입니다.")]
    [SerializeField] private RectTransform[] seatBetAnchors = new RectTransform[SeatCount];

    [Header("Fallback UI Positions")]
    [Tooltip("팟 베팅 앵커가 연결되지 않았을 때 사용할 팟 위치입니다. 테이블 패널 중앙 기준 UI 좌표입니다.")]
    [SerializeField] private Vector2 potAnchoredPosition = new Vector2(0f, 80f);
    [Tooltip("좌석 베팅 앵커가 비어 있을 때 사용할 좌석별 칩 위치입니다. 테이블 패널 중앙 기준 UI 좌표입니다.")]
    [SerializeField] private Vector2[] seatAnchoredPositions =
    {
        new Vector2(0f, -520f),
        new Vector2(-360f, -210f),
        new Vector2(-360f, 260f),
        new Vector2(0f, 530f),
        new Vector2(360f, 260f),
        new Vector2(360f, -210f)
    };

    [Header("Chip Stack")]
    [Tooltip("같은 금액 칩을 위로 쌓을 때 칩 하나마다 더해지는 UI 좌표 오프셋입니다.")]
    [SerializeField] private Vector2 chipStackOffset = new Vector2(0f, 3f);
    [Tooltip("서로 다른 칩 단위 그룹을 대각선으로 벌릴 때 사용하는 UI 좌표 오프셋입니다.")]
    [SerializeField] private Vector2 chipDiagonalBackOffset = new Vector2(18f, 10f);
    [Tooltip("생성되는 칩 Image 하나의 RectTransform 크기입니다.")]
    [SerializeField] private Vector2 chipSize = new Vector2(42f, 42f);
    [Tooltip("기존 SpriteRenderer 모드와 호환하기 위해 전달하는 기준 정렬값입니다. UI 모드에서는 생성 순서가 표시 순서를 결정합니다.")]
    [SerializeField] private int sortingBase = 700;
    [Tooltip("좌석별 칩 정렬 기준값 간격입니다. UI 모드에서는 기존 호환용 값입니다.")]
    [SerializeField] private int seatSortingGap = 120;
    [Tooltip("칩 단위 그룹별 정렬 기준값 간격입니다. UI 모드에서는 기존 호환용 값입니다.")]
    [SerializeField] private int groupSortingGap = 40;

    [Header("Animation")]
    [Tooltip("베팅 칩 수량이 갱신될 때 살짝 커졌다 돌아오는 크기입니다.")]
    [SerializeField] private float updatePunchScale = 0.08f;
    [Tooltip("베팅 칩 갱신 펀치 애니메이션 시간입니다.")]
    [SerializeField] private float updatePunchDuration = 0.16f;
    [Tooltip("좌석 앞 칩들이 팟 위치로 이동하는 시간입니다.")]
    [SerializeField] private float collectMoveDuration = 0.42f;
    [Tooltip("팟으로 모인 칩들이 사라지는 페이드 시간입니다.")]
    [SerializeField] private float collectFadeDuration = 0.18f;
    [Tooltip("여러 좌석 칩이 팟에 모일 때 겹치지 않도록 도착 위치를 살짝 벌리는 반경입니다.")]
    [SerializeField] private float potSpread = 16f;
    [Tooltip("좌석 칩을 팟으로 모을 때 사용하는 DOTween Ease입니다.")]
    [SerializeField] private Ease collectEase = Ease.InOutCubic;

    [Header("Pot Payout")]
    [SerializeField] private int maxPayoutChipsPerWinner = 6;
    [SerializeField] private float payoutScatterRadius = 34f;
    [SerializeField] private float payoutPopDuration = 0.15f;
    [SerializeField] private float payoutHoldDuration = 0.1f;
    [SerializeField] private float payoutMoveDuration = 0.48f;
    [SerializeField] private float payoutFadeDuration = 0.16f;
    [SerializeField] private Ease payoutMoveEase = Ease.InCubic;

    private readonly Dictionary<string, ChipStackView> activeStacks = new Dictionary<string, ChipStackView>();
    private readonly Dictionary<string, int> activeBetAmounts = new Dictionary<string, int>();
    private readonly List<ChipStackView> activePayoutStacks = new List<ChipStackView>();
    private BettingManager subscribedBetting;
    private Sequence collectSequence;

    private void Awake()
    {
        ResolveSceneAnchors();
        EnsureSeatPositionArray();
        EnsureChipRoot();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        KillCollectSequence();
        ClearPayoutChips();
        foreach (ChipStackView stack in activeStacks.Values)
        {
            if (stack != null)
            {
                KillStackTweens(stack);
            }
        }
    }

    private void OnValidate()
    {
        EnsureSeatPositionArray();
        chipSize = new Vector2(Mathf.Max(1f, chipSize.x), Mathf.Max(1f, chipSize.y));
        collectMoveDuration = Mathf.Max(0.01f, collectMoveDuration);
        collectFadeDuration = Mathf.Max(0.01f, collectFadeDuration);
        updatePunchDuration = Mathf.Max(0.01f, updatePunchDuration);
        potSpread = Mathf.Max(0f, potSpread);
        maxPayoutChipsPerWinner = Mathf.Clamp(maxPayoutChipsPerWinner, 1, 6);
        payoutScatterRadius = Mathf.Max(0f, payoutScatterRadius);
        payoutPopDuration = Mathf.Max(0.01f, payoutPopDuration);
        payoutHoldDuration = Mathf.Max(0f, payoutHoldDuration);
        payoutMoveDuration = Mathf.Max(0.01f, payoutMoveDuration);
        payoutFadeDuration = Mathf.Max(0.01f, payoutFadeDuration);
    }

    public void Bind(PokerUIManager targetUi, BettingManager targetBetting)
    {
        if (ui == targetUi && betting == targetBetting)
        {
            Subscribe();
            return;
        }

        Unsubscribe();
        ui = targetUi;
        betting = targetBetting;
        Subscribe();
    }

    public void ClearActiveChips()
    {
        KillCollectSequence();
        ClearPayoutChips();

        foreach (ChipStackView stack in activeStacks.Values)
        {
            if (stack != null)
            {
                KillStackTweens(stack);
                Destroy(stack.gameObject);
            }
        }

        activeStacks.Clear();
        activeBetAmounts.Clear();
    }

    public IEnumerator CollectBetsToPot()
    {
        if (activeStacks.Count == 0)
        {
            yield break;
        }

        EnsureChipRoot();
        KillCollectSequence();

        List<ChipStackView> stacks = new List<ChipStackView>(activeStacks.Values);
        Vector2 potPosition = ResolvePotPosition();
        float fadeStartTime = collectMoveDuration * 0.65f;
        collectSequence = DOTween.Sequence();

        for (int i = 0; i < stacks.Count; i++)
        {
            ChipStackView stack = stacks[i];
            if (stack == null)
            {
                continue;
            }

            Vector2 targetPosition = potPosition + GetPotSpreadOffset(i, stacks.Count);
            RectTransform stackTransform = stack.transform as RectTransform;
            if (stackTransform == null)
            {
                continue;
            }

            stackTransform.DOKill();

            // 모든 좌석의 칩이 동시에 움직이도록 절대 삽입 시간을 사용합니다.
            // 조인과 지연 설정을 함께 쓰면 뒤쪽 트윈이 앞서 삽입된 트윈 타이밍에 묶일 수 있습니다.
            collectSequence.Insert(0f, stackTransform.DOAnchorPos(targetPosition, collectMoveDuration).SetEase(collectEase));
            collectSequence.Insert(fadeStartTime, stackTransform.DOScale(Vector3.zero, collectFadeDuration).SetEase(Ease.InBack));

            foreach (Image image in stack.GetComponentsInChildren<Image>())
            {
                if (image != null)
                {
                    collectSequence.Insert(fadeStartTime, image.DOFade(0f, collectFadeDuration));
                }
            }
        }

        yield return collectSequence.WaitForCompletion();
        foreach (ChipStackView stack in stacks)
        {
            if (stack != null)
            {
                KillStackTweens(stack);
            }
        }

        collectSequence = null;
        ClearActiveChips();
    }

    public IEnumerator PlayPotPayout(IReadOnlyList<PlayerState> winners, int totalAmount)
    {
        if (winners == null || winners.Count == 0 || totalAmount <= 0)
        {
            yield break;
        }

        EnsureChipRoot();
        KillCollectSequence();
        ClearPayoutChips();

        Vector2 potPosition = ResolvePotPosition();
        collectSequence = DOTween.Sequence();

        int share = totalAmount / winners.Count;
        int remainder = totalAmount % winners.Count;

        for (int winnerIndex = 0; winnerIndex < winners.Count; winnerIndex++)
        {
            PlayerState winner = winners[winnerIndex];
            if (winner == null)
            {
                continue;
            }

            int payoutAmount = share + (winnerIndex < remainder ? 1 : 0);
            int chipCount = GetPayoutChipCount(payoutAmount);
            Vector2 targetPosition = ResolveSeatPosition(winner.SeatIndex);

            for (int chipIndex = 0; chipIndex < chipCount; chipIndex++)
            {
                ChipStackView stack = CreatePayoutStack(winner, winnerIndex, chipIndex);
                RectTransform stackTransform = stack.transform as RectTransform;
                if (stackTransform == null)
                {
                    continue;
                }

                Vector2 scatterOffset = GetPayoutScatterOffset(chipIndex, chipCount, winnerIndex);
                Vector2 scatterPosition = potPosition + scatterOffset;
                Vector2 destination = targetPosition + scatterOffset * 0.18f;

                stackTransform.anchoredPosition = potPosition;
                stackTransform.localScale = Vector3.one * 0.45f;
                SetStackAlpha(stack, 0f);

                collectSequence.Join(stackTransform.DOAnchorPos(scatterPosition, payoutPopDuration).SetEase(Ease.OutBack));
                collectSequence.Join(stackTransform.DOScale(1.05f, payoutPopDuration).SetEase(Ease.OutBack));
                JoinStackFade(collectSequence, stack, 1f, payoutPopDuration, 0f);

                float moveStart = payoutPopDuration + payoutHoldDuration + winnerIndex * 0.08f;
                collectSequence.Insert(moveStart, stackTransform.DOAnchorPos(destination, payoutMoveDuration).SetEase(payoutMoveEase));
                collectSequence.Insert(moveStart, stackTransform.DOScale(0.62f, payoutMoveDuration).SetEase(Ease.InBack));
                JoinStackFade(
                    collectSequence,
                    stack,
                    0f,
                    payoutFadeDuration,
                    moveStart + Mathf.Max(0f, payoutMoveDuration - payoutFadeDuration));
            }
        }

        yield return collectSequence.WaitForCompletion();
        collectSequence = null;
        ClearPayoutChips();
    }

    private void Subscribe()
    {
        if (!isActiveAndEnabled || betting == null || subscribedBetting == betting)
        {
            return;
        }

        Unsubscribe();
        betting.PlayerPaidChips += HandlePlayerPaidChips;
        subscribedBetting = betting;
    }

    private void Unsubscribe()
    {
        if (subscribedBetting == null)
        {
            return;
        }

        subscribedBetting.PlayerPaidChips -= HandlePlayerPaidChips;
        subscribedBetting = null;
    }

    private void HandlePlayerPaidChips(PlayerState player, int paid)
    {
        if (player == null || paid <= 0)
        {
            return;
        }

        string playerId = player.PlayerId;
        activeBetAmounts.TryGetValue(playerId, out int currentAmount);
        int newAmount = currentAmount + paid;
        activeBetAmounts[playerId] = newAmount;

        ChipStackView stack = GetOrCreateStack(player);
        stack.SetAmount(newAmount);
        RectTransform stackTransform = stack.transform as RectTransform;
        if (stackTransform != null)
        {
            stackTransform.anchoredPosition = ResolveSeatPosition(player.SeatIndex);
            PlayStackUpdated(stackTransform);
        }
    }

    private ChipStackView GetOrCreateStack(PlayerState player)
    {
        if (activeStacks.TryGetValue(player.PlayerId, out ChipStackView existingStack) && existingStack != null)
        {
            return existingStack;
        }

        EnsureChipRoot();

        GameObject stackObject = new GameObject($"BetChips_Seat{player.SeatIndex}_{player.PlayerId}");
        stackObject.SetActive(false);
        RectTransform stackRect = stackObject.AddComponent<RectTransform>();
        stackRect.SetParent(chipRoot, false);
        stackRect.anchorMin = new Vector2(0.5f, 0.5f);
        stackRect.anchorMax = new Vector2(0.5f, 0.5f);
        stackRect.pivot = new Vector2(0.5f, 0.5f);
        stackRect.anchoredPosition = ResolveSeatPosition(player.SeatIndex);
        stackRect.sizeDelta = Vector2.zero;
        stackRect.localScale = Vector3.one;

        ChipStackView stack = stackObject.AddComponent<ChipStackView>();
        stack.ConfigureUiLayout(
            chipStackOffset,
            chipDiagonalBackOffset,
            chipSize,
            sortingBase + player.SeatIndex * seatSortingGap,
            groupSortingGap);
        stack.SetAmount(0);
        stackObject.SetActive(true);

        activeStacks[player.PlayerId] = stack;
        return stack;
    }

    private ChipStackView CreatePayoutStack(PlayerState winner, int winnerIndex, int chipIndex)
    {
        EnsureChipRoot();

        GameObject stackObject = new GameObject($"PotPayoutChip_Seat{winner.SeatIndex}_{winnerIndex}_{chipIndex}");
        stackObject.SetActive(false);
        RectTransform stackRect = stackObject.AddComponent<RectTransform>();
        stackRect.SetParent(chipRoot, false);
        stackRect.anchorMin = new Vector2(0.5f, 0.5f);
        stackRect.anchorMax = new Vector2(0.5f, 0.5f);
        stackRect.pivot = new Vector2(0.5f, 0.5f);
        stackRect.sizeDelta = Vector2.zero;
        stackRect.localScale = Vector3.one;

        ChipStackView stack = stackObject.AddComponent<ChipStackView>();
        stack.ConfigureUiLayout(
            Vector2.zero,
            Vector2.zero,
            chipSize,
            sortingBase + 2000 + winnerIndex * seatSortingGap + chipIndex,
            groupSortingGap);
        stack.SetAmount(10);
        stackObject.SetActive(true);

        activePayoutStacks.Add(stack);
        return stack;
    }

    private int GetPayoutChipCount(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        int referenceAmount = betting != null ? Mathf.Max(1, betting.BigBlind) : 20;
        return Mathf.Clamp(Mathf.CeilToInt(amount / (float)referenceAmount), 1, maxPayoutChipsPerWinner);
    }

    private Vector2 GetPayoutScatterOffset(int index, int count, int winnerIndex)
    {
        if (count <= 1 || payoutScatterRadius <= 0f)
        {
            return Vector2.zero;
        }

        float angle = (Mathf.PI * 2f * index) / count + winnerIndex * 0.55f;
        float radius = payoutScatterRadius * (0.72f + 0.28f * ((index % 2) + 1));
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private static void JoinStackFade(Sequence sequence, ChipStackView stack, float alpha, float duration, float delay)
    {
        foreach (Image image in stack.GetComponentsInChildren<Image>(true))
        {
            if (image != null)
            {
                sequence.Insert(delay, image.DOFade(alpha, duration));
            }
        }
    }

    private static void SetStackAlpha(ChipStackView stack, float alpha)
    {
        foreach (Image image in stack.GetComponentsInChildren<Image>(true))
        {
            if (image == null)
            {
                continue;
            }

            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }

    private void ClearPayoutChips()
    {
        activePayoutStacks.RemoveAll(stack => stack == null);
        foreach (ChipStackView stack in activePayoutStacks)
        {
            if (stack == null)
            {
                continue;
            }

            KillStackTweens(stack);
            Destroy(stack.gameObject);
        }

        activePayoutStacks.Clear();
    }

    private void PlayStackUpdated(RectTransform stackTransform)
    {
        if (stackTransform == null)
        {
            return;
        }

        stackTransform.DOKill();
        stackTransform.localScale = Vector3.one;
        stackTransform.DOPunchScale(Vector3.one * updatePunchScale, updatePunchDuration, 1, 0.35f);
    }

    private void KillCollectSequence()
    {
        if (collectSequence == null)
        {
            return;
        }

        if (collectSequence.IsActive())
        {
            collectSequence.Kill(false);
        }

        collectSequence = null;
    }

    private static void KillStackTweens(ChipStackView stack)
    {
        if (stack == null)
        {
            return;
        }

        stack.transform.DOKill();
        foreach (Image image in stack.GetComponentsInChildren<Image>(true))
        {
            if (image != null)
            {
                image.DOKill();
            }
        }
    }

    private Vector2 ResolveSeatPosition(int seatIndex)
    {
        if (seatIndex >= 0 && seatIndex < seatBetAnchors.Length && seatBetAnchors[seatIndex] != null)
        {
            return ResolveAnchorPosition(seatBetAnchors[seatIndex]);
        }

        return seatIndex >= 0 && seatIndex < seatAnchoredPositions.Length
            ? seatAnchoredPositions[seatIndex]
            : Vector2.zero;
    }

    private Vector2 ResolvePotPosition()
    {
        if (potBetAnchor != null)
        {
            return ResolveAnchorPosition(potBetAnchor);
        }

        return potAnchoredPosition;
    }

    private Vector2 ResolveAnchorPosition(RectTransform anchor)
    {
        if (anchor == null)
        {
            return Vector2.zero;
        }

        EnsureChipRoot();
        if (chipRoot == null)
        {
            return anchor.anchoredPosition;
        }

        Vector3 localPosition = chipRoot.InverseTransformPoint(anchor.position);
        return new Vector2(localPosition.x, localPosition.y);
    }

    private Vector2 GetPotSpreadOffset(int index, int count)
    {
        if (count <= 1 || potSpread <= 0f)
        {
            return Vector2.zero;
        }

        float angle = (Mathf.PI * 2f * index) / count;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * potSpread;
    }

    private void EnsureChipRoot()
    {
        if (chipRoot != null)
        {
            return;
        }

        RectTransform parent = ResolveTablePanel();

        GameObject rootObject = new GameObject("BetChipRuntimeRoot", typeof(RectTransform));
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.SetParent(parent != null ? parent : transform, false);
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        chipRoot = rootRect;
    }

    private void ResolveSceneAnchors()
    {
        if (potBetAnchor == null)
        {
            RectTransform anchorsRoot = ResolveBetAnchorsRoot();
            if (anchorsRoot != null)
            {
                potBetAnchor = anchorsRoot.Find("PotBetAnchor") as RectTransform;
            }
        }

        if (seatBetAnchors == null || seatBetAnchors.Length != SeatCount)
        {
            seatBetAnchors = new RectTransform[SeatCount];
        }

        RectTransform root = ResolveBetAnchorsRoot();
        if (root == null)
        {
            return;
        }

        for (int i = 0; i < seatBetAnchors.Length; i++)
        {
            if (seatBetAnchors[i] == null)
            {
                seatBetAnchors[i] = ResolveSeatBetAnchor(i, root);
            }
        }
    }

    private RectTransform ResolveSeatBetAnchor(int seatIndex, RectTransform legacyAnchorsRoot)
    {
        RectTransform tablePanel = ResolveTablePanel();
        Transform seatRoot = tablePanel != null ? tablePanel.Find($"Seat{seatIndex}") : null;
        if (seatRoot != null)
        {
            RectTransform seatAnchor = seatRoot.Find("BetAnchor") as RectTransform;
            if (seatAnchor != null)
            {
                return seatAnchor;
            }

            seatAnchor = seatRoot.Find($"Seat{seatIndex}BetAnchor") as RectTransform;
            if (seatAnchor != null)
            {
                return seatAnchor;
            }
        }

        return legacyAnchorsRoot != null ? legacyAnchorsRoot.Find($"Seat{seatIndex}BetAnchor") as RectTransform : null;
    }

    private RectTransform ResolveBetAnchorsRoot()
    {
        Transform root = transform.Find("BetChipAnchors");
        if (root != null)
        {
            return root as RectTransform;
        }

        RectTransform tablePanel = ResolveTablePanel();
        return tablePanel != null ? tablePanel.Find("BetChipAnchors") as RectTransform : null;
    }

    private RectTransform ResolveTablePanel()
    {
        GameObject canvasObject = GameObject.Find(PokerUIManager.CanvasName);
        if (canvasObject == null)
        {
            return null;
        }

        return canvasObject.transform.Find("TablePanel") as RectTransform;
    }

    private void EnsureSeatPositionArray()
    {
        if (seatAnchoredPositions != null && seatAnchoredPositions.Length == SeatCount)
        {
            return;
        }

        Vector2[] existingPositions = seatAnchoredPositions;
        seatAnchoredPositions = new Vector2[SeatCount];
        for (int i = 0; i < seatAnchoredPositions.Length; i++)
        {
            seatAnchoredPositions[i] = existingPositions != null && i < existingPositions.Length
                ? existingPositions[i]
                : Vector2.zero;
        }
    }
}
