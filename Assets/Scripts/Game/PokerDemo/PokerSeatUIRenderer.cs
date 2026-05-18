using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

internal sealed class PokerSeatUIRenderer
{
    private readonly Dictionary<string, Sprite> characterSpritesByPath = new Dictionary<string, Sprite>();
    private Sprite[] characterSprites = System.Array.Empty<Sprite>();
    private string characterResourcesPath = "Sprites/Characters";
    private Color normalCharacterTint = Color.white;
    private Color normalCharacterBackgroundTint = new Color(1f, 0.893f, 0f, 1f);
    private Color foldedCharacterTint = new Color(0.42f, 0.42f, 0.42f, 0.72f);
    private Color foldedCharacterBackgroundTint = new Color(0.2f, 0.2f, 0.2f, 0.82f);
    private float seatEnterDuration = 0.38f;
    private float seatExitDuration = 0.28f;
    private bool characterSpritesLoaded;

    public void Configure(
        string characterResourcesPath,
        Color normalCharacterTint,
        Color normalCharacterBackgroundTint,
        Color foldedCharacterTint,
        Color foldedCharacterBackgroundTint,
        float seatEnterDuration,
        float seatExitDuration)
    {
        this.characterResourcesPath = characterResourcesPath;
        this.normalCharacterTint = normalCharacterTint;
        this.normalCharacterBackgroundTint = normalCharacterBackgroundTint;
        this.foldedCharacterTint = foldedCharacterTint;
        this.foldedCharacterBackgroundTint = foldedCharacterBackgroundTint;
        this.seatEnterDuration = seatEnterDuration;
        this.seatExitDuration = seatExitDuration;
    }

    public void RenderSeat(
        PokerUIManager.SeatUi ui,
        PokerSeat seat,
        PlayerState actingPlayer,
        IReadOnlyList<Card> visibleCommunityCards,
        bool waitingForNextHand,
        PokerActionImageView actionImageView,
        bool revealAllCards)
    {
        if (ui == null)
        {
            return;
        }

        EnsureSeatAnimationState(ui);
        string currentPlayerId = seat != null && !seat.IsEmpty ? seat.Player.PlayerId : string.Empty;
        bool shouldAnimateChange = ui.HasRendered && ui.LastPlayerId != currentPlayerId;
        bool becameOccupied = shouldAnimateChange && !string.IsNullOrEmpty(currentPlayerId);
        bool becameEmpty = shouldAnimateChange && string.IsNullOrEmpty(currentPlayerId);
        ui.LastPlayerId = currentPlayerId;
        ui.HasRendered = true;

        if (seat == null || seat.IsEmpty)
        {
            SetText(ui.NameText, "Empty Seat");
            SetText(ui.ChipText, string.Empty);
            SetText(ui.BetText, string.Empty);
            SetText(ui.ActionText, string.Empty);
            actionImageView?.Hide(ui.ActionImage, ui.ActionText, false, null, false);
            ClearCharacterImage(ui);
            ApplyFoldedCharacterVisual(ui, false);
            SetText(ui.StatusText, "EMPTY");
            if (becameEmpty)
            {
                PlaySeatExitAnimation(ui);
            }
            return;
        }

        PlayerState player = seat.Player;
        SetText(ui.NameText, player.Nickname);
        SetText(ui.ChipText, $"Chips {player.Chips}");
        SetText(ui.BetText, player.CurrentBet > 0 ? $"Bet {player.CurrentBet}" : string.Empty);
        RenderCharacterImage(ui, player);
        ApplyFoldedCharacterVisual(ui, player.HasFolded);
        SetText(ui.ActionText, player.LastAction);
        actionImageView?.Render(ui.ActionImage, ui.ActionText, player);
        SetText(ui.StatusText, BuildSeatStatus(player, actingPlayer, visibleCommunityCards, waitingForNextHand, revealAllCards));

        if (becameOccupied)
        {
            PlaySeatEnterAnimation(ui);
        }
    }

    public void RenderTurnTimer(IReadOnlyList<PokerUIManager.SeatUi> seatUis, PlayerState actor, float normalizedTime, bool visible)
    {
        if (seatUis == null)
        {
            return;
        }

        int actorSeatIndex = actor != null ? actor.SeatIndex : -1;
        float fillAmount = Mathf.Clamp01(normalizedTime);

        for (int i = 0; i < seatUis.Count; i++)
        {
            Image clock = seatUis[i].UserTimeClock;
            if (clock == null)
            {
                continue;
            }

            bool showClock = visible && i == actorSeatIndex;
            clock.gameObject.SetActive(showClock);
            clock.type = Image.Type.Filled;
            clock.fillMethod = Image.FillMethod.Radial360;
            clock.fillOrigin = (int)Image.Origin360.Top;
            clock.fillClockwise = false;
            clock.fillAmount = showClock ? fillAmount : 0f;
        }
    }

    public float PlaySeatExitAnimation(PokerUIManager.SeatUi seat)
    {
        if (seat == null)
        {
            return 0f;
        }

        PlaySeatExitAnimationInternal(seat);
        seat.LastPlayerId = string.Empty;
        seat.HasRendered = true;
        return Mathf.Max(0f, seatExitDuration);
    }

    public void ResetFoldedCharacterVisuals(IReadOnlyList<PokerUIManager.SeatUi> seatUis)
    {
        if (seatUis == null)
        {
            return;
        }

        foreach (PokerUIManager.SeatUi seat in seatUis)
        {
            ApplyFoldedCharacterVisual(seat, false);
        }
    }

    private void EnsureSeatAnimationState(PokerUIManager.SeatUi ui)
    {
        if (ui == null || ui.Root == null)
        {
            return;
        }

        if (!ui.HasHomePosition)
        {
            ui.HomePosition = ui.Root.anchoredPosition;
            ui.HasHomePosition = true;
        }

        if (ui.Group == null)
        {
            ui.Group = ui.Root.GetComponent<CanvasGroup>();
            if (ui.Group == null)
            {
                ui.Group = ui.Root.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void PlaySeatEnterAnimation(PokerUIManager.SeatUi ui)
    {
        if (ui == null || ui.Root == null)
        {
            return;
        }

        EnsureSeatAnimationState(ui);
        ui.SeatSequence?.Kill(false);
        ui.Root.DOKill();
        ui.Group.DOKill();

        ui.Root.anchoredPosition = ui.HomePosition + ui.EnterOffset;
        ui.Root.localScale = Vector3.one * 0.8f;
        ui.Group.alpha = 0f;

        ui.SeatSequence = DOTween.Sequence()
            .Join(ui.Group.DOFade(1f, seatEnterDuration))
            .Join(ui.Root.DOScale(1f, seatEnterDuration).SetEase(Ease.OutBack))
            .Join(ui.Root.DOAnchorPos(ui.HomePosition, seatEnterDuration).SetEase(Ease.OutCubic));
    }

    private void PlaySeatExitAnimationInternal(PokerUIManager.SeatUi ui)
    {
        if (ui == null || ui.Root == null)
        {
            return;
        }

        EnsureSeatAnimationState(ui);
        ui.SeatSequence?.Kill(false);
        ui.Root.DOKill();
        ui.Group.DOKill();

        ui.Root.anchoredPosition = ui.HomePosition;
        ui.Root.localScale = Vector3.one;
        ui.Group.alpha = 1f;

        ui.SeatSequence = DOTween.Sequence()
            .Join(ui.Group.DOFade(0f, seatExitDuration))
            .Join(ui.Root.DOScale(0.8f, seatExitDuration).SetEase(Ease.InBack))
            .Join(ui.Root.DOAnchorPos(ui.HomePosition + ui.ExitOffset, seatExitDuration).SetEase(Ease.InCubic))
            .OnComplete(() =>
            {
                ui.Root.anchoredPosition = ui.HomePosition;
                ui.Root.localScale = Vector3.one;
                ui.Group.alpha = 1f;
            });
    }

    private void RenderCharacterImage(PokerUIManager.SeatUi ui, PlayerState player)
    {
        if (ui.UserImageGraphic == null || player == null)
        {
            return;
        }

        Sprite sprite = GetCharacterSprite(player);
        ui.UserImageGraphic.sprite = sprite;
        ui.UserImageGraphic.preserveAspect = true;
        ui.UserImageGraphic.enabled = sprite != null;
    }

    private void ClearCharacterImage(PokerUIManager.SeatUi ui)
    {
        if (ui.UserImageGraphic == null)
        {
            return;
        }

        ui.UserImageGraphic.sprite = null;
        ui.UserImageGraphic.enabled = false;
    }

    private void ApplyFoldedCharacterVisual(PokerUIManager.SeatUi ui, bool folded)
    {
        if (ui == null)
        {
            return;
        }

        if (ui.UserImageGraphic != null)
        {
            ui.UserImageGraphic.color = folded ? foldedCharacterTint : normalCharacterTint;
        }

        if (ui.UserImageBackground != null && ui.UserImageBackground != ui.UserImageGraphic)
        {
            ui.UserImageBackground.color = folded ? foldedCharacterBackgroundTint : normalCharacterBackgroundTint;
        }
    }

    private Sprite GetCharacterSprite(PlayerState player)
    {
        EnsureCharacterSpritesLoaded();
        if (characterSprites.Length == 0)
        {
            return null;
        }

        if (string.IsNullOrEmpty(player.CharacterSpritePath))
        {
            Sprite randomSprite = characterSprites[Random.Range(0, characterSprites.Length)];
            player.SetCharacterSpritePath($"{characterResourcesPath}/{randomSprite.name}");
            return randomSprite;
        }

        if (characterSpritesByPath.TryGetValue(player.CharacterSpritePath, out Sprite sprite))
        {
            return sprite;
        }

        sprite = Resources.Load<Sprite>(player.CharacterSpritePath);
        if (sprite != null)
        {
            characterSpritesByPath[player.CharacterSpritePath] = sprite;
        }

        return sprite;
    }

    private void EnsureCharacterSpritesLoaded()
    {
        if (characterSpritesLoaded)
        {
            return;
        }

        characterSprites = Resources.LoadAll<Sprite>(characterResourcesPath);
        characterSpritesByPath.Clear();
        foreach (Sprite sprite in characterSprites)
        {
            if (sprite == null)
            {
                continue;
            }

            characterSpritesByPath[$"{characterResourcesPath}/{sprite.name}"] = sprite;
        }

        characterSpritesLoaded = true;
    }

    private static void SetText(Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private static string BuildSeatStatus(
        PlayerState player,
        PlayerState actingPlayer,
        IReadOnlyList<Card> visibleCommunityCards,
        bool waitingForNextHand,
        bool revealAllCards)
    {
        if (waitingForNextHand)
        {
            return "\uB300\uAE30\uC911";
        }

        if (revealAllCards && player.IsBot)
        {
            string botHandStatus = BuildHumanHandStatus(player, visibleCommunityCards);
            return CombineSeatStatus(botHandStatus);
        }

        if (player.IsBot)
        {
            return "BOT";
        }

        string handStatus = BuildHumanHandStatus(player, visibleCommunityCards);
        if (player == actingPlayer)
        {
            return CombineSeatStatus(handStatus);
        }

        if (player.HasFolded)
        {
            return "FOLD";
        }

        if (player.IsAllIn)
        {
            return CombineSeatStatus(handStatus);
        }

        return string.IsNullOrEmpty(handStatus) ? "HUMAN" : handStatus;
    }

    private static string BuildHumanHandStatus(PlayerState player, IReadOnlyList<Card> visibleCommunityCards)
    {
        if (player == null || player.HoleCards.Count == 0)
        {
            return string.Empty;
        }

        int visibleCardCount = player.HoleCards.Count + (visibleCommunityCards?.Count ?? 0);
        if (visibleCardCount >= 5)
        {
            List<Card> visibleCards = new List<Card>(visibleCardCount);
            visibleCards.AddRange(player.HoleCards);
            if (visibleCommunityCards != null)
            {
                visibleCards.AddRange(visibleCommunityCards);
            }

            return $"{ToStatusHandName(PokerHandEvaluator.EvaluateBest(visibleCards).Category)}";
        }

        PokerHandCategory category = EvaluateVisibleHandCategory(player.HoleCards, visibleCommunityCards);
        return $"{ToStatusHandName(category)}";
    }

    private static string CombineSeatStatus(string handStatus)
    {
        return $"{handStatus}";
    }

    private static PokerHandCategory EvaluateVisibleHandCategory(
        IReadOnlyList<Card> holeCards,
        IReadOnlyList<Card> visibleCommunityCards)
    {
        int[] rankCounts = new int[15];
        AddRankCounts(holeCards, rankCounts);
        AddRankCounts(visibleCommunityCards, rankCounts);

        int pairCount = 0;
        bool hasTrips = false;
        bool hasQuads = false;
        for (int rank = 2; rank < rankCounts.Length; rank++)
        {
            int count = rankCounts[rank];
            if (count == 4)
            {
                hasQuads = true;
            }
            else if (count == 3)
            {
                hasTrips = true;
            }
            else if (count == 2)
            {
                pairCount++;
            }
        }

        if (hasQuads)
        {
            return PokerHandCategory.FourOfAKind;
        }

        if (hasTrips && pairCount > 0)
        {
            return PokerHandCategory.FullHouse;
        }

        if (hasTrips)
        {
            return PokerHandCategory.ThreeOfAKind;
        }

        if (pairCount >= 2)
        {
            return PokerHandCategory.TwoPair;
        }

        return pairCount == 1 ? PokerHandCategory.OnePair : PokerHandCategory.HighCard;
    }

    private static void AddRankCounts(IReadOnlyList<Card> cards, int[] rankCounts)
    {
        if (cards == null)
        {
            return;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            rankCounts[cards[i].RankValue]++;
        }
    }

    private static string ToStatusHandName(PokerHandCategory category)
    {
        switch (category)
        {
            case PokerHandCategory.RoyalFlush:
                return "ROYAL FLUSH";
            case PokerHandCategory.StraightFlush:
                return "STRAIGHT FLUSH";
            case PokerHandCategory.FourOfAKind:
                return "FOUR KIND";
            case PokerHandCategory.FullHouse:
                return "FULL HOUSE";
            case PokerHandCategory.Flush:
                return "FLUSH";
            case PokerHandCategory.Straight:
                return "STRAIGHT";
            case PokerHandCategory.ThreeOfAKind:
                return "THREE KIND";
            case PokerHandCategory.TwoPair:
                return "TWO PAIR";
            case PokerHandCategory.OnePair:
                return "ONE PAIR";
            default:
                return "HIGH CARD";
        }
    }
}
