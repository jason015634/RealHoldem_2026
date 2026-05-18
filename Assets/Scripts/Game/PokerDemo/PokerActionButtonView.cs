using UnityEngine;
using UnityEngine.UI;

internal sealed class PokerActionButtonView
{
    private Button foldButton;
    private Button checkCallButton;
    private Button betRaiseButton;
    private Button nextHandButton;
    private Button addBotButton;
    private Button removeBotButton;
    private Slider betSlider;
    private Text betAmountText;
    private Text checkCallButtonText;
    private Text betRaiseButtonText;
    private Text nextHandButtonText;
    private Text addBotButtonText;
    private Text removeBotButtonText;

    public void Configure(
        Button foldButton,
        Button checkCallButton,
        Button betRaiseButton,
        Button nextHandButton,
        Button addBotButton,
        Button removeBotButton,
        Slider betSlider,
        Text betAmountText,
        Text checkCallButtonText,
        Text betRaiseButtonText,
        Text nextHandButtonText,
        Text addBotButtonText,
        Text removeBotButtonText)
    {
        this.foldButton = foldButton;
        this.checkCallButton = checkCallButton;
        this.betRaiseButton = betRaiseButton;
        this.nextHandButton = nextHandButton;
        this.addBotButton = addBotButton;
        this.removeBotButton = removeBotButton;
        this.betSlider = betSlider;
        this.betAmountText = betAmountText;
        this.checkCallButtonText = checkCallButtonText;
        this.betRaiseButtonText = betRaiseButtonText;
        this.nextHandButtonText = nextHandButtonText;
        this.addBotButtonText = addBotButtonText;
        this.removeBotButtonText = removeBotButtonText;
    }

    public void Render(PokerGameManager game, bool awaitingPlayerAction, bool canStartNextHand)
    {
        if (game == null || game.Betting == null)
        {
            return;
        }

        BettingManager betting = game.Betting;
        PlayerState actingPlayer = game.CurrentActor;
        int callAmount = actingPlayer != null ? betting.CallAmountFor(actingPlayer) : 0;
        bool canHumanAct = awaitingPlayerAction && actingPlayer != null && actingPlayer.IsHuman;

        SetActive(foldButton, canHumanAct);
        SetActive(checkCallButton, canHumanAct);
        SetActive(betRaiseButton, canHumanAct);
        SetActive(betAmountText, canHumanAct);
        SetActive(nextHandButton, canStartNextHand);
        SetActive(addBotButton, false);
        SetActive(removeBotButton, false);

        SetButtonInteractable(foldButton, canHumanAct);
        SetButtonInteractable(checkCallButton, canHumanAct);
        SetButtonInteractable(betRaiseButton, canHumanAct && betting.CanBetOrRaise(actingPlayer));
        SetButtonInteractable(nextHandButton, canStartNextHand && game.CanStartNewHand);
        SetButtonInteractable(addBotButton, false);
        SetButtonInteractable(removeBotButton, false);

        SetText(checkCallButtonText, callAmount <= 0 ? "Check" : $"Call {callAmount}");
        SetText(betRaiseButtonText, betting.CurrentBet <= 0 ? "Bet" : "Raise");
        SetText(nextHandButtonText, game.State == PokerGameState.WaitingToStart ? "Start" : "Next");
        SetText(addBotButtonText, "+");
        SetText(removeBotButtonText, "-");

        UpdateBetSlider(actingPlayer, betting, canHumanAct);
    }

    public void UpdateBetAmountLabel(int value)
    {
        if (betAmountText != null)
        {
            betAmountText.text = $"Bet To {value}";
        }
    }

    private void UpdateBetSlider(PlayerState player, BettingManager betting, bool canAct)
    {
        if (betSlider == null || player == null || betting == null)
        {
            return;
        }

        int min = betting.MinimumBetTarget(player);
        int max = betting.MaximumBetTarget(player);
        bool canBet = canAct && betting.CanBetOrRaise(player) && max >= min;

        betSlider.gameObject.SetActive(canAct);
        betSlider.interactable = canBet;
        betSlider.wholeNumbers = true;
        betSlider.minValue = min;
        betSlider.maxValue = Mathf.Max(min, max);

        int preferred = Mathf.Clamp(min, min, Mathf.Max(min, max));
        if (!Mathf.Approximately(betSlider.value, preferred))
        {
            betSlider.SetValueWithoutNotify(preferred);
        }

        UpdateBetAmountLabel(preferred);
    }

    private static void SetActive(Selectable selectable, bool active)
    {
        if (selectable != null)
        {
            selectable.gameObject.SetActive(active);
        }
    }

    private static void SetActive(Graphic graphic, bool active)
    {
        if (graphic != null)
        {
            graphic.gameObject.SetActive(active);
        }
    }

    private static void SetText(Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button == null)
        {
            return;
        }

        button.interactable = interactable;
        CanvasGroup group = button.GetComponent<CanvasGroup>();
        if (group == null)
        {
            return;
        }

        group.alpha = interactable ? 1f : 0.45f;
    }
}
