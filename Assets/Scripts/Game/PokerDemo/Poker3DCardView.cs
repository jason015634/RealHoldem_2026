using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if DOTWEEN
using DG.Tweening;
#endif

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
// 3D 카드 한 장의 메시, 재질, 앞/뒷면 표시, 딜 이동, 코너 플립 애니메이션을 담당합니다.
// 3D 카드 테이블 뷰가 이 컴포넌트를 여러 장 배치해서 테이블 전체 카드를 렌더링합니다.
public sealed class Poker3DCardView : MonoBehaviour
{
    private readonly struct MeshKey : System.IEquatable<MeshKey>
    {
        private readonly int width;
        private readonly int height;
        private readonly int thickness;
        private readonly int segments;

        public MeshKey(float width, float height, float thickness, int segments)
        {
            this.width = Mathf.RoundToInt(width * 10000f);
            this.height = Mathf.RoundToInt(height * 10000f);
            this.thickness = Mathf.RoundToInt(thickness * 10000f);
            this.segments = segments;
        }

        public bool Equals(MeshKey other)
        {
            return width == other.width
                && height == other.height
                && thickness == other.thickness
                && segments == other.segments;
        }

        public override bool Equals(object obj)
        {
            return obj is MeshKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = width;
                hash = (hash * 397) ^ height;
                hash = (hash * 397) ^ thickness;
                hash = (hash * 397) ^ segments;
                return hash;
            }
        }
    }

    public enum FlipStyle
    {
        Normal,
        DramaticHinge
    }

    private const string BaseMapProperty = "_BaseMap";
    private const string MainTexProperty = "_MainTex";
    private const string BaseColorProperty = "_BaseColor";
    private const string ColorProperty = "_Color";
    private const string FlipProgressProperty = "_FlipProgress";
    private const string BendAmountProperty = "_BendAmount";
    private const string LiftAmountProperty = "_LiftAmount";
    private const string FlipRotationProperty = "_FlipRotation";
    private const string ShineAmountProperty = "_ShineAmount";
    private const string CardWidthProperty = "_CardWidth";
    private const string CardHeightProperty = "_CardHeight";
    private const string FlipStyleProperty = "_FlipStyle";
    private const string HingeCurlProperty = "_HingeCurl";
    private const string HingeTuckProperty = "_HingeTuck";
    private const string HingeFollowDelayProperty = "_HingeFollowDelay";
    private const string CornerRollProperty = "_CornerRoll";
    private const string GripStartProperty = "_GripStart";
    private const string InwardCurlAmountProperty = "_InwardCurlAmount";
    private const string InwardCurlLiftProperty = "_InwardCurlLift";
    private const string InwardCurlVerticalTuckProperty = "_InwardCurlVerticalTuck";
    private const string DiagonalTiltProperty = "_DiagonalTilt";
    private const string CornerRollLiftProperty = "_CornerRollLift";
    private const string CornerRollCurlProperty = "_CornerRollCurl";
    private const string CornerRollTuckProperty = "_CornerRollTuck";
    private const string SettleFlexProperty = "_SettleFlex";

    [Header("Card Mesh")]
    [SerializeField] private float width = 0.62f;
    [SerializeField] private float height = 0.88f;
    [SerializeField] private float thickness = 0.025f;
    [SerializeField] private int segments = 24;

    [Header("CardFlipDemo Timing")]
    [SerializeField] private float flipDuration = 0.58f;
    [SerializeField] private float dramaticHingeDuration = 1.05f;
    [SerializeField] private float peekDurationRatio = 0.56f;
    [SerializeField] private float snapDurationRatio = 0.34f;
    [SerializeField] private float settleDurationRatio = 0.1f;
    [SerializeField] private float peekHoldTime = 0.02f;
    [SerializeField] private float peekProgress = 0.342f;
    [SerializeField] private float snapEndProgress = 0.86f;

    [Header("CardFlipDemo Shape")]
    [SerializeField] private float maxBend = 0.5f;
    [SerializeField] private float inwardCurlAmount = 0.035f;
    [SerializeField] private float inwardCurlLift = 0.02f;
    [SerializeField] private float inwardCurlVerticalTuck = 0.01f;
    [SerializeField] private Vector2 customGripStartPosition = new Vector2(-1f, 0f);
    [SerializeField] private float diagonalTilt = 0f;
    [SerializeField] private bool normalUseCornerRoll;
    [SerializeField] private bool dramaticUseCornerRoll = true;
    [SerializeField] private float cornerRollLift = 0.5f;
    [SerializeField] private float cornerRollCurl = 0.1f;
    [SerializeField] private float cornerRollTuck = 0.1f;
    [SerializeField] private float cornerFollowDelay = 0.48f;
    [SerializeField] private float silhouetteHold = 0.34f;
    [SerializeField] private float settleFlex = 0.006f;

    [Header("CardFlipDemo Easing")]
    [SerializeField] private AnimationCurve peekEase = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.25f),
        new Keyframe(0.7f, 0.78f, 1.25f, 0.55f),
        new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private AnimationCurve snapEase = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.28f, 0.08f, 0.35f, 2.8f),
        new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private AnimationCurve settleEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("CardFlipDemo Reveal Motion")]
    [SerializeField] private Vector3 revealLiftOffset = new Vector3(0.03f, 0.015f, -0.08f);
    [SerializeField] private float revealTiltDegrees = -1.2f;

    [Header("Fallback Visuals")]
    [SerializeField] private Color frontFallbackColor = Color.white;
    [SerializeField] private Color backColor = new Color(0.08f, 0.18f, 0.42f, 1f);
    [SerializeField] private Color edgeColor = new Color(0.86f, 0.86f, 0.78f, 1f);

    private static Material sharedFaceMaterial;
    private static Material sharedEdgeMaterial;
    private static Sprite sharedBackSprite;
    private static Texture2D sharedBackTexture;
    private static readonly Dictionary<MeshKey, Mesh> sharedMeshes = new Dictionary<MeshKey, Mesh>();

    private Mesh cardMesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock frontBlock;
    private MaterialPropertyBlock backBlock;
    private MaterialPropertyBlock edgeBlock;
    private float builtWidth;
    private float builtHeight;
    private float builtThickness;
    private int builtSegments;
    private bool isFaceUp;
    private bool isFlipping;
    private bool isDealAnimating;
    private Coroutine dealCoroutine;

    // 스크립트는 카드 플립 데모의 타이밍/트랜스폼 움직임을 유지하고, 셰이더는 이 값들로 휘어짐을 계산합니다.
    private float shaderFlipProgress;
    private float shaderBendAmount;
    private float shaderLiftAmount;
    private float shaderFlipRotation;
    private float shaderShineAmount;
    private float shaderFlipStyle;
    private float shaderHingeCurl;
    private float shaderHingeTuck;
    private float shaderHingeFollowDelay;
    private float shaderCornerRoll;

#if DOTWEEN
    private Sequence flipSequence;
#endif

    public float Width => width;
    public float Height => height;
    public float Thickness => thickness;
    public float FlipDuration => flipDuration;
    public float MaxFlipDuration => Mathf.Max(GetFlipDuration(FlipStyle.Normal), GetFlipDuration(FlipStyle.DramaticHinge));
    public bool IsFlipping => isFlipping;

    private void Awake()
    {
        EnsureReady();
    }

    private void OnValidate()
    {
        width = Mathf.Max(0.01f, width);
        height = Mathf.Max(0.01f, height);
        thickness = Mathf.Max(0.001f, thickness);
        segments = Mathf.Clamp(segments, 4, 48);
        flipDuration = Mathf.Max(0.05f, flipDuration);
        dramaticHingeDuration = Mathf.Max(0.05f, dramaticHingeDuration);
        peekDurationRatio = Mathf.Max(0.01f, peekDurationRatio);
        snapDurationRatio = Mathf.Max(0.01f, snapDurationRatio);
        settleDurationRatio = Mathf.Max(0.01f, settleDurationRatio);
        peekHoldTime = Mathf.Max(0f, peekHoldTime);
        peekProgress = Mathf.Clamp(peekProgress, 0.05f, 0.8f);
        snapEndProgress = Mathf.Clamp(snapEndProgress, peekProgress + 0.05f, 0.95f);
        maxBend = Mathf.Max(0f, maxBend);
        inwardCurlAmount = Mathf.Max(0f, inwardCurlAmount);
        inwardCurlLift = Mathf.Max(0f, inwardCurlLift);
        inwardCurlVerticalTuck = Mathf.Max(0f, inwardCurlVerticalTuck);
        customGripStartPosition = new Vector2(
            Mathf.Clamp(customGripStartPosition.x, -1f, 1f),
            Mathf.Clamp(customGripStartPosition.y, -1f, 1f));
        diagonalTilt = Mathf.Clamp(diagonalTilt, -45f, 45f);
        cornerRollLift = Mathf.Max(0f, cornerRollLift);
        cornerRollCurl = Mathf.Max(0f, cornerRollCurl);
        cornerRollTuck = Mathf.Max(0f, cornerRollTuck);
        cornerFollowDelay = Mathf.Clamp01(cornerFollowDelay);
        silhouetteHold = Mathf.Clamp01(silhouetteHold);
        settleFlex = Mathf.Max(0f, settleFlex);

        if (Application.isPlaying && isActiveAndEnabled)
        {
            EnsureReady();
        }
    }

    public void SetSize(float newWidth, float newHeight, float newThickness)
    {
        width = Mathf.Max(0.01f, newWidth);
        height = Mathf.Max(0.01f, newHeight);
        thickness = Mathf.Max(0.001f, newThickness);
        EnsureReady();
    }

    public void SetCard(Card card, bool faceUp)
    {
        CancelFlip();
        EnsureReady();
        gameObject.SetActive(true);
        isFaceUp = faceUp;
        ResetMeshDeformation();
        ApplyCardVisual(card, faceUp);
    }

    public void Reveal(Card card, bool animated)
    {
        Reveal(card, animated, 0f);
    }

    public void Reveal(Card card, bool animated, float delay)
    {
        Reveal(card, animated, delay, FlipStyle.Normal);
    }

    public void Reveal(Card card, bool animated, float delay, FlipStyle flipStyle)
    {
        if (!animated)
        {
            SetCard(card, true);
            return;
        }

        HideBack(card);
        PlayFlipToFace(card, delay, flipStyle);
    }

    public void HideBack(Card card)
    {
        CancelFlip();
        EnsureReady();
        gameObject.SetActive(true);
        isFaceUp = false;
        ResetMeshDeformation();
        ApplyCardVisual(card, false);
    }

    public void PlayDealFromLocal(Vector3 originLocalPosition, float duration, float delay, float spinDegrees, float arcHeight)
    {
        EnsureReady();
        gameObject.SetActive(true);

        if (dealCoroutine != null)
        {
            StopCoroutine(dealCoroutine);
        }

        dealCoroutine = StartCoroutine(DealFromLocalRoutine(originLocalPosition, duration, delay, spinDegrees, arcHeight));
    }

    public void PlayFlipToFace(Card card)
    {
        PlayFlipToFace(card, 0f);
    }

    public void PlayFlipToFace(Card card, float delay)
    {
        PlayFlipToFace(card, delay, FlipStyle.Normal);
    }

    public void PlayFlipToFace(Card card, float delay, FlipStyle flipStyle)
    {
        if (isFlipping)
        {
            return;
        }

        EnsureReady();
        gameObject.SetActive(true);
        isFaceUp = false;
        isFlipping = true;

        Vector3 lockedLocalPosition = transform.localPosition;
        Quaternion lockedLocalRotation = transform.localRotation;
        float progress = 0f;
        bool faceTextureApplied = false;
        float activeFlipDuration = GetFlipDuration(flipStyle);
        float faceTextureProgress = GetFaceTextureProgress(flipStyle);

#if DOTWEEN
        flipSequence?.Kill(false);
        flipSequence = DOTween.Sequence();
        if (delay > 0f)
        {
            flipSequence.AppendInterval(delay);
        }

        float remainingDuration = Mathf.Max(0.05f, activeFlipDuration - peekHoldTime);
        float ratioSum = Mathf.Max(0.01f, peekDurationRatio + snapDurationRatio + settleDurationRatio);
        float peekDuration = remainingDuration * (peekDurationRatio / ratioSum);
        float snapDuration = remainingDuration * (snapDurationRatio / ratioSum);
        float settleDuration = remainingDuration * (settleDurationRatio / ratioSum);

        AppendRotationTween(
            peekProgress,
            peekDuration,
            peekEase,
            flipStyle,
            lockedLocalPosition,
            lockedLocalRotation,
            () => progress,
            value => progress = value,
            card,
            () => faceTextureApplied,
            value => faceTextureApplied = value,
            faceTextureProgress);

        if (peekHoldTime > 0f)
        {
            flipSequence.AppendInterval(peekHoldTime);
        }

        AppendRotationTween(
            snapEndProgress,
            snapDuration,
            snapEase,
            flipStyle,
            lockedLocalPosition,
            lockedLocalRotation,
            () => progress,
            value => progress = value,
            card,
            () => faceTextureApplied,
            value => faceTextureApplied = value,
            faceTextureProgress);

        AppendRotationTween(
            1f,
            settleDuration,
            settleEase,
            flipStyle,
            lockedLocalPosition,
            lockedLocalRotation,
            () => progress,
            value => progress = value,
            card,
            () => faceTextureApplied,
            value => faceTextureApplied = value,
            faceTextureProgress);

        flipSequence.OnComplete(() =>
        {
            if (!isDealAnimating)
            {
                transform.localPosition = lockedLocalPosition;
                transform.localRotation = lockedLocalRotation;
            }

            CompleteReveal(card);
        });

        flipSequence.OnKill(() =>
        {
            if (isFlipping)
            {
                if (!isDealAnimating)
                {
                    transform.localPosition = lockedLocalPosition;
                    transform.localRotation = lockedLocalRotation;
                }

                ResetMeshDeformation();
                isFlipping = false;
            }
        });
#else
        CompleteReveal(card);
#endif
    }

#if DOTWEEN
    private void AppendRotationTween(
        float targetProgress,
        float duration,
        AnimationCurve ease,
        FlipStyle flipStyle,
        Vector3 startPosition,
        Quaternion startRotation,
        System.Func<float> getProgress,
        System.Action<float> setProgress,
        Card card,
        System.Func<bool> getFaceTextureApplied,
        System.Action<bool> setFaceTextureApplied,
        float faceTextureProgress)
    {
        DG.Tweening.Core.DOGetter<float> progressGetter = () => getProgress();
        DG.Tweening.Core.DOSetter<float> progressSetter = value =>
        {
            setProgress(value);
            float currentProgress = getProgress();
            float liftEnvelope = GetRevealLiftEnvelope(currentProgress, flipStyle);
            float yAngle = Mathf.Lerp(0f, 180f * GetFlipDirection(), GetRotationProgress(currentProgress, flipStyle));
            float zTilt = revealTiltDegrees * liftEnvelope;

            ApplyFlip(currentProgress, flipStyle);

            if (!getFaceTextureApplied() && ShouldApplyFaceTexture(currentProgress, flipStyle, faceTextureProgress))
            {
                ApplyCardVisual(card, true, true);
                setFaceTextureApplied(true);
            }

            if (!isDealAnimating)
            {
                transform.localPosition = startPosition + revealLiftOffset * liftEnvelope;
                transform.localRotation = startRotation * GetFlipRotation(currentProgress, yAngle, zTilt, flipStyle);
            }
        };

        flipSequence.Append(DOTween
            .To(progressGetter, progressSetter, targetProgress, Mathf.Max(0.01f, duration))
            .SetEase(ease ?? AnimationCurve.Linear(0f, 0f, 1f, 1f)));
    }
#endif

    public void ResetMeshDeformation()
    {
        EnsureReady();
        ResetShaderDeformation();
        ApplyCurrentPropertyBlocks();
    }

    public void Clear()
    {
        CancelDeal();
        CancelFlip();
        EnsureReady();
        isFaceUp = false;
        ResetMeshDeformation();
        gameObject.SetActive(false);
    }

    private float GetFlipDuration(FlipStyle flipStyle)
    {
        return flipStyle == FlipStyle.DramaticHinge ? dramaticHingeDuration : flipDuration;
    }

    private float GetFaceTextureProgress(FlipStyle flipStyle)
    {
        return flipStyle == FlipStyle.DramaticHinge ? 0.56f : 0.5f;
    }

    private IEnumerator DealFromLocalRoutine(Vector3 originLocalPosition, float duration, float delay, float spinDegrees, float arcHeight)
    {
        Vector3 targetPosition = transform.localPosition;
        Quaternion targetRotation = transform.localRotation;
        Quaternion startRotation = targetRotation * Quaternion.Euler(0f, 0f, spinDegrees);

        transform.localPosition = originLocalPosition;  // 시작위치로 세팅
        transform.localRotation = startRotation;        // 시작 각도로 세팅
        isDealAnimating = true;

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);     // 카드마다 조금씩 다르게 출발
        }

        if (duration <= 0f)                             // 재생 시간이 0 이하이면 애니메이션 없이 바로 도착
        {
            transform.localPosition = targetPosition;
            transform.localRotation = targetRotation;
            isDealAnimating = false;
            dealCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)                      // 실제 이동 함수
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            Vector3 position = Vector3.LerpUnclamped(originLocalPosition, targetPosition, eased);
            position.z -= Mathf.Sin(t * Mathf.PI) * arcHeight;
            transform.localPosition = position;
            transform.localRotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, eased);
            yield return null;
        }

        transform.localPosition = targetPosition;
        transform.localRotation = targetRotation;
        isDealAnimating = false;
        dealCoroutine = null;
    }

    private void EnsureReady()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        frontBlock ??= new MaterialPropertyBlock();
        backBlock ??= new MaterialPropertyBlock();
        edgeBlock ??= new MaterialPropertyBlock();

        EnsureSharedMaterials();
        meshRenderer.sharedMaterials = new[] { sharedFaceMaterial, sharedFaceMaterial, sharedEdgeMaterial };
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        if (cardMesh == null
            || meshFilter.sharedMesh != cardMesh
            || !Mathf.Approximately(builtWidth, width)
            || !Mathf.Approximately(builtHeight, height)
            || !Mathf.Approximately(builtThickness, thickness)
            || builtSegments != segments)
        {
            cardMesh = GetSharedCardMesh(width, height, thickness, segments, revealLiftOffset, cornerRollLift, inwardCurlLift, maxBend, cornerRollCurl, settleFlex);
            meshFilter.sharedMesh = cardMesh;
            builtWidth = width;
            builtHeight = height;
            builtThickness = thickness;
            builtSegments = segments;
        }

        ApplyEdgeBlock();
        meshRenderer.SetPropertyBlock(edgeBlock, 2);
    }

    private static void EnsureSharedMaterials()
    {
        if (sharedFaceMaterial == null)
        {
            sharedFaceMaterial = new Material(FindCardShader())
            {
                name = "Poker3DCard_Face_Shared",
                renderQueue = 4990
            };
            ConfigureDoubleSided(sharedFaceMaterial);
        }

        if (sharedEdgeMaterial == null)
        {
            sharedEdgeMaterial = new Material(FindCardShader())
            {
                name = "Poker3DCard_Edge_Shared",
                renderQueue = 4990
            };
            ConfigureDoubleSided(sharedEdgeMaterial);
        }

        if (sharedBackSprite == null && sharedBackTexture == null)
        {
            sharedBackSprite = CardSpriteCache.GetBack();
            if (sharedBackSprite == null)
            {
                sharedBackTexture = BuildBackTexture();
            }
        }
    }

    private static Shader FindCardShader()
    {
        Shader shader = Shader.Find("RealHoldem/PokerCardDeform");
        if (shader != null)
        {
            return shader;
        }

        shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            return shader;
        }

        shader = Shader.Find("Unlit/Texture");
        return shader != null ? shader : Shader.Find("Standard");
    }

    private static void ConfigureDoubleSided(Material material)
    {
        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 0f);
        }

        material.doubleSidedGI = true;
    }

    private static Texture2D BuildBackTexture()
    {
        const int textureWidth = 64;
        const int textureHeight = 96;
        Texture2D texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            name = "Poker3DCard_Back_Runtime",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color main = new Color(0.08f, 0.18f, 0.42f, 1f);
        Color stripe = new Color(0.16f, 0.29f, 0.58f, 1f);
        Color border = new Color(0.82f, 0.86f, 0.98f, 1f);

        for (int y = 0; y < textureHeight; y++)
        {
            for (int x = 0; x < textureWidth; x++)
            {
                bool isBorder = x < 3 || y < 3 || x >= textureWidth - 3 || y >= textureHeight - 3;
                bool isInnerBorder = x == 8 || y == 8 || x == textureWidth - 9 || y == textureHeight - 9;
                bool isStripe = ((x + y) % 14) < 5;
                texture.SetPixel(x, y, isBorder || isInnerBorder ? border : isStripe ? stripe : main);
            }
        }

        texture.Apply(false, true);
        return texture;
    }

    private static Mesh GetSharedCardMesh(
        float width,
        float height,
        float thickness,
        int segments,
        Vector3 revealLiftOffset,
        float cornerRollLift,
        float inwardCurlLift,
        float maxBend,
        float cornerRollCurl,
        float settleFlex)
    {
        MeshKey key = new MeshKey(width, height, thickness, Mathf.Clamp(segments, 4, 48));
        if (!sharedMeshes.TryGetValue(key, out Mesh mesh) || mesh == null)
        {
            mesh = BuildCardMesh(width, height, thickness, key, segments, revealLiftOffset, cornerRollLift, inwardCurlLift, maxBend, cornerRollCurl, settleFlex);
            sharedMeshes[key] = mesh;
        }

        return mesh;
    }

    private static Mesh BuildCardMesh(
        float width,
        float height,
        float thickness,
        MeshKey key,
        int segments,
        Vector3 revealLiftOffset,
        float cornerRollLift,
        float inwardCurlLift,
        float maxBend,
        float cornerRollCurl,
        float settleFlex)
    {
        int segmentCount = Mathf.Max(4, segments);
        int columns = segmentCount + 1;
        int rows = segmentCount + 1;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float halfThickness = thickness * 0.5f;

        List<Vector3> vertices = new List<Vector3>(columns * rows * 2 + segmentCount * 16);
        List<Vector2> uvs = new List<Vector2>(vertices.Capacity);
        List<int> frontTriangles = new List<int>(segmentCount * segmentCount * 6);
        List<int> backTriangles = new List<int>(segmentCount * segmentCount * 6);
        List<int> edgeTriangles = new List<int>(segmentCount * 24);

        int frontStart = vertices.Count;
        AddGrid(vertices, uvs, -halfThickness, segmentCount, width, height);
        int backStart = vertices.Count;
        AddGrid(vertices, uvs, halfThickness, segmentCount, width, height);

        for (int y = 0; y < segmentCount; y++)
        {
            for (int x = 0; x < segmentCount; x++)
            {
                int bottomLeft = frontStart + y * columns + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = frontStart + (y + 1) * columns + x;
                int topRight = topLeft + 1;
                frontTriangles.Add(bottomLeft);
                frontTriangles.Add(topLeft);
                frontTriangles.Add(topRight);
                frontTriangles.Add(bottomLeft);
                frontTriangles.Add(topRight);
                frontTriangles.Add(bottomRight);

                bottomLeft = backStart + y * columns + x;
                bottomRight = bottomLeft + 1;
                topLeft = backStart + (y + 1) * columns + x;
                topRight = topLeft + 1;
                backTriangles.Add(bottomLeft);
                backTriangles.Add(bottomRight);
                backTriangles.Add(topRight);
                backTriangles.Add(bottomLeft);
                backTriangles.Add(topRight);
                backTriangles.Add(topLeft);
            }
        }

        for (int x = 0; x < segmentCount; x++)
        {
            float u0 = x / (float)segmentCount;
            float u1 = (x + 1) / (float)segmentCount;
            float x0 = Mathf.Lerp(-halfWidth, halfWidth, u0);
            float x1 = Mathf.Lerp(-halfWidth, halfWidth, u1);
            AddEdgeQuad(vertices, uvs, edgeTriangles,
                new Vector3(x0, halfHeight, -halfThickness),
                new Vector3(x1, halfHeight, -halfThickness),
                new Vector3(x1, halfHeight, halfThickness),
                new Vector3(x0, halfHeight, halfThickness));
            AddEdgeQuad(vertices, uvs, edgeTriangles,
                new Vector3(x1, -halfHeight, -halfThickness),
                new Vector3(x0, -halfHeight, -halfThickness),
                new Vector3(x0, -halfHeight, halfThickness),
                new Vector3(x1, -halfHeight, halfThickness));
        }

        for (int y = 0; y < segmentCount; y++)
        {
            float v0 = y / (float)segmentCount;
            float v1 = (y + 1) / (float)segmentCount;
            float y0 = Mathf.Lerp(-halfHeight, halfHeight, v0);
            float y1 = Mathf.Lerp(-halfHeight, halfHeight, v1);
            AddEdgeQuad(vertices, uvs, edgeTriangles,
                new Vector3(-halfWidth, y0, -halfThickness),
                new Vector3(-halfWidth, y1, -halfThickness),
                new Vector3(-halfWidth, y1, halfThickness),
                new Vector3(-halfWidth, y0, halfThickness));
            AddEdgeQuad(vertices, uvs, edgeTriangles,
                new Vector3(halfWidth, y1, -halfThickness),
                new Vector3(halfWidth, y0, -halfThickness),
                new Vector3(halfWidth, y0, halfThickness),
                new Vector3(halfWidth, y1, halfThickness));
        }

        Mesh mesh = new Mesh
        {
            name = $"Segmented Poker 3D Card {key.GetHashCode()}"
        };

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 3;
        mesh.SetTriangles(frontTriangles, 0);
        mesh.SetTriangles(backTriangles, 1);
        mesh.SetTriangles(edgeTriangles, 2);
        mesh.RecalculateNormals();
        float boundsLift = revealLiftOffset.magnitude + cornerRollLift + inwardCurlLift;
        float boundsBend = maxBend + cornerRollCurl + settleFlex;
        float boundsDepth = Mathf.Max(width, height) * 2.5f + boundsLift + boundsBend;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(width * 3f, height * 3f, boundsDepth));
        return mesh;
    }

    private static void AddGrid(List<Vector3> vertices, List<Vector2> uvs, float z, int segmentCount, float width, float height)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        for (int y = 0; y <= segmentCount; y++)
        {
            float v = y / (float)segmentCount;
            float localY = Mathf.Lerp(-halfHeight, halfHeight, v);
            for (int x = 0; x <= segmentCount; x++)
            {
                float u = x / (float)segmentCount;
                float localX = Mathf.Lerp(-halfWidth, halfWidth, u);
                vertices.Add(new Vector3(localX, localY, z));
                uvs.Add(new Vector2(u, v));
            }
        }
    }

    private static void AddEdgeQuad(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(1f, 1f));
        uvs.Add(new Vector2(0f, 1f));
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
    }

    private void ApplyFlip(float flipProgress, FlipStyle flipStyle)
    {
        flipProgress = Mathf.Clamp01(flipProgress);

        shaderFlipProgress = flipProgress;
        shaderFlipStyle = flipStyle == FlipStyle.DramaticHinge ? 1f : 0f;
        shaderFlipRotation = 0f;
        shaderBendAmount = maxBend;
        shaderLiftAmount = 0f;
        shaderShineAmount = 0f;
        shaderHingeCurl = cornerRollCurl;
        shaderHingeTuck = cornerRollTuck;
        shaderHingeFollowDelay = cornerFollowDelay;
        shaderCornerRoll = UsesCornerRoll(flipStyle) ? 1f : 0f;

        // 스크립트는 카드 플립 데모의 타이밍/트랜스폼 움직임을 맞추고, 셰이더는 휘어짐 변형만 맞춥니다.
        ApplyCurrentPropertyBlocks();
    }

    private float GetRevealLiftEnvelope(float progress, FlipStyle flipStyle)
    {
        if (UsesCornerRoll(flipStyle))
        {
            float cornerRise = Smooth01(Mathf.InverseLerp(0.08f, 0.5f, progress));
            float cornerSettle = Smooth01(Mathf.InverseLerp(0.78f, 1f, progress));
            return cornerRise * (1f - cornerSettle);
        }

        float rise = Smooth01(Mathf.InverseLerp(0.04f, 0.42f, progress));
        float settle = Smooth01(Mathf.InverseLerp(0.72f, 1f, progress));
        return rise * (1f - settle);
    }

    private Quaternion GetFlipRotation(float progress, float yAngle, float zTilt, FlipStyle flipStyle)
    {
        if (!UsesCornerRoll(flipStyle))
        {
            return Quaternion.Euler(0f, yAngle, zTilt);
        }

        float rotationProgress = GetRotationProgress(progress, flipStyle);
        float flipAngle = 180f * GetFlipDirection() * rotationProgress;
        float handLiftTilt = revealTiltDegrees * GetHingeFlexEnvelope(progress);
        Quaternion flipRotation = Quaternion.Euler(0f, flipAngle, 0f);
        Quaternion liftTilt = Quaternion.AngleAxis(handLiftTilt + zTilt, Vector3.forward);
        return flipRotation * liftTilt;
    }

    private float GetRotationProgress(float progress, FlipStyle flipStyle)
    {
        if (UsesCornerRoll(flipStyle))
        {
            float silhouetteStart = Mathf.Lerp(0.66f, 0.58f, silhouetteHold);
            float silhouetteEnd = Mathf.Lerp(0.7f, 0.78f, silhouetteHold);

            if (progress < 0.48f)
            {
                return Mathf.Lerp(0f, 0.04f, Smooth01(progress / 0.48f));
            }

            if (progress < silhouetteStart)
            {
                return Mathf.Lerp(0.04f, 0.5f, Smooth01(Mathf.InverseLerp(0.48f, silhouetteStart, progress)));
            }

            if (progress < silhouetteEnd)
            {
                return Mathf.Lerp(0.5f, 0.56f, Smooth01(Mathf.InverseLerp(silhouetteStart, silhouetteEnd, progress)));
            }

            if (progress < 0.92f)
            {
                return Mathf.Lerp(0.56f, 0.98f, Smooth01(Mathf.InverseLerp(silhouetteEnd, 0.92f, progress)));
            }

            return Mathf.Lerp(0.98f, 1f, Smooth01(Mathf.InverseLerp(0.92f, 1f, progress)));
        }

        if (progress < 0.22f)
        {
            return Mathf.Lerp(0f, 0.07f, Smooth01(progress / 0.22f));
        }

        if (progress < 0.76f)
        {
            return Mathf.Lerp(0.07f, 0.92f, Smooth01(Mathf.InverseLerp(0.22f, 0.76f, progress)));
        }

        return Mathf.Lerp(0.92f, 1f, Smooth01(Mathf.InverseLerp(0.76f, 1f, progress)));
    }

    private static float GetHingeFlexEnvelope(float progress)
    {
        float liftIn = Smooth01(Mathf.InverseLerp(0.34f, 0.68f, progress));
        float liftOut = Smooth01(Mathf.InverseLerp(0.84f, 1f, progress));
        return liftIn * (1f - liftOut);
    }

    private float GetFlipDirection()
    {
        return customGripStartPosition.x < 0f ? -1f : 1f;
    }

    private bool UsesCornerRoll(FlipStyle flipStyle)
    {
        return flipStyle == FlipStyle.DramaticHinge ? dramaticUseCornerRoll : normalUseCornerRoll;
    }

    private bool ShouldApplyFaceTexture(float progress, FlipStyle flipStyle, float fallbackProgress)
    {
        return GetRotationProgress(progress, flipStyle) >= 0.5f;
    }

    private void CompleteReveal(Card card)
    {
        isFlipping = false;
        isFaceUp = true;
        ResetMeshDeformation();
        ApplyCardVisual(card, true);
    }

    private void CancelFlip()
    {
#if DOTWEEN
        flipSequence?.Kill(false);
        flipSequence = null;
#endif
        isFlipping = false;
    }

    private void CancelDeal()
    {
        if (dealCoroutine != null)
        {
            StopCoroutine(dealCoroutine);
            dealCoroutine = null;
        }

        isDealAnimating = false;
    }

    private void ApplyCardVisual(Card card, bool faceUp)
    {
        ApplyCardVisual(card, faceUp, false);
    }

    private void ApplyCardVisual(Card card, bool faceUp, bool faceOnBothSides)
    {
        EnsureReady();

        Sprite sprite = faceUp ? CardSpriteCache.Get(card) : null;
        if (faceUp && sprite == null)
        {
            Debug.LogWarning($"[Poker3DCardView] Missing card sprite at Resources/{card.ResourcePath}");
        }

        ApplyFaceBlock(frontBlock, faceUp ? sprite : null, faceUp ? frontFallbackColor : Color.white, faceUp);
        if (faceUp && faceOnBothSides)
        {
            ApplyFaceBlock(backBlock, sprite, frontFallbackColor, true, true);
        }
        else
        {
            ApplyBackBlock(backBlock);
        }

        ApplyEdgeBlock();

        meshRenderer.SetPropertyBlock(frontBlock, 0);
        meshRenderer.SetPropertyBlock(backBlock, 1);
        meshRenderer.SetPropertyBlock(edgeBlock, 2);
    }

    private void ApplyFaceBlock(MaterialPropertyBlock block, Sprite sprite, Color color, bool faceUp, bool mirrorSpriteX = false)
    {
        block.Clear();
        if (sprite != null)
        {
            ApplySpriteBlock(block, sprite, mirrorSpriteX);
            ApplyDeformBlock(block);
            return;
        }

        if (faceUp)
        {
            ApplySolidBlock(block, Texture2D.whiteTexture, color);
        }
        else
        {
            ApplyBackTextureBlock(block);
        }

        ApplyDeformBlock(block);
    }

    private void ApplyBackBlock(MaterialPropertyBlock block)
    {
        block.Clear();
        ApplyBackTextureBlock(block);
        ApplyDeformBlock(block);
    }

    private void ApplyEdgeBlock()
    {
        edgeBlock.Clear();
        edgeBlock.SetTexture(BaseMapProperty, Texture2D.whiteTexture);
        edgeBlock.SetTexture(MainTexProperty, Texture2D.whiteTexture);
        edgeBlock.SetColor(BaseColorProperty, edgeColor);
        edgeBlock.SetColor(ColorProperty, edgeColor);
        ApplyDeformBlock(edgeBlock);
    }

    private void ApplyBackTextureBlock(MaterialPropertyBlock block)
    {
        if (sharedBackSprite != null)
        {
            ApplySpriteBlock(block, sharedBackSprite);
            ApplyDeformBlock(block);
            return;
        }

        ApplySolidBlock(block, sharedBackTexture, backColor);
        ApplyDeformBlock(block);
    }

    private static void ApplySpriteBlock(MaterialPropertyBlock block, Sprite sprite, bool mirrorX = false)
    {
        Rect textureRect = sprite.textureRect;
        Texture2D texture = sprite.texture;
        float scaleX = textureRect.width / texture.width;
        float offsetX = textureRect.x / texture.width;
        if (mirrorX)
        {
            offsetX += scaleX;
            scaleX = -scaleX;
        }

        Vector4 textureScaleOffset = new Vector4(
            scaleX,
            textureRect.height / texture.height,
            offsetX,
            textureRect.y / texture.height);

        block.SetTexture(BaseMapProperty, texture);
        block.SetTexture(MainTexProperty, texture);
        block.SetVector("_BaseMap_ST", textureScaleOffset);
        block.SetVector("_MainTex_ST", textureScaleOffset);
        block.SetColor(BaseColorProperty, Color.white);
        block.SetColor(ColorProperty, Color.white);
    }

    private static void ApplySolidBlock(MaterialPropertyBlock block, Texture texture, Color color)
    {
        block.SetTexture(BaseMapProperty, texture);
        block.SetTexture(MainTexProperty, texture);
        block.SetVector("_BaseMap_ST", new Vector4(1f, 1f, 0f, 0f));
        block.SetVector("_MainTex_ST", new Vector4(1f, 1f, 0f, 0f));
        block.SetColor(BaseColorProperty, color);
        block.SetColor(ColorProperty, color);
    }

    private void ApplyDeformBlock(MaterialPropertyBlock block)
    {
        block.SetFloat(FlipProgressProperty, shaderFlipProgress);
        block.SetFloat(BendAmountProperty, shaderBendAmount);
        block.SetFloat(LiftAmountProperty, shaderLiftAmount);
        block.SetFloat(FlipRotationProperty, shaderFlipRotation);
        block.SetFloat(ShineAmountProperty, shaderShineAmount);
        block.SetFloat(CardWidthProperty, width);
        block.SetFloat(CardHeightProperty, height);
        block.SetFloat(FlipStyleProperty, shaderFlipStyle);
        block.SetFloat(HingeCurlProperty, shaderHingeCurl);
        block.SetFloat(HingeTuckProperty, shaderHingeTuck);
        block.SetFloat(HingeFollowDelayProperty, shaderHingeFollowDelay);
        block.SetFloat(CornerRollProperty, shaderCornerRoll);
        block.SetVector(GripStartProperty, new Vector4(customGripStartPosition.x, customGripStartPosition.y, 0f, 0f));
        block.SetFloat(InwardCurlAmountProperty, inwardCurlAmount);
        block.SetFloat(InwardCurlLiftProperty, inwardCurlLift);
        block.SetFloat(InwardCurlVerticalTuckProperty, inwardCurlVerticalTuck);
        block.SetFloat(DiagonalTiltProperty, diagonalTilt);
        block.SetFloat(CornerRollLiftProperty, cornerRollLift);
        block.SetFloat(CornerRollCurlProperty, cornerRollCurl);
        block.SetFloat(CornerRollTuckProperty, cornerRollTuck);
        block.SetFloat(SettleFlexProperty, settleFlex);
    }

    private void ApplyCurrentPropertyBlocks()
    {
        ApplyDeformBlock(frontBlock);
        ApplyDeformBlock(backBlock);
        ApplyDeformBlock(edgeBlock);
        meshRenderer.SetPropertyBlock(frontBlock, 0);
        meshRenderer.SetPropertyBlock(backBlock, 1);
        meshRenderer.SetPropertyBlock(edgeBlock, 2);
    }

    private void ResetShaderDeformation()
    {
        shaderFlipProgress = 0f;
        shaderBendAmount = 0f;
        shaderLiftAmount = 0f;
        shaderFlipRotation = 0f;
        shaderShineAmount = 0f;
        shaderFlipStyle = 0f;
        shaderHingeCurl = cornerRollCurl;
        shaderHingeTuck = cornerRollTuck;
        shaderHingeFollowDelay = cornerFollowDelay;
        shaderCornerRoll = 0f;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void OnDestroy()
    {
        CancelDeal();
        CancelFlip();

        cardMesh = null;
    }
}
