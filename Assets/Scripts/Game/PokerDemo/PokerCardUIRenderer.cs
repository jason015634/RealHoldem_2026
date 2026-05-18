using System.Collections.Generic;
using UnityEngine;

internal sealed class PokerCardUIRenderer
{
    private readonly List<CardView> seatCardAnchors = new List<CardView>(12);

    private Canvas canvas;
    private RectTransform deckAnchor;
    private IReadOnlyList<PokerUIManager.SeatUi> seatUis;
    private IReadOnlyList<CardView> communityCards;
    private Poker3DCardTableView card3DTable;
    private bool use3DCards;
    private bool canvasConfiguredFor3DCards;

    public void Configure(
        Canvas canvas,
        RectTransform deckAnchor,
        IReadOnlyList<PokerUIManager.SeatUi> seatUis,
        IReadOnlyList<CardView> communityCards,
        Poker3DCardTableView card3DTable,
        bool use3DCards,
        bool canvasConfiguredFor3DCards)
    {
        this.canvas = canvas;
        this.deckAnchor = deckAnchor;
        this.seatUis = seatUis;
        this.communityCards = communityCards;
        this.card3DTable = card3DTable;
        this.use3DCards = use3DCards;
        this.canvasConfiguredFor3DCards = canvasConfiguredFor3DCards;
    }

    public bool CanvasConfiguredFor3DCards => canvasConfiguredFor3DCards;
    public Poker3DCardTableView Card3DTable => card3DTable;

    public bool ShouldRenderWith3DCards()
    {
        if (card3DTable == null)
        {
            card3DTable = Object.FindObjectOfType<Poker3DCardTableView>(true);
        }

        if (!use3DCards || card3DTable == null)
        {
            if (card3DTable != null)
            {
                card3DTable.Clear();
                card3DTable.gameObject.SetActive(false);
            }

            return false;
        }

        card3DTable.gameObject.SetActive(true);
        return true;
    }

    public void RenderCardState(PokerGameManager game, bool revealAllHoleCards)
    {
        if (game == null || !ShouldRenderWith3DCards())
        {
            return;
        }

        ConfigureCanvasFor3DCards();
        card3DTable.ApplyLayoutFromUiAnchors(canvas, GetSeatCardAnchors(), communityCards);
        card3DTable.Render(game.Seats, game.CommunityCards, revealAllHoleCards);
    }

    public void ClearAllCards()
    {
        if (card3DTable != null)
        {
            card3DTable.Clear();
        }
    }

    public float PlayHoleCardDealAnimation(IReadOnlyList<PlayerState> players, float duration, float stagger)
    {
        if (!ShouldRenderWith3DCards() || card3DTable == null)
        {
            return 0f;
        }

        return card3DTable.PlaySeatDealAnimation(canvas, deckAnchor, players, duration, stagger);
    }

    private IReadOnlyList<CardView> GetSeatCardAnchors()
    {
        seatCardAnchors.Clear();
        if (seatUis == null)
        {
            return seatCardAnchors;
        }

        foreach (PokerUIManager.SeatUi seat in seatUis)
        {
            if (seat.Cards != null)
            {
                seatCardAnchors.AddRange(seat.Cards);
            }
        }

        return seatCardAnchors;
    }

    private void ConfigureCanvasFor3DCards()
    {
        if (canvasConfiguredFor3DCards || canvas == null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = mainCamera;
            canvas.planeDistance = 20f;
        }

        canvasConfiguredFor3DCards = true;
    }
}
