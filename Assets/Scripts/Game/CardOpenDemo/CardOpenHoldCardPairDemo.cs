using UnityEngine;

// Scene setup guide:
// 1. In CardOpenTestScene, create a HoldCards root object near the table/player area.
// 2. Add child objects LeftCard and RightCard, each with CardOpenPeekingDemo.
// 3. Add this CardOpenHoldCardPairDemo to the HoldCards root and assign LeftCard/RightCard.
// 4. The two card children should be slightly overlapped at rest; this script adds fan/spread while dragging.
// 5. Use colliders on the card objects and set Input Layers to include their layer.

public sealed class CardOpenHoldCardPairDemo : MonoBehaviour
{
    [Header("Cards")]
    [SerializeField] private CardOpenPeekingDemo leftCard;
    [SerializeField] private CardOpenPeekingDemo rightCard;

    [Header("Input")]
    [SerializeField] private Camera inputCamera;
    [SerializeField] private LayerMask inputLayers = ~0;
    [SerializeField] private float dragPixelsForFullLift = 260f;

    [Header("Pair Pose")]
    [SerializeField] private Vector3 liftOffset = new Vector3(0f, 0.012f, -0.008f);
    [SerializeField] private float revealTiltDegrees = 0f;
    [Range(0f, 1f)]
    [Tooltip("0 = local -Y/player-near edge, 1 = local +Y/far edge.")]
    [SerializeField] private float hingePosition = 0.08f;
    [SerializeField] private float leftFanDegrees = -7f;
    [SerializeField] private float rightFanDegrees = 8f;
    [SerializeField] private Vector3 leftSpread = new Vector3(-0.035f, 0f, 0.012f);
    [SerializeField] private Vector3 rightSpread = new Vector3(0.04f, 0.012f, -0.012f);
    [Range(0f, 0.25f)]
    [SerializeField] private float rightCardLeadProgress = 0.06f;

    [Header("Paper Feel")]
    [Range(0f, 1f)]
    [SerializeField] private float maxSoftCurl = 0.62f;
    [Range(0f, 1f)]
    [SerializeField] private float settledCurlRatio = 0.18f;
    [SerializeField] private float dragFollowSpeed = 18f;
    [SerializeField] private float returnSpeed = 10f;
    [SerializeField] private AnimationCurve revealEase = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.6f),
        new Keyframe(0.45f, 0.28f, 0.9f, 1.4f),
        new Keyframe(1f, 1f, 0f, 0f));

    private struct RestPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    private RestPose leftRest;
    private RestPose rightRest;
    private float dragStartY;
    private int activeTouchId = -1;
    private float targetProgress;
    private float currentProgress;
    private bool isDragging;

    private void Awake()
    {
        ResolveCards();
        CacheRestPose();
        SetChildInput(false);
        ApplyPose(0f, immediate: true);
    }

    private void OnValidate()
    {
        dragPixelsForFullLift = Mathf.Max(1f, dragPixelsForFullLift);
        revealTiltDegrees = Mathf.Clamp(revealTiltDegrees, -12f, 24f);
        hingePosition = Mathf.Clamp01(hingePosition);
        maxSoftCurl = Mathf.Clamp01(maxSoftCurl);
        settledCurlRatio = Mathf.Clamp01(settledCurlRatio);
        dragFollowSpeed = Mathf.Max(0.01f, dragFollowSpeed);
        returnSpeed = Mathf.Max(0.01f, returnSpeed);
        rightCardLeadProgress = Mathf.Clamp(rightCardLeadProgress, 0f, 0.25f);
    }

    private void Update()
    {
        HandleInput();
        UpdateProgress(Time.deltaTime);
    }

    [ContextMenu("Cache Current Rest Pose")]
    private void CacheRestPose()
    {
        ResolveCards();

        if (leftCard != null)
        {
            leftRest.Position = leftCard.transform.localPosition;
            leftRest.Rotation = leftCard.transform.localRotation;
        }

        if (rightCard != null)
        {
            rightRest.Position = rightCard.transform.localPosition;
            rightRest.Rotation = rightCard.transform.localRotation;
        }
    }

    [ContextMenu("Test Reveal Pose")]
    private void TestRevealPose()
    {
        ResolveCards();
        ApplyPose(0.68f, immediate: true);
        currentProgress = 0.68f;
        targetProgress = 0.68f;
    }

    [ContextMenu("Reset Pose")]
    private void ResetPose()
    {
        ResolveCards();
        currentProgress = 0f;
        targetProgress = 0f;
        isDragging = false;
        activeTouchId = -1;
        ApplyPose(0f, immediate: true);
    }

    private void ResolveCards()
    {
        if (leftCard == null || rightCard == null)
        {
            CardOpenPeekingDemo[] cards = GetComponentsInChildren<CardOpenPeekingDemo>(true);
            if (cards.Length > 0 && leftCard == null)
            {
                leftCard = cards[0];
            }

            if (cards.Length > 1 && rightCard == null)
            {
                rightCard = cards[1];
            }
        }

        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }
    }

    private void SetChildInput(bool enabled)
    {
        if (leftCard != null)
        {
            leftCard.SetInputEnabled(enabled);
        }

        if (rightCard != null)
        {
            rightCard.SetInputEnabled(enabled);
        }
    }

    private void HandleInput()
    {
        if (!isDragging && TryGetPointerDown(out Vector2 downPosition))
        {
            TryBeginDrag(downPosition);
            return;
        }

        if (!isDragging)
        {
            return;
        }

        if (TryGetPointerPosition(out Vector2 position))
        {
            float drag = position.y - dragStartY;
            targetProgress = Mathf.Clamp01(drag / dragPixelsForFullLift);
        }

        if (TryGetPointerUp())
        {
            isDragging = false;
            activeTouchId = -1;
            targetProgress = 0f;
        }
    }

    private bool TryBeginDrag(Vector2 screenPosition)
    {
        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }

        if (inputCamera == null)
        {
            return false;
        }

        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, inputLayers, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (!IsCardCollider(hit.collider))
        {
            return false;
        }

        isDragging = true;
        dragStartY = screenPosition.y;
        return true;
    }

    private bool IsCardCollider(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return false;
        }

        return (leftCard != null && hitCollider.transform == leftCard.transform)
            || (rightCard != null && hitCollider.transform == rightCard.transform)
            || (leftCard != null && hitCollider.transform.IsChildOf(leftCard.transform))
            || (rightCard != null && hitCollider.transform.IsChildOf(rightCard.transform));
    }

    private bool TryGetPointerDown(out Vector2 position)
    {
        if (Input.touchCount > 0)
        {
            foreach (Touch touch in Input.touches)
            {
                if (touch.phase == TouchPhase.Began)
                {
                    activeTouchId = touch.fingerId;
                    position = touch.position;
                    return true;
                }
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            activeTouchId = -1;
            position = Input.mousePosition;
            return true;
        }

        position = default;
        return false;
    }

    private bool TryGetPointerPosition(out Vector2 position)
    {
        if (activeTouchId >= 0)
        {
            foreach (Touch touch in Input.touches)
            {
                if (touch.fingerId == activeTouchId)
                {
                    position = touch.position;
                    return true;
                }
            }

            position = default;
            return false;
        }

        position = Input.mousePosition;
        return Input.GetMouseButton(0);
    }

    private bool TryGetPointerUp()
    {
        if (activeTouchId >= 0)
        {
            foreach (Touch touch in Input.touches)
            {
                if (touch.fingerId == activeTouchId)
                {
                    return touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                }
            }

            return true;
        }

        return Input.GetMouseButtonUp(0);
    }

    private void UpdateProgress(float deltaTime)
    {
        float speed = isDragging ? dragFollowSpeed : returnSpeed;
        float lerp = 1f - Mathf.Exp(-speed * deltaTime);
        currentProgress = Mathf.Lerp(currentProgress, targetProgress, lerp);

        if (Mathf.Abs(currentProgress - targetProgress) < 0.0001f)
        {
            currentProgress = targetProgress;
        }

        ApplyPose(currentProgress, immediate: false);
    }

    private void ApplyPose(float progress, bool immediate)
    {
        float leftProgress = EvaluateProgress(progress);
        float rightProgress = EvaluateProgress(Mathf.Clamp01(progress + rightCardLeadProgress));

        ApplyCardPose(leftCard, leftRest, leftProgress, progress, leftFanDegrees, leftSpread, immediate);
        ApplyCardPose(
            rightCard,
            rightRest,
            rightProgress,
            Mathf.Clamp01(progress + rightCardLeadProgress),
            rightFanDegrees,
            rightSpread,
            immediate);
    }

    private float EvaluateProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        return revealEase != null ? Mathf.Clamp01(revealEase.Evaluate(progress)) : Smooth01(progress);
    }

    private void ApplyCardPose(
        CardOpenPeekingDemo card,
        RestPose rest,
        float poseProgress,
        float curlProgress,
        float fanDegrees,
        Vector3 spread,
        bool immediate)
    {
        if (card == null)
        {
            return;
        }

        float smooth = Smooth01(poseProgress);
        Vector3 hinge = GetLocalHinge(card);
        Quaternion targetRotation = rest.Rotation * Quaternion.Euler(-revealTiltDegrees * smooth, 0f, fanDegrees * smooth);
        Vector3 pivot = rest.Position + rest.Rotation * hinge;
        Vector3 hingedPosition = pivot - targetRotation * hinge;
        Vector3 targetPosition = Vector3.Lerp(rest.Position, hingedPosition + liftOffset * smooth + spread * smooth, smooth);

        card.transform.localPosition = targetPosition;
        card.transform.localRotation = targetRotation;

        float curl = maxSoftCurl * EvaluateCurlEnvelope(curlProgress);
        if (immediate)
        {
            card.SetPeekProgressImmediate(curl);
        }
        else
        {
            card.SetPeekProgressTarget(curl);
        }
    }

    private Vector3 GetLocalHinge(CardOpenPeekingDemo card)
    {
        if (card == null)
        {
            return Vector3.zero;
        }

        float localY = Mathf.Lerp(-card.Height * 0.5f, card.Height * 0.5f, hingePosition);
        return new Vector3(0f, localY, 0f);
    }

    private float EvaluateCurlEnvelope(float progress)
    {
        progress = Mathf.Clamp01(progress);
        float rise = Smooth01(Mathf.InverseLerp(0.02f, 0.72f, progress));
        float settle = Smooth01(Mathf.InverseLerp(0.72f, 1f, progress));
        return rise * Mathf.Lerp(1f, settledCurlRatio, settle);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
