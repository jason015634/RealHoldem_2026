using System;
using System.Collections;
using UnityEngine;

public sealed class PokerTurnTimer
{
    private readonly MonoBehaviour coroutineRunner;
    private readonly PokerUIManager ui;
    private readonly Func<PlayerState, bool> isActiveActor;
    private readonly Action<PlayerState> timeoutHandler;
    private readonly float actionTimeLimit;

    private Coroutine timerCoroutine;
    private int timerToken;
    private float remainingTime;

    public bool IsRunning => timerCoroutine != null;
    public float NormalizedTime => actionTimeLimit > 0f ? Mathf.Clamp01(remainingTime / actionTimeLimit) : 0f;

    public PokerTurnTimer(
        MonoBehaviour coroutineRunner,
        PokerUIManager ui,
        float actionTimeLimit,
        Func<PlayerState, bool> isActiveActor,
        Action<PlayerState> timeoutHandler)
    {
        this.coroutineRunner = coroutineRunner;
        this.ui = ui;
        this.actionTimeLimit = actionTimeLimit;
        this.isActiveActor = isActiveActor;
        this.timeoutHandler = timeoutHandler;
    }

    public void Start(PlayerState actor)
    {
        Stop();

        if (coroutineRunner == null || actor == null || actionTimeLimit <= 0f)
        {
            return;
        }

        remainingTime = actionTimeLimit;
        timerToken++;
        ui?.SetTurnTimer(actor, 1f, true);
        timerCoroutine = coroutineRunner.StartCoroutine(TimerRoutine(actor, timerToken));
    }

    public void Stop()
    {
        timerToken++;
        remainingTime = 0f;

        if (timerCoroutine != null && coroutineRunner != null)
        {
            coroutineRunner.StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        ui?.HideTurnTimers();
    }

    private IEnumerator TimerRoutine(PlayerState actor, int token)
    {
        while (remainingTime > 0f && IsActive(actor, token))
        {
            remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
            ui?.SetTurnTimer(actor, NormalizedTime, true);
            yield return null;
        }

        if (!IsActive(actor, token))
        {
            yield break;
        }

        timerCoroutine = null;
        remainingTime = 0f;
        ui?.SetTurnTimer(actor, 0f, true);
        timeoutHandler?.Invoke(actor);
    }

    private bool IsActive(PlayerState actor, int token)
    {
        return token == timerToken
            && actor != null
            && isActiveActor != null
            && isActiveActor(actor);
    }
}
