using System.Collections.Generic;
using UnityEngine;

// 3D 카드들을 테이블 좌석/커뮤니티 카드 위치에 맞춰 배치하고 렌더링합니다.
// UI 카드 앵커를 월드 좌표로 변환해 2D UI 배치와 3D 카드 배치를 맞추는 역할도 합니다.
public sealed class Poker3DCardTableView : MonoBehaviour
{
    private struct CardSlotState
    {
        public bool HasCard;
        public Card Card;
        public bool FaceUp;
    }

    public const string RootName = "Poker3DCardRoot";
    private const int SeatCount = 6;
    private const int HoleCardsPerSeat = 2;

    [Header("Cards")]
    [SerializeField] private Poker3DCardView[] seatCards = new Poker3DCardView[SeatCount * HoleCardsPerSeat];
    [SerializeField] private Poker3DCardView[] communityCards = new Poker3DCardView[5];

    [Header("Layout")]
    [SerializeField] private float cardWidth = 0.62f;
    [SerializeField] private float cardHeight = 0.88f;
    [SerializeField] private float cardThickness = 0.025f;
    [SerializeField] private float tableZ = 0f;
    [SerializeField] private float uiCameraDepth = 8f;

    [Header("Reveal Timing")]
    [SerializeField] private bool animateReveals = true;
    [SerializeField] private float playerRevealDelay = 0.08f;
    [SerializeField] private float communityRevealDelay = 0.04f;
    [SerializeField] private float showdownStagger = 0.16f;

    private readonly CardSlotState[] seatStates = new CardSlotState[SeatCount * HoleCardsPerSeat];
    private readonly CardSlotState[] communityStates = new CardSlotState[5];
    private readonly Vector3[] cachedAnchorPositions = new Vector3[SeatCount * HoleCardsPerSeat + 5];
    private readonly Vector2[] cachedAnchorSizes = new Vector2[SeatCount * HoleCardsPerSeat + 5];

    private static readonly Vector3 CardRotation = Vector3.zero;
    private bool layoutDirty = true;
    private Canvas cachedCanvas;
    private Camera cachedCamera;
    private Vector3 cachedCameraPosition;
    private Quaternion cachedCameraRotation;
    private float cachedCameraFieldOfView;
    private float cachedCameraOrthographicSize;
    private bool cachedCameraOrthographic;
    private float cachedCanvasScaleFactor;
    private int cachedScreenWidth = -1;
    private int cachedScreenHeight = -1;

    public bool AnimateReveals => animateReveals;
    public float RevealWaitDuration => GetLongestFlipDuration() + Mathf.Max(playerRevealDelay, communityRevealDelay, showdownStagger);

    private void Awake()
    {
        ResolveCardsFromChildren();
        ApplyDefaultLayout();
        Clear();
    }

    private void OnValidate()
    {
        cardWidth = Mathf.Max(0.01f, cardWidth);
        cardHeight = Mathf.Max(0.01f, cardHeight);
        cardThickness = Mathf.Max(0.001f, cardThickness);
        playerRevealDelay = Mathf.Max(0f, playerRevealDelay);
        communityRevealDelay = Mathf.Max(0f, communityRevealDelay);
        showdownStagger = Mathf.Max(0f, showdownStagger);
        layoutDirty = true;
    }

    public void Render(
        IReadOnlyList<PokerSeat> seats,
        IReadOnlyList<Card> visibleCommunityCards,
        bool revealAllHoleCards)
    {
        ResolveCardsFromChildren();

        for (int seatIndex = 0; seatIndex < SeatCount; seatIndex++)
        {
            PokerSeat seat = seats != null && seatIndex < seats.Count ? seats[seatIndex] : null;
            PlayerState player = seat != null ? seat.Player : null;
            bool faceUp = player != null && (player.IsHuman || revealAllHoleCards);
            RenderSeatCards(seatIndex, player != null ? player.HoleCards : null, faceUp, revealAllHoleCards);
        }

        RenderCards(communityCards, communityStates, visibleCommunityCards, true, communityRevealDelay);
    }

    public void Clear()
    {
        ResolveCardsFromChildren();
        ClearCards(seatCards, seatStates);
        ClearCards(communityCards, communityStates);
    }

    public void ApplyDefaultLayout()
    {
        ResolveCardsFromChildren();

        Vector3[] seatPositions =
        {
            new Vector3(0.95f, -2.25f, tableZ),
            new Vector3(-2.30f, -1.10f, tableZ),
            new Vector3(-2.30f, 1.95f, tableZ),
            new Vector3(0.95f, 3.05f, tableZ),
            new Vector3(2.30f, 1.95f, tableZ),
            new Vector3(2.30f, -1.10f, tableZ)
        };

        for (int seatIndex = 0; seatIndex < SeatCount; seatIndex++)
        {
            Vector3 basePosition = seatPositions[seatIndex];
            SetSlot(seatCards, SeatCardIndex(seatIndex, 0), basePosition + new Vector3(-0.35f, 0f, 0f));
            SetSlot(seatCards, SeatCardIndex(seatIndex, 1), basePosition + new Vector3(0.35f, 0f, 0f));
        }

        for (int i = 0; i < communityCards.Length; i++)
        {
            float x = -1.55f + i * 0.78f;
            SetSlot(communityCards, i, new Vector3(x, 0.55f, tableZ));
        }

        layoutDirty = true;
    }

    public void MarkLayoutDirty()
    {
        layoutDirty = true;
    }

    public void ApplyLayoutFromUiAnchors(
        Canvas canvas,
        IReadOnlyList<CardView> seatCardAnchors,
        IReadOnlyList<CardView> communityCardAnchors)
    {
        Camera camera = ResolveCamera(canvas);
        if (camera == null)
        {
            return;
        }

        if (!ShouldApplyAnchorLayout(canvas, camera, seatCardAnchors, communityCardAnchors))
        {
            return;
        }

        ResolveCardsFromChildren();

        int seatAnchorCount = seatCardAnchors != null ? seatCardAnchors.Count : 0;
        for (int i = 0; i < seatCards.Length && i < seatAnchorCount; i++)
        {
            ApplySlotFromAnchor(seatCards, i, seatCardAnchors[i], canvas, camera);
        }

        int communityAnchorCount = communityCardAnchors != null ? communityCardAnchors.Count : 0;
        for (int i = 0; i < communityCards.Length && i < communityAnchorCount; i++)
        {
            ApplySlotFromAnchor(communityCards, i, communityCardAnchors[i], canvas, camera);
        }

        layoutDirty = false;
        cachedCanvas = canvas;
        cachedCamera = camera;
        cachedCameraPosition = camera.transform.position;
        cachedCameraRotation = camera.transform.rotation;
        cachedCameraFieldOfView = camera.fieldOfView;
        cachedCameraOrthographicSize = camera.orthographicSize;
        cachedCameraOrthographic = camera.orthographic;
        cachedCanvasScaleFactor = canvas != null ? canvas.scaleFactor : 0f;
        cachedScreenWidth = Screen.width;
        cachedScreenHeight = Screen.height;
    }

    public void ResolveCardsFromChildren()
    {
        EnsureSeatArray();
        AssignSeatCardsByName();
        AssignByName(communityCards, "CommunityCard3D_");
    }

    public bool HasSeatCards()
    {
        ResolveCardsFromChildren();
        for (int i = 0; i < seatCards.Length; i++)
        {
            if (seatCards[i] == null)
            {
                return false;
            }
        }

        return true;
    }

    public float PlaySeatDealAnimation(
        Canvas canvas,
        RectTransform originAnchor,
        IReadOnlyList<PlayerState> players,
        float duration,
        float stagger)
    {
        ResolveCardsFromChildren();

        Vector3 originLocalPosition = ResolveLocalOrigin(canvas, originAnchor);
        int dealIndex = 0;
        for (int cardIndex = 0; cardIndex < HoleCardsPerSeat; cardIndex++)
        {
            if (players == null)
            {
                break;
            }

            for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
            {
                PlayerState player = players[playerIndex];
                if (player == null)
                {
                    continue;
                }

                int slotIndex = SeatCardIndex(player.SeatIndex, cardIndex);
                if (slotIndex < 0 || slotIndex >= seatCards.Length)
                {
                    continue;
                }

                Poker3DCardView view = seatCards[slotIndex];
                if (view == null)
                {
                    continue;
                }

                float delay = dealIndex * Mathf.Max(0f, stagger);
                float spin = DealSpinFor(player.SeatIndex, cardIndex);
                view.PlayDealFromLocal(originLocalPosition, duration, delay, spin, cardHeight * 0.28f);
                dealIndex++;
            }
        }

        return dealIndex == 0 ? 0f : Mathf.Max(0f, duration) + Mathf.Max(0f, stagger) * (dealIndex - 1);
    }

    private void RenderSeatCards(int seatIndex, IReadOnlyList<Card> cards, bool faceUp, bool staggerBySeat)
    {
        for (int cardIndex = 0; cardIndex < HoleCardsPerSeat; cardIndex++)
        {
            int slotIndex = SeatCardIndex(seatIndex, cardIndex);
            Poker3DCardView view = seatCards[slotIndex];
            if (view == null)
            {
                continue;
            }

            if (cards == null || cardIndex >= cards.Count)
            {
                view.Clear();
                seatStates[slotIndex] = default;
                continue;
            }

            Poker3DCardView.FlipStyle flipStyle = staggerBySeat
                ? Poker3DCardView.FlipStyle.DramaticHinge
                : Poker3DCardView.FlipStyle.Normal;
            RenderCard(
                view,
                ref seatStates[slotIndex],
                cards[cardIndex],
                faceUp,
                staggerBySeat ? showdownStagger * seatIndex : playerRevealDelay,
                flipStyle);
        }
    }

    private void RenderCards(
        Poker3DCardView[] views,
        CardSlotState[] states,
        IReadOnlyList<Card> cards,
        bool faceUp,
        float revealDelay)
    {
        for (int i = 0; i < views.Length; i++)
        {
            Poker3DCardView view = views[i];
            if (view == null)
            {
                continue;
            }

            if (cards == null || i >= cards.Count)
            {
                view.Clear();
                states[i] = default;
                continue;
            }

            Poker3DCardView.FlipStyle flipStyle = i == 4
                ? Poker3DCardView.FlipStyle.DramaticHinge
                : Poker3DCardView.FlipStyle.Normal;
            RenderCard(view, ref states[i], cards[i], faceUp, revealDelay, flipStyle);
        }
    }

    private void RenderCard(
        Poker3DCardView view,
        ref CardSlotState state,
        Card card,
        bool faceUp,
        float revealDelay,
        Poker3DCardView.FlipStyle flipStyle)
    {
        bool isNewCard = !state.HasCard || !SameCard(state.Card, card);
        bool needsReveal = faceUp && (isNewCard || !state.FaceUp);

        if (needsReveal)
        {
            view.Reveal(card, animateReveals && Application.isPlaying, revealDelay, flipStyle);
        }
        else if (isNewCard || state.FaceUp != faceUp)
        {
            if (faceUp)
            {
                view.SetCard(card, true);
            }
            else
            {
                view.HideBack(card);
            }
        }

        state.HasCard = true;
        state.Card = card;
        state.FaceUp = faceUp;
    }

    private void ClearCards(Poker3DCardView[] views, CardSlotState[] states)
    {
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] != null)
            {
                views[i].Clear();
            }

            states[i] = default;
        }
    }

    private void EnsureSeatArray()
    {
        if (seatCards == null || seatCards.Length != SeatCount * HoleCardsPerSeat)
        {
            seatCards = new Poker3DCardView[SeatCount * HoleCardsPerSeat];
        }
    }

    private void AssignSeatCardsByName()    // 카드 슬롯 자동 바인딩 함수
    {
        for (int seatIndex = 0; seatIndex < SeatCount; seatIndex++)
        {
            for (int cardIndex = 0; cardIndex < HoleCardsPerSeat; cardIndex++)
            {
                int slotIndex = SeatCardIndex(seatIndex, cardIndex);
                if (seatCards[slotIndex] != null)
                {
                    continue;
                }

                Transform child = transform.Find($"Seat{seatIndex}Card3D_{cardIndex + 1}");
                if (child != null)
                {
                    seatCards[slotIndex] = child.GetComponent<Poker3DCardView>();
                }
            }
        }
    }

    private void AssignByName(Poker3DCardView[] target, string prefix)
    {
        for (int i = 0; i < target.Length; i++)
        {
            if (target[i] != null)
            {
                continue;
            }

            Transform child = transform.Find($"{prefix}{i + 1}");
            if (child != null)
            {
                target[i] = child.GetComponent<Poker3DCardView>();
            }
        }
    }

    private void SetSlot(Poker3DCardView[] views, int index, Vector3 localPosition)
    {
        if (index < 0 || index >= views.Length || views[index] == null)
        {
            return;
        }

        Poker3DCardView view = views[index];
        view.transform.localPosition = localPosition;
        view.transform.localEulerAngles = CardRotation;
        view.transform.localScale = Vector3.one;
        view.SetSize(cardWidth, cardHeight, cardThickness);
    }

    private void ApplySlotFromAnchor(Poker3DCardView[] views, int index, CardView anchor, Canvas canvas, Camera camera)
    {
        if (index < 0 || index >= views.Length || views[index] == null || anchor == null)
        {
            return;
        }

        RectTransform rect = anchor.RectTransform;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 centerScreen = RectTransformUtility.WorldToScreenPoint(canvasCamera, rect.position);
        Vector2 leftScreen = RectTransformUtility.WorldToScreenPoint(canvasCamera, (corners[0] + corners[1]) * 0.5f);
        Vector2 rightScreen = RectTransformUtility.WorldToScreenPoint(canvasCamera, (corners[2] + corners[3]) * 0.5f);
        Vector2 bottomScreen = RectTransformUtility.WorldToScreenPoint(canvasCamera, (corners[0] + corners[3]) * 0.5f);
        Vector2 topScreen = RectTransformUtility.WorldToScreenPoint(canvasCamera, (corners[1] + corners[2]) * 0.5f);

        Vector3 worldCenter = camera.ScreenToWorldPoint(new Vector3(centerScreen.x, centerScreen.y, uiCameraDepth));
        Vector3 worldLeft = camera.ScreenToWorldPoint(new Vector3(leftScreen.x, leftScreen.y, uiCameraDepth));
        Vector3 worldRight = camera.ScreenToWorldPoint(new Vector3(rightScreen.x, rightScreen.y, uiCameraDepth));
        Vector3 worldBottom = camera.ScreenToWorldPoint(new Vector3(bottomScreen.x, bottomScreen.y, uiCameraDepth));
        Vector3 worldTop = camera.ScreenToWorldPoint(new Vector3(topScreen.x, topScreen.y, uiCameraDepth));

        Poker3DCardView view = views[index];
        view.transform.position = worldCenter;
        view.transform.rotation = Quaternion.identity;
        view.transform.localScale = Vector3.one;
        view.SetSize(Vector3.Distance(worldLeft, worldRight), Vector3.Distance(worldBottom, worldTop), cardThickness);
    }

    private bool ShouldApplyAnchorLayout(
        Canvas canvas,
        Camera camera,
        IReadOnlyList<CardView> seatCardAnchors,
        IReadOnlyList<CardView> communityCardAnchors)
    {
        bool dirty = layoutDirty
            || cachedCanvas != canvas
            || cachedCamera != camera
            || cachedCameraPosition != camera.transform.position
            || cachedCameraRotation != camera.transform.rotation
            || !Mathf.Approximately(cachedCameraFieldOfView, camera.fieldOfView)
            || !Mathf.Approximately(cachedCameraOrthographicSize, camera.orthographicSize)
            || cachedCameraOrthographic != camera.orthographic
            || !Mathf.Approximately(cachedCanvasScaleFactor, canvas != null ? canvas.scaleFactor : 0f)
            || cachedScreenWidth != Screen.width
            || cachedScreenHeight != Screen.height;

        dirty |= HasAnchorLayoutChanged(seatCardAnchors, 0, SeatCount * HoleCardsPerSeat);
        dirty |= HasAnchorLayoutChanged(communityCardAnchors, SeatCount * HoleCardsPerSeat, 5);
        return dirty;
    }

    private bool HasAnchorLayoutChanged(IReadOnlyList<CardView> anchors, int cacheOffset, int maxCount)
    {
        bool changed = false;
        for (int i = 0; i < maxCount; i++)
        {
            int cacheIndex = cacheOffset + i;
            RectTransform rect = anchors != null && i < anchors.Count && anchors[i] != null
                ? anchors[i].RectTransform
                : null;
            Vector3 position = rect != null ? rect.position : Vector3.zero;
            Vector2 size = rect != null ? rect.rect.size : Vector2.zero;

            if (cachedAnchorPositions[cacheIndex] != position || cachedAnchorSizes[cacheIndex] != size)
            {
                cachedAnchorPositions[cacheIndex] = position;
                cachedAnchorSizes[cacheIndex] = size;
                changed = true;
            }
        }

        return changed;
    }

    private static Camera ResolveCamera(Canvas canvas)
    {
        if (canvas != null && canvas.worldCamera != null)
        {
            return canvas.worldCamera;
        }

        return Camera.main;
    }

    // UI 사각 트랜스폼 앵커, 보통 덱 위치를 이 3D 카드 루트의 로컬 좌표로 변환합니다.
    // 3D 카드 딜 애니메이션은 이 값을 시작점으로 사용해서 2D UI 덱에서 카드가 날아오는 것처럼 보이게 합니다.
    private Vector3 ResolveLocalOrigin(Canvas canvas, RectTransform originAnchor)
    {
        if (canvas == null || originAnchor == null)
        {
            return Vector3.zero;
        }

        Camera camera = ResolveCamera(canvas);
        if (camera == null)
        {
            return Vector3.zero;
        }

        // 먼저 UI 앵커의 화면 좌표를 구합니다.
        // 화면 공간 오버레이 캔버스는 캔버스 카메라가 비어 있어야 하고,
        // 카메라/월드 공간 캔버스는 해당 캔버스 카메라가 필요합니다.
        Camera canvasCamera = canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, originAnchor.position);

        // 화면 좌표를 다시 게임 카메라 기준 월드 좌표로 투영합니다.
        // 직교 카메라에서는 깊이값이 주로 카메라 앞쪽 평면 위치만 고르고,
        // 원근 카메라에서는 투영된 월드 위치와 크기에도 영향을 줍니다.
        Vector3 worldPoint = camera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, uiCameraDepth));

        // 카드 뷰들은 3D 카드 테이블 뷰 아래의 로컬 좌표로 움직이므로,
        // 월드 좌표로 구한 덱 위치를 이 루트의 로컬 좌표계로 바꿔 반환합니다.
        return transform.InverseTransformPoint(worldPoint);
    }

    private static float DealSpinFor(int seatIndex, int cardIndex)
    {
        float direction = seatIndex % 2 == 0 ? -1f : 1f;    //짝수 - 왼쪽 회전, 홀수 - 오른쪽 회전
        return direction * (18f + cardIndex * 8f);
    }

    private float GetLongestFlipDuration()
    {
        float duration = 0f;
        AddLongestDuration(seatCards, ref duration);
        AddLongestDuration(communityCards, ref duration);
        return duration;
    }

    private static void AddLongestDuration(Poker3DCardView[] views, ref float duration)
    {
        foreach (Poker3DCardView view in views)
        {
            if (view != null)
            {
                duration = Mathf.Max(duration, view.MaxFlipDuration);
            }
        }
    }

    private static int SeatCardIndex(int seatIndex, int cardIndex)
    {
        return seatIndex * HoleCardsPerSeat + cardIndex;
    }

    private static bool SameCard(Card a, Card b)
    {
        return a.Suit == b.Suit && a.Rank == b.Rank;
    }
}
