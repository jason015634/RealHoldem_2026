using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class PokerUIManager : MonoBehaviour
{
    [System.Serializable]
    internal sealed class SeatUi
    {
        public RectTransform Root;
        public Text NameText;
        public Text ChipText;
        public Text BetText;
        public Text ActionText;
        public Image ActionImage;
        public Text StatusText;
        public RectTransform UserImage;
        [System.NonSerialized] public Image UserImageBackground;
        [System.NonSerialized] public Image UserImageGraphic;
        public Image UserTimeClock;
        public RectTransform BetAnchor;
        public Vector2 EnterOffset = new Vector2(0f, -48f);
        public Vector2 ExitOffset = new Vector2(0f, 48f);
        public List<CardView> Cards = new List<CardView>(2);
        [System.NonSerialized] public CanvasGroup Group;
        [System.NonSerialized] public Sequence SeatSequence;
        [System.NonSerialized] public Vector2 HomePosition;
        [System.NonSerialized] public bool HasHomePosition;
        [System.NonSerialized] public bool HasRendered;
        [System.NonSerialized] public string LastPlayerId;
    }

    public const string CanvasName = "PokerDemoCanvas";
    private const int SeatCount = 6;

    [Header("Runtime UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Text potText;
    [SerializeField] private Text stateText;
    [SerializeField] private Text resultText;
    [SerializeField] private Text betAmountText;
    [SerializeField] private Slider betSlider;
    [SerializeField] private Button foldButton;
    [SerializeField] private Button checkCallButton;
    [SerializeField] private Button betRaiseButton;
    [SerializeField] private Button nextHandButton;
    [SerializeField] private Button addBotButton;
    [SerializeField] private Button removeBotButton;
    [SerializeField] private RectTransform deckAnchor;
    [SerializeField] private List<CardView> communityCards = new List<CardView>(5);

    [Header("3D Cards")]
    [SerializeField] private bool use3DCards = true;
    [SerializeField] private Poker3DCardTableView card3DTable;

    [Header("Seat UI")]
    [SerializeField] private List<SeatUi> seatUis = new List<SeatUi>(SeatCount);

    [Header("Action Images")]
    [SerializeField] private Sprite allInActionSprite;
    [SerializeField] private Sprite betActionSprite;
    [SerializeField] private Sprite callActionSprite;
    [SerializeField] private Sprite checkActionSprite;
    [SerializeField] private Sprite foldActionSprite;
    [SerializeField] private float actionPopStartScale = 0.6f;
    [SerializeField] private float actionPopOvershootScale = 1.15f;
    [SerializeField] private float actionPopInDuration = 0.14f;
    [SerializeField] private float actionSettleDuration = 0.12f;
    [SerializeField] private float actionHideDuration = 0.16f;
    [SerializeField] private float actionHideScale = 0.72f;

    [Header("Seat Animation")]
    [SerializeField] private float seatEnterDuration = 0.38f;
    [SerializeField] private float seatExitDuration = 0.28f;

    [Header("Character Images")]
    [SerializeField] private string characterResourcesPath = "Sprites/Characters";
    [SerializeField] private Color normalCharacterTint = Color.white;
    [SerializeField] private Color normalCharacterBackgroundTint = new Color(1f, 0.893f, 0f, 1f);
    [SerializeField] private Color foldedCharacterTint = new Color(0.42f, 0.42f, 0.42f, 0.72f);
    [SerializeField] private Color foldedCharacterBackgroundTint = new Color(0.2f, 0.2f, 0.2f, 0.82f);

    private Text checkCallButtonText;
    private Text betRaiseButtonText;
    private Text nextHandButtonText;
    private Text addBotButtonText;
    private Text removeBotButtonText;
    private bool canvasConfiguredFor3DCards;
    private bool hasResolvedSceneUi;
    private bool missingSceneUiLogged;
    private readonly PokerActionImageView actionImageView = new PokerActionImageView();
    private readonly PokerActionButtonView actionButtonView = new PokerActionButtonView();
    private readonly PokerCardUIRenderer cardRenderer = new PokerCardUIRenderer();
    private readonly PokerSeatUIRenderer seatRenderer = new PokerSeatUIRenderer();

    public bool IsUsing3DCards
    {
        get
        {
            ConfigureCardRenderer();
            bool renderWith3DCards = cardRenderer.ShouldRenderWith3DCards();
            SyncCardRendererState();
            return renderWith3DCards;
        }
    }

    public float Card3DRevealWaitDuration
    {
        get
        {
            ConfigureCardRenderer();
            bool renderWith3DCards = cardRenderer.ShouldRenderWith3DCards();
            SyncCardRendererState();
            Poker3DCardTableView table = cardRenderer.Card3DTable;
            return renderWith3DCards && table != null ? table.RevealWaitDuration : 0f;
        }
    }

    public bool HasPrebuiltSceneUi()
    {
        return canvas != null
            && potText != null
            && stateText != null
            && resultText != null
            && betAmountText != null
            && betSlider != null
            && foldButton != null
            && checkCallButton != null
            && betRaiseButton != null
            && nextHandButton != null
            && addBotButton != null
            && removeBotButton != null
            && deckAnchor != null
            && communityCards.Count == 5
            && seatUis.Count == SeatCount
            && seatUis.TrueForAll(seat => seat.Root != null
                && seat.NameText != null
                && seat.ChipText != null
                && seat.Cards != null
                && seat.Cards.Count == 2);
    }

    public void Bind(PokerGameManager game)
    {
        if (!EnsureBuilt() || game == null)
        {
            return;
        }

        foldButton.onClick.RemoveAllListeners();
        checkCallButton.onClick.RemoveAllListeners();
        betRaiseButton.onClick.RemoveAllListeners();
        nextHandButton.onClick.RemoveAllListeners();
        addBotButton.onClick.RemoveAllListeners();
        removeBotButton.onClick.RemoveAllListeners();
        betSlider.onValueChanged.RemoveAllListeners();

        foldButton.onClick.AddListener(game.OnPlayerFold);
        checkCallButton.onClick.AddListener(game.OnPlayerCheckOrCall);
        betRaiseButton.onClick.AddListener(() => game.OnPlayerBetOrRaise(GetSelectedBetTarget()));
        nextHandButton.onClick.AddListener(game.StartNewHand);
        //addBotButton.onClick.AddListener(game.AddBot);
        //removeBotButton.onClick.AddListener(game.RemoveBot);
        betSlider.onValueChanged.AddListener(value => UpdateBetAmountLabel(Mathf.RoundToInt(value)));
    }

    public int GetSelectedBetTarget()
    {
        return betSlider != null ? Mathf.RoundToInt(betSlider.value) : 0;
    }

    public void SetTurnTimer(PlayerState actor, float normalizedTime, bool visible)
    {
        if (!EnsureBuilt())
        {
            return;
        }

        ConfigureSeatRenderer();
        seatRenderer.RenderTurnTimer(seatUis, actor, normalizedTime, visible);
    }

    public void HideTurnTimers()
    {
        if (!EnsureBuilt())
        {
            return;
        }

        ConfigureSeatRenderer();
        seatRenderer.RenderTurnTimer(seatUis, null, 0f, false);
    }

    public float PlayHoleCardDealAnimation(IReadOnlyList<PlayerState> players, float duration, float stagger)
    {
        ConfigureCardRenderer();
        float animationDuration = cardRenderer.PlayHoleCardDealAnimation(players, duration, stagger);
        SyncCardRendererState();
        return animationDuration;
    }

    public float PlaySeatExitAnimation(int seatIndex)
    {
        if (!EnsureBuilt() || seatIndex < 0 || seatIndex >= seatUis.Count)
        {
            return 0f;
        }

        ConfigureSeatRenderer();
        return seatRenderer.PlaySeatExitAnimation(seatUis[seatIndex]);
    }

    public void RenderTableSnapshot(PokerGameManager game, bool revealAllHoleCards, bool awaitingPlayerAction, bool canStartNextHand)
    {
        if (!EnsureBuilt() || game == null)
        {
            return;
        }

        RenderPot(game);
        RenderStatus(game);
        RenderCardState(game, revealAllHoleCards);
        RenderAllSeats(game, revealAllHoleCards);
        RenderCurrentActor(game, awaitingPlayerAction, canStartNextHand);
    }

    public void RenderStatus(PokerGameManager game)
    {
        if (!EnsureBuilt() || game == null)
        {
            return;
        }

        stateText.text = game.State.ToString();
        resultText.text = game.StatusMessage;
    }

    public void RenderPot(PokerGameManager game)
    {
        if (!EnsureBuilt() || game == null || game.Betting == null)
        {
            return;
        }

        potText.text = $"POT {game.Betting.Pot}";
    }

    public void RenderCardState(PokerGameManager game, bool revealAllHoleCards)
    {
        if (!EnsureBuilt() || game == null)
        {
            return;
        }

        ConfigureCardRenderer();
        cardRenderer.RenderCardState(game, revealAllHoleCards);
        SyncCardRendererState();
    }

    public void RenderAllSeats(PokerGameManager game, bool revealAllHoleCards)
    {
        if (!EnsureBuilt() || game == null)
        {
            return;
        }

        PlayerState actingPlayer = game.CurrentActor;
        for (int i = 0; i < game.Seats.Count && i < seatUis.Count; i++)
        {
            RenderSeat(game, i, actingPlayer, revealAllHoleCards);
        }
    }

    public void RenderSeat(PokerGameManager game, int seatIndex, bool revealAllHoleCards)
    {
        if (!EnsureBuilt() || game == null)
        {
            return;
        }

        RenderSeat(game, seatIndex, game.CurrentActor, revealAllHoleCards);
    }

    public void RenderCurrentActor(PokerGameManager game, bool awaitingPlayerAction, bool canStartNextHand)
    {
        if (!EnsureBuilt() || game == null)
        {
            return;
        }

        ConfigureSeatRenderer();
        seatRenderer.RenderTurnTimer(seatUis, game.CurrentActor, game.ActionTimerNormalized, game.IsActionTimerRunning);
        RenderActionButtons(game, awaitingPlayerAction, canStartNextHand);
    }

    public void RenderActionButtons(PokerGameManager game, bool awaitingPlayerAction, bool canStartNextHand)
    {
        if (!EnsureBuilt() || game == null || game.Betting == null)
        {
            return;
        }

        ConfigureActionButtonView();
        actionButtonView.Render(game, awaitingPlayerAction, canStartNextHand);
    }

    public void HideActionImage(int seatIndex, string hiddenActionText, bool animate)
    {
        if (!EnsureBuilt() || seatIndex < 0 || seatIndex >= seatUis.Count)
        {
            return;
        }

        ConfigureActionImageView();
        SeatUi seat = seatUis[seatIndex];
        actionImageView.Hide(seat.ActionImage, seat.ActionText, animate, hiddenActionText, suppressCurrentAction: true);
    }

    public void HideAllActionImages(bool animate)
    {
        if (!EnsureBuilt())
        {
            return;
        }

        ConfigureActionImageView();
        foreach (SeatUi seat in seatUis)
        {
            actionImageView.Hide(seat.ActionImage, seat.ActionText, animate, null, suppressCurrentAction: true);
        }
    }

    public void ClearHiddenActionImageState(int seatIndex)
    {
        if (!EnsureBuilt() || seatIndex < 0 || seatIndex >= seatUis.Count)
        {
            return;
        }

        ConfigureActionImageView();
        actionImageView.ClearHiddenState(seatUis[seatIndex].ActionImage);
    }

    public void ClearAllCards()
    {
        ResetFoldedCharacterVisuals();
        ConfigureCardRenderer();
        cardRenderer.ClearAllCards();
        SyncCardRendererState();
    }

    public void ResetFoldedCharacterVisuals()
    {
        ConfigureSeatRenderer();
        seatRenderer.ResetFoldedCharacterVisuals(seatUis);
    }

    private void RenderSeat(PokerGameManager game, int seatIndex, PlayerState actingPlayer, bool revealAllHoleCards)
    {
        if (game == null || seatIndex < 0 || seatIndex >= game.Seats.Count || seatIndex >= seatUis.Count)
        {
            return;
        }

        ConfigureSeatRenderer();
        ConfigureActionImageView();
        PlayerState seatPlayer = game.Seats[seatIndex]?.Player;
        seatRenderer.RenderSeat(
            seatUis[seatIndex],
            game.Seats[seatIndex],
            actingPlayer,
            game.CommunityCards,
            game.IsWaitingForNextHand(seatPlayer),
            actionImageView,
            revealAllHoleCards);
    }

    private void ConfigureActionImageView()
    {
        actionImageView.Configure(
            allInActionSprite,
            betActionSprite,
            callActionSprite,
            checkActionSprite,
            foldActionSprite,
            actionPopStartScale,
            actionPopOvershootScale,
            actionPopInDuration,
            actionSettleDuration,
            actionHideDuration,
            actionHideScale);
    }

    private void ConfigureActionButtonView()
    {
        actionButtonView.Configure(
            foldButton,
            checkCallButton,
            betRaiseButton,
            nextHandButton,
            addBotButton,
            removeBotButton,
            betSlider,
            betAmountText,
            checkCallButtonText,
            betRaiseButtonText,
            nextHandButtonText,
            addBotButtonText,
            removeBotButtonText);
    }

    private void ConfigureCardRenderer()
    {
        cardRenderer.Configure(
            canvas,
            deckAnchor,
            seatUis,
            communityCards,
            card3DTable,
            use3DCards,
            canvasConfiguredFor3DCards);
    }

    private void SyncCardRendererState()
    {
        canvasConfiguredFor3DCards = cardRenderer.CanvasConfiguredFor3DCards;
        card3DTable = cardRenderer.Card3DTable;
    }

    private void ConfigureSeatRenderer()
    {
        seatRenderer.Configure(
            characterResourcesPath,
            normalCharacterTint,
            normalCharacterBackgroundTint,
            foldedCharacterTint,
            foldedCharacterBackgroundTint,
            seatEnterDuration,
            seatExitDuration);
    }

    private void UpdateBetAmountLabel(int value)
    {
        ConfigureActionButtonView();
        actionButtonView.UpdateBetAmountLabel(value);
    }

    private bool EnsureBuilt()
    {
        if (!hasResolvedSceneUi)
        {
            ResolveCanvasReference();
            ResolveReferencesFromHierarchy();
            ResolveRuntimeReferences();
            hasResolvedSceneUi = true;
        }

        if (HasPrebuiltSceneUi())
        {
            return true;
        }

        if (!missingSceneUiLogged)
        {
            Debug.LogError($"[PokerUIManager] Required scene UI is missing. Add a prebuilt {CanvasName} hierarchy to the scene.");
            missingSceneUiLogged = true;
        }

        return false;
    }

    private void ResolveCanvasReference()
    {
        if (canvas != null)
        {
            return;
        }

        GameObject existingCanvas = GameObject.Find(CanvasName);
        if (existingCanvas != null)
        {
            existingCanvas.TryGetComponent(out canvas);
        }
    }

    private void ResolveRuntimeReferences()
    {
        checkCallButtonText = GetButtonText(checkCallButton);
        betRaiseButtonText = GetButtonText(betRaiseButton);
        nextHandButtonText = GetButtonText(nextHandButton);
        addBotButtonText = GetButtonText(addBotButton);
        removeBotButtonText = GetButtonText(removeBotButton);
    }

    private static Text GetButtonText(Button button)
    {
        return button != null ? button.GetComponentInChildren<Text>(true) : null;
    }

    private void ResolveReferencesFromHierarchy()
    {
        if (canvas == null)
        {
            return;
        }

        Transform tablePanel = canvas.transform.Find("TablePanel");
        Transform actionPanel = canvas.transform.Find("ActionPanel");

        stateText = FindText(tablePanel, "StateText");
        potText = FindText(tablePanel, "PotText");
        resultText = FindText(tablePanel, "ResultText");
        deckAnchor = FindComponent<RectTransform>(tablePanel, "DeckAnchor");

        communityCards.Clear();
        for (int i = 1; i <= 5; i++)
        {
            communityCards.Add(FindComponent<CardView>(tablePanel, $"CommunityCard{i}"));
        }

        seatUis.Clear();
        for (int i = 0; i < SeatCount; i++)
        {
            Transform seatRoot = tablePanel != null ? tablePanel.Find($"Seat{i}") : null;
            seatUis.Add(ResolveSeatUi(seatRoot));
        }

        betAmountText = FindText(actionPanel, "BetAmountText");
        betSlider = FindComponent<Slider>(actionPanel, "BetSlider");
        foldButton = FindComponent<Button>(actionPanel, "FoldButton");
        checkCallButton = FindComponent<Button>(actionPanel, "CheckCallButton");
        betRaiseButton = FindComponent<Button>(actionPanel, "BetRaiseButton");
        nextHandButton = FindComponent<Button>(actionPanel, "NextHandButton");
        addBotButton = FindComponent<Button>(actionPanel, "AddBotButton");
        removeBotButton = FindComponent<Button>(actionPanel, "RemoveBotButton");

        communityCards.RemoveAll(card => card == null);
    }

    private SeatUi ResolveSeatUi(Transform root)
    {
        SeatUi seat = new SeatUi();
        if (root == null)
        {
            return seat;
        }

        seat.Root = root as RectTransform;
        seat.NameText = FindText(root, "Name");
        seat.ChipText = FindText(root, "Chips");
        seat.BetText = FindText(root, "Bet");
        seat.ActionText = FindText(root, "Action");
        seat.ActionImage = FindComponent<Image>(root, "ActionImage");
        seat.StatusText = FindText(root, "Status");
        seat.UserImage = FindComponent<RectTransform>(root, "UserImage");
        seat.UserImageBackground = seat.UserImage != null ? seat.UserImage.GetComponent<Image>() : null;
        seat.UserImageGraphic = FindUserImageGraphic(seat.UserImage);
        seat.UserTimeClock = FindComponentInChildrenByName<Image>(root, "UserTimeClock");
        seat.BetAnchor = FindComponent<RectTransform>(root, "BetAnchor");
        ConfigureStatusText(seat.StatusText);
        seat.Cards.Clear();
        seat.Cards.Add(FindComponent<CardView>(root, "Card1"));
        seat.Cards.Add(FindComponent<CardView>(root, "Card2"));
        seat.Cards.RemoveAll(card => card == null);
        return seat;
    }

    private static void ConfigureStatusText(Text statusText)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.resizeTextForBestFit = true;
        statusText.resizeTextMinSize = 10;
        statusText.resizeTextMaxSize = statusText.fontSize;
    }

    private static Image FindUserImageGraphic(RectTransform userImageRoot)
    {
        if (userImageRoot == null)
        {
            return null;
        }

        Image namedImage = FindComponent<Image>(userImageRoot, "Image");
        if (namedImage != null)
        {
            return namedImage;
        }

        foreach (Image image in userImageRoot.GetComponentsInChildren<Image>(true))
        {
            if (image.name != "UserTimeClock")
            {
                return image;
            }
        }

        return null;
    }

    private static Text FindText(Transform parent, string childName)
    {
        return FindComponent<Text>(parent, childName);
    }

    private static T FindComponent<T>(Transform parent, string childName) where T : Component
    {
        if (parent == null)
        {
            return null;
        }

        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static T FindComponentInChildrenByName<T>(Transform parent, string childName) where T : Component
    {
        if (parent == null)
        {
            return null;
        }

        Transform directChild = parent.Find(childName);
        if (directChild != null && directChild.TryGetComponent(out T directComponent))
        {
            return directComponent;
        }

        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName && child.TryGetComponent(out T component))
            {
                return component;
            }
        }

        return null;
    }
}
