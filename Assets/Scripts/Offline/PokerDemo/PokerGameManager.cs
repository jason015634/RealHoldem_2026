using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 한 핸드의 진행 상태입니다. 딜, 베팅 라운드, 쇼다운, 핸드 종료 흐름을 구분합니다.
public enum PokerGameState
{
    WaitingToStart,
    DealHoleCards,
    PreFlopBetting,
    DealFlop,
    FlopBetting,
    DealTurn,
    TurnBetting,
    DealRiver,
    RiverBetting,
    Showdown,
    HandEnd
}

// 오프라인 포커 데모의 메인 게임 흐름을 관리합니다.
// 플레이어/봇 착석, 카드 딜, 베팅 라운드, 쇼다운 판정, UI 갱신까지 한 핸드의 생명주기를 담당합니다.
public sealed class PokerGameManager : MonoBehaviour
{
    private const int MaxSeats = 6;

    [Header("Game Settings")]
    [SerializeField] private int startingChips = 1000;
    [SerializeField] private int smallBlind = 10;
    [SerializeField] private int bigBlind = 20;
    [SerializeField] private int maxRaisesPerRound = 2;
    [SerializeField] private float dealDelay = 0.16f;
    [SerializeField] private float holeCardDealDuration = 0.48f;
    [SerializeField] private float holeCardDealStagger = 0.05f;
    [SerializeField] private float aiThinkDelay = 0.65f;
    [SerializeField] private float actionTimeLimit = 7f;
    [SerializeField] private float actionImageRoundEndHoldDelay = 0.35f;
    [SerializeField] private float startNewRoundDelay = 0.35f;

    [Header("Rule Debug")]
    [SerializeField] private bool enableRuleDebugHotkey = true;
    [SerializeField] private KeyCode ruleDebugKey = KeyCode.F9;

    [Header("Auto Bot Join")]
    [Tooltip("시작 버튼을 누른 뒤 각 봇이 입장하기까지의 랜덤 지연 범위입니다.")]
    [SerializeField] private Vector2 botJoinDelayRange = new Vector2(0.5f, 4f);
    [Tooltip("추후 테이블 연출에 맞게 조정합니다. 예약된 좌석 사이에 더해 봇이 순서대로 입장하게 합니다.")]
    [SerializeField] private float botJoinSeatOffset = 0.35f;
    [Tooltip("칩을 잃고 나간 봇 좌석에 대체 봇이 들어오기 전까지의 랜덤 지연 범위입니다.")]
    [SerializeField] private Vector2 botRejoinDelayRange = new Vector2(2f, 8f);
    [Tooltip("추후 대체 봇 스택을 더 촘촘하게 맞춰야 하면 조정합니다.")]
    [SerializeField] private Vector2Int botStartingChipsRange = new Vector2Int(800, 1400);

    [Header("Scene References")]
    [SerializeField] private PokerUIManager ui;
    [SerializeField] private BettingManager betting;
    [SerializeField] private HumanLikePokerAI ai;
    [SerializeField] private PokerBetChipAnimator betChipAnimator;

    private readonly PokerTableState table = new PokerTableState(MaxSeats);
    private readonly List<PlayerState> handPlayers = new List<PlayerState>(MaxSeats);   // 현재 핸드에 참여 중인 플레이어
    private readonly List<PlayerState> pendingActors = new List<PlayerState>(MaxSeats); // 이번 베팅 라운드에서 아직 액션이 남은 플레이어
    private readonly List<Card> communityCards = new List<Card>(5);
    private Deck deck;
    private PlayerState localPlayer;
    private PlayerState currentActor;
    private IPokerAI pokerAi;
    private int botSerial;
    private int dealerSeatIndex;
    private int raiseCount;
    private bool awaitingPlayerAction;
    private bool revealAllHoleCards;
    private bool resolvingBettingRound;
    private bool resolvingFoldWin;
    private bool autoStartRequested;
    private PokerTurnTimer turnTimer;
    private string statusMessage = "Tap Start Hand";
    private readonly HashSet<int> pendingBotJoinSeatIndices = new HashSet<int>();
    private readonly HashSet<int> removingBotSeatIndices = new HashSet<int>();
    private readonly List<int> bustedBotSeatIndices = new List<int>();
    private readonly StringBuilder showdownSummaryBuilder = new StringBuilder(96);

    public PokerGameState State { get; private set; } = PokerGameState.WaitingToStart;
    public PlayerState Player => localPlayer;
    public PlayerState Opponent => FindOpponent();
    public PlayerState CurrentActor => currentActor;
    public IReadOnlyList<PokerSeat> Seats => table.Seats;
    public IReadOnlyList<PlayerState> HandPlayers => handPlayers;
    public IReadOnlyList<Card> CommunityCards => communityCards;
    public BettingManager Betting => betting;
    public string StatusMessage => statusMessage;
    public bool IsActionTimerRunning => turnTimer != null && turnTimer.IsRunning && currentActor != null && IsBettingState(State);
    public float ActionTimerNormalized => turnTimer != null ? turnTimer.NormalizedTime : 0f;    // 액션 타이머의 남은 비율입니다. 1은 시간이 가득 남은 상태, 0은 시간이 끝난 상태입니다.
    public bool CanEditSeats => State == PokerGameState.WaitingToStart || State == PokerGameState.HandEnd;
    public bool CanAddBot => false;
    public bool CanRemoveBot => false;
    public bool IsHandInProgress => State != PokerGameState.WaitingToStart && State != PokerGameState.HandEnd;
    public bool CanStartNewHand => CanEditSeats
        && !autoStartRequested
        && removingBotSeatIndices.Count == 0
        && (CountPlayablePlayersInSeatOrder() >= 2 || table.EmptySeatCount > 0);

    private void Awake()
    {
        Screen.orientation = ScreenOrientation.Portrait;

        betting = betting != null ? betting : GetComponent<BettingManager>();
        if (betting == null)
        {
            betting = gameObject.AddComponent<BettingManager>();
        }

        ai = ai != null ? ai : GetComponent<HumanLikePokerAI>();
        if (ai == null)
        {
            ai = gameObject.AddComponent<HumanLikePokerAI>();
        }

        pokerAi = ai;

        ui = ui != null ? ui : GetComponent<PokerUIManager>();
        if (ui == null)
        {
            ui = gameObject.AddComponent<PokerUIManager>();
        }

        betChipAnimator = betChipAnimator != null ? betChipAnimator : GetComponent<PokerBetChipAnimator>();
        if (betChipAnimator == null)
        {
            betChipAnimator = gameObject.AddComponent<PokerBetChipAnimator>();
        }

        betChipAnimator.Bind(ui, betting);
        turnTimer = new PokerTurnTimer(this, ui, actionTimeLimit, IsActiveTimedActor, HandleActionTimeout);
        CreateLocalPlayer();
        deck = new Deck();
        SyncBettingPlayers();
        ui.Bind(this);
    }

    private void Start()
    {
        if(LobbyRoomSelection.SelectedRoom != null)
        {
            smallBlind = LobbyRoomSelection.SelectedRoom.SmallBlind;
            bigBlind = LobbyRoomSelection.SelectedRoom.BigBlind;
        }

        SetState(PokerGameState.WaitingToStart, "Press Start to invite bots.");
        RenderFullTable();

        StartNewHand();
    }

    private void Update()
    {
        if (enableRuleDebugHotkey && Input.GetKeyDown(ruleDebugKey))
        {
            RunRuleDebugScenarios();
        }
    }

    [ContextMenu("Run Rule Debug Scenarios")]
    public void RunRuleDebugScenarios()
    {
        try
        {
            string result = PokerRuleDebugRunner.RunAll();
            statusMessage = result;
            Debug.Log($"[RuleDebug] {result}");
        }
        catch (System.Exception exception)
        {
            statusMessage = $"Rule debug failed: {exception.Message}";
            Debug.LogError($"[RuleDebug] {exception}");
        }

        RenderStatusOnly();
    }

    //public void AddBot()
    //{
    //    if (!CanAddBot)
    //    {
    //        return;
    //    }

    //    botSerial++;
    //    PlayerState bot = new PlayerState($"bot-{botSerial}", $"Bot {botSerial}", startingChips, false, -1);
    //    if (table.TrySitFirstEmpty(bot, out PokerSeat seat))
    //    {
    //        Debug.Log($"[Seat] Added {bot.Nickname} to seat {seat.SeatIndex}.");
    //        SyncBettingPlayers();
    //        RenderSeatState(seat.SeatIndex);
    //    }
    //}

    //public void RemoveBot()
    //{
    //    if (!CanRemoveBot)
    //    {
    //        return;
    //    }

    //    table.TryGetLastBotSeat(out PokerSeat botSeat);
    //    int removedSeatIndex = botSeat != null ? botSeat.SeatIndex : -1;
    //    PlayerState removed = table.RemoveLastBot();
    //    if (removed != null)
    //    {
    //        Debug.Log($"[Seat] Removed {removed.Nickname} from seat.");
    //        SyncBettingPlayers();
    //        RenderSeatState(removedSeatIndex);
    //    }
    //}

    public void StartNewHand()
    {
        // 새 핸드를 시작하기 전에 자동 시작이 가능한 상태인지 체크합니다. 자동 시작이 이미 예약되어 있거나, 봇이 퇴장 중이거나, 현재 플레이어 구성이 새 핸드를 시작하기에 적합하지 않으면 요청을 무시합니다.
        if (State != PokerGameState.WaitingToStart && State != PokerGameState.HandEnd)
        {
            Debug.Log("시작 못함");
            return;
        }

        // 자동 시작이 이미 예약되어 있거나, 봇이 퇴장 중이거나, 현재 플레이어 구성이 새 핸드를 시작하기에 적합하지 않으면 요청을 무시합니다.
        if (autoStartRequested)
        {
            return;
        }

        // 봇이 퇴장 중인 경우, 새 핸드 시작 요청을 무시합니다. 봇이 퇴장하는 동안 플레이어 구성이 계속 바뀌기 때문에, 새 핸드 시작 시점에 플레이어 구성이 적합한지 체크하기 어렵습니다. 봇 퇴장이 완료되어 안정된 플레이어 구성이 될 때까지 기다렸다가 새 핸드를 시작하도록 합니다.
        if (removingBotSeatIndices.Count > 0)
        {
            return;
        }

        EnsurePlayableStacks(); // 칩 제공 함수
        autoStartRequested = true;
        ScheduleAutoBotJoinsForEmptySeats();
        TryStartRequestedHand();

        if (autoStartRequested)
        {
            SetState(State, "Waiting for bots to join...");
            RenderStatusAndActions();
        }

        if (CountPlayablePlayersInSeatOrder() < 2 && table.EmptySeatCount == 0)
        {
            autoStartRequested = false;
            SetState(State, "Need at least 2 players with chips.");
            RenderStatusAndActions();
        }
    }

    public bool CanJoinCurrentHand(PlayerState player)
    {
        return player != null && handPlayers.Contains(player);
    }

    public bool IsWaitingForNextHand(PlayerState player)
    {
        return player != null && IsHandInProgress && !CanJoinCurrentHand(player);
    }

    private void ScheduleAutoBotJoinsForEmptySeats()
    {
        IReadOnlyList<int> emptySeats = table.EmptySeatIndicesInSeatOrder();
        float cumulativeDelay = 0f;     // 누적 딜레이
        for (int i = 0; i < emptySeats.Count; i++)
        {
            int seatIndex = emptySeats[i];
            if (pendingBotJoinSeatIndices.Contains(seatIndex))
            {
                continue;
            }

            pendingBotJoinSeatIndices.Add(seatIndex);
            float minDelay = Mathf.Min(botJoinDelayRange.x, botJoinDelayRange.y);
            float maxDelay = Mathf.Max(botJoinDelayRange.x, botJoinDelayRange.y);
            float randomDelay = Random.Range(minDelay, maxDelay);
            cumulativeDelay += Mathf.Max(0f, randomDelay) + Mathf.Max(0f, botJoinSeatOffset) * i;
            StartCoroutine(DelayedBotJoinRoutine(seatIndex, cumulativeDelay, allowFallbackSeat: true)); // 각 자리마다 delay만큼의 시간이 흐른 뒤 bot 착석하게 하는 코루틴 시작
        }
    }

    private IEnumerator DelayedBotJoinRoutine(int preferredSeatIndex, float delay, bool allowFallbackSeat)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        pendingBotJoinSeatIndices.Remove(preferredSeatIndex);

        PlayerState bot = CreateBot();
        if (!table.TrySitAtSeat(preferredSeatIndex, bot, out PokerSeat seat)
            && (!allowFallbackSeat || !table.TrySitFirstEmpty(bot, out seat)))
        {
            yield break;
        }

        Debug.Log($"[Seat] {bot.Nickname} joined seat {seat.SeatIndex}.");
        if (!IsHandInProgress)
        {
            SyncBettingPlayers();
        }

        RenderSeatState(seat.SeatIndex);
        TryStartRequestedHand();
    }

    private PlayerState CreateBot()
    {
        botSerial++;
        return new PlayerState($"bot-{botSerial}", $"Bot {botSerial}", RandomBotStartingChips(), false, -1);
    }

    private int RandomBotStartingChips()
    {
        int min = Mathf.Min(botStartingChipsRange.x, botStartingChipsRange.y);
        int max = Mathf.Max(botStartingChipsRange.x, botStartingChipsRange.y);
        if (max <= 0)
        {
            return startingChips;
        }

        min = Mathf.Max(1, min);
        return Random.Range(min, max + 1);
    }

    private void TryStartRequestedHand()
    {
        if (!autoStartRequested || !CanEditSeats)
        {
            return;
        }

        if (CountPlayablePlayersInSeatOrder() < 2)
        {
            return;
        }

        autoStartRequested = false;
        StopActionTimer();
        StartCoroutine(StartHandRoutine());
    }

    private void ProcessBustedBotsAfterHand()
    {
        bustedBotSeatIndices.Clear();
        for (int i = 0; i < table.Seats.Count; i++)
        {
            PokerSeat seat = table.Seats[i];
            if (seat != null
                && seat.HasBot
                && seat.Player.Chips <= 0
                && !removingBotSeatIndices.Contains(seat.SeatIndex))
            {
                bustedBotSeatIndices.Add(seat.SeatIndex);
            }
        }

        for (int i = 0; i < bustedBotSeatIndices.Count; i++)
        {
            StartCoroutine(RemoveAndRejoinBotRoutine(bustedBotSeatIndices[i]));
        }
    }

    private IEnumerator RemoveAndRejoinBotRoutine(int seatIndex)
    {
        if (removingBotSeatIndices.Contains(seatIndex))
        {
            yield break;
        }

        PokerSeat seat = table.GetSeat(seatIndex);
        PlayerState leavingBot = seat?.Player;
        if (leavingBot == null || !leavingBot.IsBot || leavingBot.Chips > 0)
        {
            yield break;
        }

        removingBotSeatIndices.Add(seatIndex);
        Debug.Log($"[Seat] {leavingBot.Nickname} busted at seat {seatIndex}. Leaving table.");

        float exitDuration = ui != null ? ui.PlaySeatExitAnimation(seatIndex) : 0f;
        if (exitDuration > 0f)
        {
            yield return new WaitForSeconds(exitDuration);
        }

        if (table.GetSeat(seatIndex)?.Player == leavingBot)
        {
            PlayerState removed = table.RemoveBotAtSeat(seatIndex);
            if (removed != null)
            {
                handPlayers.Remove(removed);
                pendingActors.Remove(removed);
            }
        }

        removingBotSeatIndices.Remove(seatIndex);
        SyncBettingPlayers();
        RenderSeatState(seatIndex);

        if (!pendingBotJoinSeatIndices.Add(seatIndex))
        {
            yield break;
        }

        float rejoinDelay = Random.Range(
            Mathf.Min(botRejoinDelayRange.x, botRejoinDelayRange.y),
            Mathf.Max(botRejoinDelayRange.x, botRejoinDelayRange.y));
        StartCoroutine(DelayedBotJoinRoutine(seatIndex, Mathf.Max(0f, rejoinDelay), allowFallbackSeat: false));
    }

    public void OnPlayerFold()
    {
        if (!CanPlayerAct())
        {
            return;
        }

        betting.ApplyFold(currentActor);
        CompleteCurrentAction(bettingReopened: false);
    }

    public void OnPlayerCheckOrCall()
    {
        if (!CanPlayerAct())
        {
            return;
        }

        int callAmount = betting.CallAmountFor(currentActor);
        if (callAmount <= 0)
        {
            betting.ApplyCheck(currentActor);
        }
        else
        {
            betting.ApplyCall(currentActor);
        }

        CompleteCurrentAction(bettingReopened: false);
    }

    public void OnPlayerBetOrRaise(int totalBetTarget)
    {
        if (!CanPlayerAct())
        {
            return;
        }

        betting.ApplyBetOrRaise(currentActor, totalBetTarget);
        CompleteCurrentAction(betting.LastActionWasFullRaise, betting.LastActionIncreasedBet);
    }

    private IEnumerator StartHandRoutine()
    {
        // 새 핸드를 시작하기 전에 착석 상태와 칩 상태를 기준으로 베팅 대상 플레이어를 다시 동기화합니다.
        SyncBettingPlayers();

        if (handPlayers.Count < 2)
        {
            SetState(PokerGameState.WaitingToStart, "Need at least 2 players with chips.");
            RenderStatusAndActions();
            yield break;
        }

        revealAllHoleCards = false;
        awaitingPlayerAction = false;
        currentActor = null;
        communityCards.Clear();
        pendingActors.Clear();
        deck.Reset();
        deck.Shuffle();
        betting.ResetForNewHand();
        betChipAnimator.ClearActiveChips();

        SetState(PokerGameState.DealHoleCards, "Posting blinds...");
        ui.HideAllActionImages(false);
        ui.ClearAllCards();
        RenderFullTable();
        yield return new WaitForSeconds(dealDelay);

        BlindPositionInfo blindInfo = CurrentBlindInfo();
        betting.PostBlinds(blindInfo.SmallBlind, blindInfo.BigBlind);
        RenderBettingStateForSeats(blindInfo.SmallBlind, blindInfo.BigBlind);
        yield return DealHoleCardsRoutine();

        raiseCount = 0;
        BeginBetting(PokerGameState.PreFlopBetting, blindInfo.PreflopFirstActor.SeatIndex, "Pre-Flop betting.");
    }

    private IEnumerator DealHoleCardsRoutine()
    {
        // 모든 플레이어에게 홀카드를 먼저 지급한 뒤 UI에서 딜 애니메이션을 재생합니다.
        for (int cardIndex = 0; cardIndex < 2; cardIndex++)
        {
            foreach (PlayerState player in handPlayers)
            {
                Card card = deck.Draw();
                player.HoleCards.Add(card);
                //Debug.Log(player.IsHuman ? $"[Deal] {player.Nickname} receives {card}" : $"[Deal] {player.Nickname} receives a hole card.");
            }
        }

        RenderCardState();
        RenderVisibleHandSeatState();

        float animationDuration = ui.PlayHoleCardDealAnimation(handPlayers, holeCardDealDuration, holeCardDealStagger);

        animationDuration = Mathf.Max(animationDuration, ui.Card3DRevealWaitDuration);
        if (animationDuration > 0f)
        {
            yield return new WaitForSeconds(animationDuration);
        }
    }

    private IEnumerator DealCommunityRoutine(PokerGameState dealState, PokerGameState bettingState, int count, string message)
    {
        awaitingPlayerAction = false;
        currentActor = null;
        SetState(dealState, message);
        RenderStatusAndActions();
        yield return new WaitForSeconds(dealDelay);

        for (int i = 0; i < count; i++)
        {
            Card card = deck.Draw();
            communityCards.Add(card);
            Debug.Log($"[Deal] Community card: {card}");
            RenderCardState();
            RenderVisibleHandSeatState();
            yield return new WaitForSeconds(ui.Card3DRevealWaitDuration);
        }

        betting.BeginNewStreet();
        raiseCount = 0;
        BeginBetting(bettingState, CurrentBlindInfo().PostflopFirstActor.SeatIndex, $"{bettingState}.");
    }

    private void BeginBetting(PokerGameState bettingState, int firstSeatIndex, string message)
    {
        // 베팅 라운드를 시작할 때 액션 가능한 플레이어를 대기 목록에 담고 첫 액션 좌석부터 진행합니다.
        SetState(bettingState, message);
        ResetPendingActors(EligibleActors());
        Debug.Log($"[State] Betting round started: {bettingState}, Pending={pendingActors.Count}");
        ContinueBettingFrom(firstSeatIndex);
    }

    private void ContinueBettingFrom(int seatIndex)
    {
        PlayerState previousActor = currentActor;
        StopActionTimer();
        awaitingPlayerAction = false;
        currentActor = null;

        PlayerState foldWinner = GetSingleUnfoldedPlayer();
        if (foldWinner != null)
        {
            FinishByFold(foldWinner); // 폴드하지 않은 플레이어가 한 명만 남으면 즉시 핸드를 종료합니다.
            return;
        }

        pendingActors.RemoveAll(player => !CanActInBetting(player));    
        if (pendingActors.Count == 0)
        {
            AdvanceAfterBettingRound(); // 더 이상 액션할 플레이어가 없으면 다음 스트리트로 넘어갑니다.
            return;
        }

        currentActor = NextPendingActorFrom(seatIndex);
        if (currentActor == null)
        {
            AdvanceAfterBettingRound(); // 대기 목록은 남았지만 실제로 선택할 액션 플레이어가 없으면 베팅 라운드를 정리합니다.
            return;
        }

        StartActionTimer(currentActor);

        if (currentActor.IsHuman)
        {
            awaitingPlayerAction = true;
            statusMessage = $"{currentActor.Nickname}'s action.";
            RenderStatusOnly();
            RenderActorChanged(previousActor, currentActor);
            return;
        }

        RenderActorChanged(previousActor, currentActor);
        StartCoroutine(BotActionRoutine(currentActor));
    }

    private IEnumerator BotActionRoutine(PlayerState bot)
    {
        statusMessage = $"{bot.Nickname} is thinking...";
        RenderStatusOnly();
        RenderSeatState(bot);
        float thinkDelay = pokerAi != null
            ? pokerAi.ChooseThinkDelay(bot, communityCards, betting, aiThinkDelay, actionTimeLimit, handPlayers)
            : aiThinkDelay;
        yield return new WaitForSeconds(thinkDelay);

        if (currentActor != bot || !IsBettingState(State) || !CanActInBetting(bot))
        {
            yield break;
        }

        PokerDecision decision = pokerAi != null
            ? pokerAi.Decide(bot, communityCards, betting, raiseCount, maxRaisesPerRound, handPlayers)
            : new PokerDecision(BettingAction.Check);
        ApplyDecision(bot, decision);
        CompleteCurrentAction(betting.LastActionWasFullRaise, betting.LastActionIncreasedBet);
    }

    private void StartActionTimer(PlayerState actor)
    {
        turnTimer?.Start(actor);
    }

    private void StopActionTimer()
    {
        turnTimer?.Stop();
    }

    private bool IsActiveTimedActor(PlayerState actor)
    {
        return actor != null
            && currentActor == actor
            && IsBettingState(State)
            && CanActInBetting(actor);
    }

    private void HandleActionTimeout(PlayerState actor)
    {
        if (!IsActiveTimedActor(actor))
        {
            return;
        }

        Debug.Log($"[Timer] {actor.Nickname} timed out.");
        if (betting.CanCheck(actor))    // 체크가 가능하면 체크로 넘기고, 아니면 자동 폴드 처리합니다.
        {
            betting.ApplyCheck(actor);
        }
        else
        {
            betting.ApplyFold(actor);
        }

        CompleteCurrentAction(bettingReopened: false);
    }

    private void CompleteCurrentAction(bool bettingReopened, bool betIncreased = false)
    {
        if (currentActor == null)
        {
            return;
        }

        PlayerState acted = currentActor;
        awaitingPlayerAction = false;
        StopActionTimer();

        if (bettingReopened)
        {
            // 유효 레이즈가 나오면 액션이 다시 열린 것이므로, 방금 행동한 플레이어를 제외한 나머지를 다시 대기 목록에 넣습니다.
            raiseCount++;
            ResetPendingActorsAfterFullRaise(acted);
        }
        else if (betIncreased)
        {
            // 숏 올인은 유효 레이즈가 아니므로 베팅을 다시 열지는 않습니다.
            // 다만 콜 금액이 남은 플레이어만 다시 액션하도록 대기 목록을 정리합니다.
            ResetPendingActorsAfterShortAllIn(acted);
        }
        else
        {
            pendingActors.Remove(acted);
        }

        currentActor = null;
        RenderBettingStateForSeat(acted);
        ContinueBettingFrom(NextSeatIndex(acted.SeatIndex));
    }

    private void ApplyDecision(PlayerState actor, PokerDecision decision)
    {
        switch (decision.Action)
        {
            case BettingAction.Fold:
                betting.ApplyFold(actor);
                break;
            case BettingAction.Check:
                betting.ApplyCheck(actor);
                break;
            case BettingAction.Call:
                betting.ApplyCall(actor);
                break;
            case BettingAction.Bet:
            case BettingAction.Raise:
                betting.ApplyBetOrRaise(actor, decision.TotalBetTarget);
                break;
        }

        Debug.Log($"[AI] {actor.Nickname} action: {decision.Action}, Target={decision.TotalBetTarget}");
    }

    private void AdvanceAfterBettingRound()
    {
        if (!resolvingBettingRound)
        {
            StartCoroutine(AdvanceAfterBettingRoundRoutine());
        }
    }

    private IEnumerator AdvanceAfterBettingRoundRoutine()
    {
        resolvingBettingRound = true;
        PlayerState foldWinner = GetSingleUnfoldedPlayer();
        if (foldWinner != null)
        {
            resolvingBettingRound = false;
            FinishByFold(foldWinner);
            yield break;
        }

        awaitingPlayerAction = false;
        currentActor = null;
        yield return WaitForActionImageHold();
        HideAllActionImages(animate: true);
        yield return betChipAnimator.CollectBetsToPot();

        if (ShouldAutoRunoutToShowdown())
        {
            // 남은 플레이어가 모두 올인이라 더 이상 베팅 액션이 불가능한 경우,
            // 남은 커뮤니티 카드를 자동으로 공개하고 쇼다운으로 진행합니다.
            yield return AutoRunoutToShowdownRoutine();
            resolvingBettingRound = false;
            yield break;
        }

        switch (State)
        {
            case PokerGameState.PreFlopBetting:
                StartCoroutine(DealCommunityRoutine(PokerGameState.DealFlop, PokerGameState.FlopBetting, 3, "Dealing the flop..."));
                break;
            case PokerGameState.FlopBetting:
                StartCoroutine(DealCommunityRoutine(PokerGameState.DealTurn, PokerGameState.TurnBetting, 1, "Dealing the turn..."));
                break;
            case PokerGameState.TurnBetting:
                StartCoroutine(DealCommunityRoutine(PokerGameState.DealRiver, PokerGameState.RiverBetting, 1, "Dealing the river..."));
                break;
            case PokerGameState.RiverBetting:
                StartCoroutine(ShowdownRoutine());
                break;
        }

        resolvingBettingRound = false;
    }

    private bool ShouldAutoRunoutToShowdown()
    {
        return ShouldAutoRunoutForRuleDebug(handPlayers, communityCards.Count, State);
    }

    public static bool ShouldAutoRunoutForRuleDebug(
        IReadOnlyList<PlayerState> players,
        int communityCardCount,
        PokerGameState state)
    {
        if (state == PokerGameState.RiverBetting || communityCardCount >= 5)
        {
            return false;
        }

        int contenders = 0;
        int playersWhoCanCreateSidePot = 0;
        for (int i = 0; i < players.Count; i++)
        {
            PlayerState player = players[i];
            if (player == null || player.HasFolded)
            {
                continue;
            }

            contenders++;
            if (!player.IsAllIn)
            {
                playersWhoCanCreateSidePot++;
            }
        }

        return contenders > 1 && playersWhoCanCreateSidePot < 2;
    }

    private IEnumerator AutoRunoutToShowdownRoutine()
    {
        while (communityCards.Count < 5)
        {
            int cardsToDeal = communityCards.Count == 0 ? 3 : 1;
            PokerGameState dealState = communityCards.Count == 0
                ? PokerGameState.DealFlop
                : communityCards.Count == 3 ? PokerGameState.DealTurn : PokerGameState.DealRiver;

            SetState(dealState, "All-in runout...");
            RenderStatusAndActions();
            yield return new WaitForSeconds(dealDelay);

            for (int i = 0; i < cardsToDeal && communityCards.Count < 5; i++)
            {
                Card card = deck.Draw();
                communityCards.Add(card);
                Debug.Log($"[Deal] Auto-runout community card: {card}");
                RenderCardState();
                RenderVisibleHandSeatState();
                yield return new WaitForSeconds(ui.Card3DRevealWaitDuration);
            }
        }

        yield return ShowdownRoutine();
    }

    private IEnumerator ShowdownRoutine()
    {
        // 폴드하지 않은 플레이어들의 홀카드 2장과 커뮤니티 카드 5장 중 가장 강한 5장 조합을 평가합니다.
        awaitingPlayerAction = false;
        currentActor = null;
        revealAllHoleCards = true;
        SetState(PokerGameState.Showdown, "Showdown");
        RenderFullTable();
        yield return new WaitForSeconds(ui.IsUsing3DCards ? ui.Card3DRevealWaitDuration + 0.05f : 0.45f);

        Dictionary<PlayerState, PokerHandResult> results = new Dictionary<PlayerState, PokerHandResult>(handPlayers.Count);
        foreach (PlayerState player in handPlayers)
        {
            if (player == null || player.HasFolded)
            {
                continue;
            }

            List<Card> sevenCards = new List<Card>(player.HoleCards);
            sevenCards.AddRange(communityCards);
            PokerHandResult result = PokerHandEvaluator.EvaluateBest(sevenCards);
            results[player] = result;
            Debug.Log($"[Showdown] {player.Nickname}={result}");
        }

        PokerHandResult best = null;  // 현재까지 찾은 가장 강한 족보입니다.
        foreach (PokerHandResult result in results.Values)
        {
            if (best == null || result.CompareTo(best) > 0) // 비교 대상이 현재 최고 족보보다 강하면 최고 족보를 갱신합니다.
            {
                best = result; 
            }
        }

        IReadOnlyList<PotAward> awards = betting.CalculateShowdownAwards(results);
        List<PlayerState> winners = new List<PlayerState>(MaxSeats);
        for (int i = 0; i < awards.Count; i++)
        {
            PlayerState player = awards[i].Player;
            if (player != null && !winners.Contains(player))
            {
                winners.Add(player);
            }
        }

        int potAmount = betting.Pot;
        yield return betChipAnimator.PlayPotPayout(winners, potAmount);
        int paidOut = betting.ApplyPotAwards(awards);

        if (winners.Count == 1)
        {
            SetState(PokerGameState.HandEnd, $"{winners[0].Nickname} wins {paidOut} with {best}.");
        }
        else
        {
            showdownSummaryBuilder.Clear();
            for (int i = 0; i < awards.Count; i++)
            {
                PotAward award = awards[i];
                if (i > 0)
                {
                    showdownSummaryBuilder.Append(", ");
                }

                showdownSummaryBuilder.Append(award.Player.Nickname);
                showdownSummaryBuilder.Append(" +");
                showdownSummaryBuilder.Append(award.Amount);
            }

            string summary = showdownSummaryBuilder.ToString();
            SetState(PokerGameState.HandEnd, $"Side pots paid {paidOut}. {summary}.");
        }

        MoveDealerButton();
        RenderFullTable();
        ProcessBustedBotsAfterHand();

        yield return new WaitForSeconds(startNewRoundDelay);

        StartNewHand();
    }

    private void FinishByFold(PlayerState winner)
    {
        if (!resolvingFoldWin)
        {
            StartCoroutine(FinishByFoldRoutine(winner));
        }
    }

    private IEnumerator FinishByFoldRoutine(PlayerState winner)
    {
        resolvingFoldWin = true;
        awaitingPlayerAction = false;
        currentActor = null;
        revealAllHoleCards = true;
        yield return WaitForActionImageHold();
        HideAllActionImages(animate: true);
        yield return betChipAnimator.CollectBetsToPot();
        int potAmount = betting.Pot;
        yield return betChipAnimator.PlayPotPayout(new[] { winner }, potAmount);
        int won = betting.AwardPotTo(winner);
        SetState(PokerGameState.HandEnd, $"{winner.Nickname} wins {won}. Everyone else folded.");
        MoveDealerButton();
        RenderFullTable();
        resolvingFoldWin = false;

        yield return new WaitForSeconds(startNewRoundDelay);

        StartNewHand();

        ProcessBustedBotsAfterHand();
    }

    private bool CanPlayerAct()
    {
        return awaitingPlayerAction
            && currentActor != null
            && currentActor.IsHuman
            && IsBettingState(State)
            && CanActInBetting(currentActor);
    }

    private IEnumerable<PlayerState> EligibleActors()
    {
        for (int i = 0; i < handPlayers.Count; i++)
        {
            PlayerState player = handPlayers[i];
            if (CanActInBetting(player))
            {
                yield return player;
            }
        }
    }

    private bool CanActInBetting(PlayerState player)
    {
        return player != null && !player.HasFolded && !player.IsAllIn;
    }

    private void ResetPendingActors(IEnumerable<PlayerState> players)
    {
        pendingActors.Clear();
        foreach (PlayerState player in players)
        {
            if (CanActInBetting(player))
            {
                pendingActors.Add(player);
            }
        }
    }

    private void ResetPendingActorsAfterFullRaise(PlayerState acted)
    {
        pendingActors.Clear();
        for (int i = 0; i < handPlayers.Count; i++)
        {
            PlayerState player = handPlayers[i];
            if (player != acted && CanActInBetting(player))
            {
                pendingActors.Add(player);
            }
        }
    }

    private void ResetPendingActorsAfterShortAllIn(PlayerState acted)
    {
        pendingActors.Clear();
        for (int i = 0; i < handPlayers.Count; i++)
        {
            PlayerState player = handPlayers[i];
            if (player != acted && CanActInBetting(player) && betting.CallAmountFor(player) > 0)
            {
                pendingActors.Add(player);
            }
        }
    }

    private PlayerState NextPendingActorFrom(int seatIndex)
    {
        for (int offset = 0; offset < MaxSeats; offset++)
        {
            int index = (seatIndex + offset) % MaxSeats;
            PlayerState player = table.GetSeat(index)?.Player;
            if (player != null && pendingActors.Contains(player))
            {
                return player;
            }
        }

        return null;
    }

    private PlayerState GetSingleUnfoldedPlayer()
    {
        PlayerState unfoldedPlayer = null;
        int unfoldedCount = 0;
        for (int i = 0; i < handPlayers.Count; i++)
        {
            PlayerState player = handPlayers[i];
            if (player == null || player.HasFolded)
            {
                continue;
            }

            unfoldedPlayer = player;
            unfoldedCount++;
            if (unfoldedCount > 1)
            {
                return null;
            }
        }

        return unfoldedCount == 1 ? unfoldedPlayer : null;
    }

    private BlindPositionInfo CurrentBlindInfo()
    {
        return PokerRuleUtility.CalculateBlindPositions(handPlayers, dealerSeatIndex, MaxSeats);
    }

    private int NextSeatIndex(int seatIndex)
    {
        return PokerRuleUtility.NextSeatIndex(seatIndex, MaxSeats);
    }

    private void MoveDealerButton()
    {
        List<PlayerState> playablePlayers = PlayablePlayersInSeatOrder();
        PlayerState nextDealer = PokerRuleUtility.PlayerAtSeatOrNext(NextSeatIndex(dealerSeatIndex), MaxSeats, playablePlayers);
        if (nextDealer != null)
        {
            dealerSeatIndex = nextDealer.SeatIndex;
        }
    }

    private static bool IsBettingState(PokerGameState state)
    {
        return state == PokerGameState.PreFlopBetting
            || state == PokerGameState.FlopBetting
            || state == PokerGameState.TurnBetting
            || state == PokerGameState.RiverBetting;
    }

    private void SetState(PokerGameState newState, string message)
    {
        State = newState;
        statusMessage = message;
        Debug.Log($"[State] {newState}: {message}");
    }

    private bool CanStartNextHandUi()
    {
        return State == PokerGameState.WaitingToStart || State == PokerGameState.HandEnd;
    }

    private void RenderFullTable()
    {
        if (ui == null)
        {
            return;
        }

        ui.RenderTableSnapshot(this, revealAllHoleCards, awaitingPlayerAction, CanStartNextHandUi());
    }

    private void RenderStatusOnly()
    {
        if (ui == null)
        {
            return;
        }

        ui.RenderStatus(this);
    }

    private void RenderStatusAndActions()
    {
        if (ui == null)
        {
            return;
        }

        ui.RenderStatus(this);
        ui.RenderCurrentActor(this, awaitingPlayerAction, CanStartNextHandUi());
    }

    private void RenderAllSeatState()
    {
        if (ui == null)
        {
            return;
        }

        ui.RenderAllSeats(this, revealAllHoleCards);
        ui.RenderCurrentActor(this, awaitingPlayerAction, CanStartNextHandUi());
    }

    private void RenderSeatState(int seatIndex)
    {
        if (ui == null)
        {
            return;
        }

        ui.RenderSeat(this, seatIndex, revealAllHoleCards);
    }

    private void RenderSeatState(PlayerState player)
    {
        if (player == null)
        {
            return;
        }

        RenderSeatState(player.SeatIndex);
    }

    private void HideActionImage(PlayerState player, bool animate)
    {
        if (ui == null || player == null)
        {
            return;
        }

        ui.HideActionImage(player.SeatIndex, player.LastAction, animate);
    }

    private void HideAllActionImages(bool animate)
    {
        if (ui == null)
        {
            return;
        }

        foreach (PlayerState player in table.OccupiedPlayers())
        {
            HideActionImage(player, animate);
        }
    }

    private IEnumerator WaitForActionImageHold()
    {
        if (actionImageRoundEndHoldDelay <= 0f)
        {
            yield break;
        }

        yield return new WaitForSeconds(actionImageRoundEndHoldDelay);
    }

    private void RenderSeatsState(params int[] seatIndices)
    {
        if (ui == null || seatIndices == null)
        {
            return;
        }

        HashSet<int> renderedSeatIndices = new HashSet<int>();
        foreach (int seatIndex in seatIndices)
        {
            if (seatIndex < 0 || !renderedSeatIndices.Add(seatIndex))
            {
                continue;
            }

            ui.RenderSeat(this, seatIndex, revealAllHoleCards);
        }
    }

    private void RenderSeatsState(IEnumerable<PlayerState> players)
    {
        if (ui == null || players == null)
        {
            return;
        }

        HashSet<int> renderedSeatIndices = new HashSet<int>();
        foreach (PlayerState player in players)
        {
            if (player == null || player.SeatIndex < 0 || !renderedSeatIndices.Add(player.SeatIndex))
            {
                continue;
            }

            ui.RenderSeat(this, player.SeatIndex, revealAllHoleCards);
        }
    }

    private void RenderActorChanged(PlayerState previousActor, PlayerState nextActor)
    {
        RenderSeatsState(
            previousActor != null ? previousActor.SeatIndex : -1,
            nextActor != null ? nextActor.SeatIndex : -1);

        if (ui != null)
        {
            ui.RenderCurrentActor(this, awaitingPlayerAction, CanStartNextHandUi());
        }

        HideActionImage(nextActor, animate: true);
    }

    private void RenderBettingStateForSeat(PlayerState player)
    {
        if (ui == null || player == null)
        {
            return;
        }

        ui.RenderPot(this);
        ui.ClearHiddenActionImageState(player.SeatIndex);
        RenderSeatState(player);
        ui.RenderCurrentActor(this, awaitingPlayerAction, CanStartNextHandUi());
    }

    private void RenderBettingStateForSeats(params PlayerState[] players)
    {
        if (ui == null)
        {
            return;
        }

        ui.RenderPot(this);
        RenderSeatsState(players);
        ui.RenderCurrentActor(this, awaitingPlayerAction, CanStartNextHandUi());
    }

    private void RenderVisibleHandSeatState()
    {
        if (ui == null)
        {
            return;
        }

        foreach (PlayerState player in table.OccupiedPlayers())
        {
            if (player != null && player.IsHuman)
            {
                ui.RenderSeat(this, player.SeatIndex, revealAllHoleCards);
            }
        }
    }

    private void RenderCardState()
    {
        if (ui == null)
        {
            return;
        }

        ui.RenderCardState(this, revealAllHoleCards);
    }

    private void CreateLocalPlayer()
    {
        localPlayer = new PlayerState("local-player", "Player", startingChips, true, 0);    // 로컬 플레이어 생성
        table.GetSeat(0).Sit(localPlayer);  // 플레이어를 0번 좌석에 앉힙니다.
        dealerSeatIndex = localPlayer.SeatIndex;    // 첫 핸드는 로컬 플레이어를 딜러 버튼 위치로 시작합니다.
    }

    private void BuildHandPlayers()
    {
        handPlayers.Clear();
        handPlayers.AddRange(table.OccupiedPlayersInSeatOrder());   // 착석 중인 플레이어를 좌석 순서대로 핸드 플레이어 목록에 복사합니다.
    }

    private List<PlayerState> PlayablePlayersInSeatOrder()
    {
        List<PlayerState> players = new List<PlayerState>(MaxSeats);
        foreach (PlayerState player in table.OccupiedPlayersInSeatOrder())
        {
            if (IsEligibleForNewHand(player))
            {
                players.Add(player);
            }
        }

        return players;
    }

    private int CountPlayablePlayersInSeatOrder()
    {
        int count = 0;
        foreach (PlayerState player in table.OccupiedPlayersInSeatOrder())
        {
            if (IsEligibleForNewHand(player))
            {
                count++;
            }
        }

        return count;
    }

    private PlayerState FindOpponent()
    {
        for (int i = 0; i < handPlayers.Count; i++)
        {
            PlayerState player = handPlayers[i];
            if (player != null && player != localPlayer)
            {
                return player;
            }
        }

        return null;
    }

    private static bool IsEligibleForNewHand(PlayerState player)
    {
        return player != null && player.Chips > 0;
    }

    private void SyncBettingPlayers()
    {
        handPlayers.Clear();
        handPlayers.AddRange(PlayablePlayersInSeatOrder());
        betting.Initialize(handPlayers, smallBlind, bigBlind);
    }

    private void EnsurePlayableStacks()
    {
        foreach (PlayerState player in table.OccupiedPlayers())
        {
            if (player.Chips < bigBlind)
            {
                // 새 핸드를 시작하기 전에 모든 플레이어의 칩이 빅블라인드 이상이 되도록 보충합니다. 이렇게 하면 새 핸드가 시작된 후 바로 올인으로 끝나는 상황을 방지할 수 있습니다.
                player.RefillChips(startingChips);
            }
        }
    }
}
