using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class PokerActionImageView
{
    private sealed class ActionImageState
    {
        public string LastActionImageKey;
        public string HiddenActionText;
        public Sequence Sequence;
    }

    private readonly Dictionary<Image, ActionImageState> states = new Dictionary<Image, ActionImageState>();

    private Sprite allInActionSprite;
    private Sprite betActionSprite;
    private Sprite callActionSprite;
    private Sprite checkActionSprite;
    private Sprite foldActionSprite;
    private float popStartScale;
    private float popOvershootScale;
    private float popInDuration;
    private float settleDuration;
    private float hideDuration;
    private float hideScale;
    private bool actionSpritesLoaded;

    public void Configure(
        Sprite allInActionSprite,
        Sprite betActionSprite,
        Sprite callActionSprite,
        Sprite checkActionSprite,
        Sprite foldActionSprite,
        float popStartScale,
        float popOvershootScale,
        float popInDuration,
        float settleDuration,
        float hideDuration,
        float hideScale)
    {
        if (allInActionSprite != null)
        {
            this.allInActionSprite = allInActionSprite;
        }

        if (betActionSprite != null)
        {
            this.betActionSprite = betActionSprite;
        }

        if (callActionSprite != null)
        {
            this.callActionSprite = callActionSprite;
        }

        if (checkActionSprite != null)
        {
            this.checkActionSprite = checkActionSprite;
        }

        if (foldActionSprite != null)
        {
            this.foldActionSprite = foldActionSprite;
        }

        this.popStartScale = popStartScale;
        this.popOvershootScale = popOvershootScale;
        this.popInDuration = popInDuration;
        this.settleDuration = settleDuration;
        this.hideDuration = hideDuration;
        this.hideScale = hideScale;
    }

    public void Render(Image actionImage, Text actionText, PlayerState player)
    {
        if (actionImage == null)
        {
            return;
        }

        ActionImageState state = GetState(actionImage);
        string currentActionText = player != null ? player.LastAction : string.Empty;
        if (string.IsNullOrWhiteSpace(currentActionText))
        {
            state.HiddenActionText = string.Empty;
            Hide(actionImage, actionText, false, null, false);
            return;
        }

        if (state.HiddenActionText == currentActionText)
        {
            Hide(actionImage, actionText, false, currentActionText, true);
            return;
        }

        string actionKey = GetActionImageKey(player, currentActionText);
        if (string.IsNullOrEmpty(actionKey))
        {
            Hide(actionImage, actionText, false, null, false);
            return;
        }

        EnsureActionSpritesLoaded();
        Sprite actionSprite = GetActionSprite(actionKey);
        if (actionSprite == null)
        {
            Hide(actionImage, actionText, false, null, false);
            return;
        }

        if (state.LastActionImageKey == actionKey
            && actionImage.sprite == actionSprite
            && actionImage.gameObject.activeSelf)
        {
            return;
        }

        state.LastActionImageKey = actionKey;
        state.HiddenActionText = string.Empty;
        actionImage.sprite = actionSprite;
        actionImage.preserveAspect = true;
        actionImage.gameObject.SetActive(true);
        PlayPopup(actionImage, state);
    }

    public void Hide(Image actionImage, Text actionText, bool animate, string hiddenActionText, bool suppressCurrentAction)
    {
        if (actionImage == null)
        {
            return;
        }

        ActionImageState state = GetState(actionImage);
        if (suppressCurrentAction && !string.IsNullOrWhiteSpace(hiddenActionText))
        {
            state.HiddenActionText = hiddenActionText;
        }
        else if (suppressCurrentAction && actionText != null && !string.IsNullOrWhiteSpace(actionText.text))
        {
            state.HiddenActionText = actionText.text;
        }
        else if (!suppressCurrentAction)
        {
            state.HiddenActionText = string.Empty;
        }

        state.LastActionImageKey = string.Empty;
        state.Sequence?.Kill(false);
        state.Sequence = null;
        actionImage.DOKill();
        actionImage.rectTransform.DOKill();

        if (animate && actionImage.gameObject.activeSelf)
        {
            RectTransform actionTransform = actionImage.rectTransform;
            state.Sequence = DOTween.Sequence()
                .Join(actionImage.DOFade(0f, hideDuration).SetEase(Ease.OutQuad))
                .Join(actionTransform.DOScale(hideScale, hideDuration).SetEase(Ease.InBack))
                .OnComplete(() =>
                {
                    actionTransform.localScale = Vector3.one;
                    actionImage.gameObject.SetActive(false);
                    state.Sequence = null;
                });
            return;
        }

        Color color = actionImage.color;
        color.a = 0f;
        actionImage.color = color;
        actionImage.rectTransform.localScale = Vector3.one;
        actionImage.gameObject.SetActive(false);
    }

    public void ClearHiddenState(Image actionImage)
    {
        if (actionImage == null)
        {
            return;
        }

        GetState(actionImage).HiddenActionText = string.Empty;
    }

    private void PlayPopup(Image actionImage, ActionImageState state)
    {
        state.Sequence?.Kill(false);
        actionImage.DOKill();
        actionImage.rectTransform.DOKill();

        RectTransform actionTransform = actionImage.rectTransform;
        Color color = actionImage.color;
        color.a = 0f;
        actionImage.color = color;
        actionTransform.localScale = Vector3.one * popStartScale;

        state.Sequence = DOTween.Sequence()
            .Join(actionImage.DOFade(1f, popInDuration))
            .Join(actionTransform.DOScale(popOvershootScale, popInDuration).SetEase(Ease.OutBack))
            .Append(actionTransform.DOScale(1f, settleDuration).SetEase(Ease.OutQuad));
    }

    private ActionImageState GetState(Image actionImage)
    {
        if (!states.TryGetValue(actionImage, out ActionImageState state))
        {
            state = new ActionImageState();
            states[actionImage] = state;
        }

        return state;
    }

    private string GetActionImageKey(PlayerState player, string actionText)
    {
        if (string.IsNullOrWhiteSpace(actionText))
        {
            return string.Empty;
        }

        string normalized = actionText.Trim().ToLowerInvariant()
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        if (normalized.StartsWith("fold"))
        {
            return "Fold";
        }

        if ((player != null && player.IsAllIn && !player.HasFolded) || normalized.Contains("allin"))
        {
            return "AllIn";
        }

        if (normalized.StartsWith("check"))
        {
            return "Check";
        }

        if (normalized.StartsWith("call"))
        {
            return "Call";
        }

        if (normalized.StartsWith("bet") || normalized.StartsWith("raise"))
        {
            return "Bet";
        }

        return string.Empty;
    }

    private Sprite GetActionSprite(string actionKey)
    {
        switch (actionKey)
        {
            case "AllIn":
                return allInActionSprite;
            case "Bet":
                return betActionSprite;
            case "Call":
                return callActionSprite;
            case "Check":
                return checkActionSprite;
            case "Fold":
                return foldActionSprite;
            default:
                return null;
        }
    }

    private void EnsureActionSpritesLoaded()
    {
        if (actionSpritesLoaded)
        {
            return;
        }

        allInActionSprite = allInActionSprite != null ? allInActionSprite : Resources.Load<Sprite>("Sprites/UI/AllIn");
        betActionSprite = betActionSprite != null ? betActionSprite : Resources.Load<Sprite>("Sprites/UI/Bet");
        callActionSprite = callActionSprite != null ? callActionSprite : Resources.Load<Sprite>("Sprites/UI/Call");
        checkActionSprite = checkActionSprite != null ? checkActionSprite : Resources.Load<Sprite>("Sprites/UI/Check");
        foldActionSprite = foldActionSprite != null ? foldActionSprite : Resources.Load<Sprite>("Sprites/UI/Fold");
        actionSpritesLoaded = true;
    }
}
