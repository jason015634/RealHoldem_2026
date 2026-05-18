using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

// 카드 뒤집기 데모에서 잡는 위치를 지정하는 값입니다.
public enum CardGripCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    MiddleLeft,
    MiddleRight,
    Custom
}

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
// 리버 카드가 들렸다가 휘어지며 뒤집히는 3D 카드 플립 데모입니다.
// 세그먼트 메시를 직접 만들고 DOTween으로 단계별 회전/휘어짐/리프트 애니메이션을 재생합니다.
public sealed class CardFlipDemo : MonoBehaviour
{
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
    [SerializeField] private float width = 0.5f;
    [SerializeField] private float height = 0.72f;
    [SerializeField] private float thickness = 0.018f;
    [SerializeField] private int segments = 24;

    [Header("Flip Timing")]
    [FormerlySerializedAs("flipDuration")]
    [SerializeField] private float totalDuration = 0.72f;
    [Range(0.05f, 0.85f)]
    [SerializeField] private float peekDurationRatio = 0.28f;
    [Range(0.05f, 0.85f)]
    [SerializeField] private float snapDurationRatio = 0.38f;
    [Range(0.05f, 0.85f)]
    [SerializeField] private float settleDurationRatio = 0.34f;
    [SerializeField] private float peekHoldTime = 0.06f;

    [Header("Flip Progress")]
    [Range(0.05f, 0.8f)]
    [SerializeField] private float peekProgress = 0.2f;
    [Range(0.2f, 0.95f)]
    [SerializeField] private float snapEndProgress = 0.72f;

    [Header("Flip Shape")]
    [SerializeField] private float peekLiftHeight = 0.14f;
    [FormerlySerializedAs("liftHeight")]
    [SerializeField] private float snapLiftHeight = 0.26f;
    [SerializeField] private float maxBend = 0.045f;

    [Header("Inner Curl")]
    [SerializeField] private float inwardCurlAmount = 0.1f;
    [SerializeField] private float inwardCurlLift = 0.07f;
    [SerializeField] private float inwardCurlVerticalTuck = 0.035f;

    [Tooltip("Normalized card point where the curl starts. (-1,-1)=bottom-left, (1,1)=top-right, (0,0)=center.")]
    [SerializeField] private Vector2 customGripStartPosition = new Vector2(1f, 0.65f);
    [SerializeField] private float diagonalTilt = 14f;

    [Header("Hinge Flip")]
    [SerializeField] private bool useCornerRoll;
    [SerializeField] private float cornerRollLift = 0.14f;
    [SerializeField] private float cornerRollCurl = 0.09f;
    [SerializeField] private float cornerRollTuck = 0.05f;
    [SerializeField] private float cornerFollowDelay = 0.22f;
    [SerializeField] private float silhouetteHold = 0.16f;
    [SerializeField] private float settleFlex = 0.018f;

    [Header("Flip Easing")]
    [SerializeField] private AnimationCurve peekEase = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.25f),
        new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private AnimationCurve snapEase = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(0.35f, 0.15f, 0.2f, 2.4f),
        new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private AnimationCurve settleEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Card Materials")]
    [SerializeField] private Material cardMaterial;
    [SerializeField] private Color backColor = new Color(0.18f, 0.28f, 0.5f, 1f);
    [SerializeField] private Color frontColor = Color.white;
    [SerializeField] private Color edgeColor = new Color(0.82f, 0.82f, 0.76f, 1f);
    [SerializeField] private bool useCardSprites = true;
    [SerializeField] private string backSpriteResourcePath = "Sprites/Cards/back";
    [SerializeField] private string frontSpriteResourcePath = "Sprites/Cards/Diamond_King";

    [Header("River Reveal Motion")]
    [SerializeField] private bool autoPlayOnStart = true;
    [SerializeField] private float autoPlayDelay = 0.45f;
    [SerializeField] private Vector3 revealLiftOffset = new Vector3(0.12f, 0.04f, -0.055f);
    [SerializeField] private float revealTiltDegrees = -7f;

    [Header("Community Test Layout")]
    [SerializeField] private bool buildCommunityLayout = true;
    [SerializeField] private string[] staticCommunitySpritePaths =
    {
        "Sprites/Cards/Club_Ace",
        "Sprites/Cards/Club_8",
        "Sprites/Cards/Spade_Jack",
        "Sprites/Cards/Spade_5"
    };
    [SerializeField] private float cardSpacing = 0.44f;
    [SerializeField] private Vector3 layoutCenterOffset = Vector3.zero;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh cardMesh;
    private Material frontMaterial;
    private Material backMaterial;
    private Material edgeMaterial;
    private Sequence flipSequence;
    private Vector3 riverSlotPosition;
    private bool isFlipping;
    private GameObject[] generatedLayoutObjects;
    private float shaderFlipProgress;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        BuildCardMesh();
        EnsureMaterials();
        //BuildCommunityReferenceLayout();
        ResetToBackVisual();
    }

    private void Start()
    {
        if (autoPlayOnStart)
        {
            DOVirtual.DelayedCall(Mathf.Max(0f, autoPlayDelay), PlayRiverReveal);
        }
    }

    private void OnValidate()
    {
        width = Mathf.Max(0.05f, width);
        height = Mathf.Max(0.05f, height);
        thickness = Mathf.Max(0.001f, thickness);
        segments = Mathf.Clamp(segments, 2, 64);
        totalDuration = Mathf.Max(0.1f, totalDuration);
        peekDurationRatio = Mathf.Max(0.01f, peekDurationRatio);
        snapDurationRatio = Mathf.Max(0.01f, snapDurationRatio);
        settleDurationRatio = Mathf.Max(0.01f, settleDurationRatio);
        peekHoldTime = Mathf.Max(0f, peekHoldTime);
        peekProgress = Mathf.Clamp(peekProgress, 0.05f, 0.8f);
        snapEndProgress = Mathf.Clamp(snapEndProgress, peekProgress + 0.05f, 0.95f);
        peekLiftHeight = Mathf.Max(0f, peekLiftHeight);
        snapLiftHeight = Mathf.Max(0f, snapLiftHeight);
        maxBend = Mathf.Max(0f, maxBend);
        inwardCurlAmount = Mathf.Max(0f, inwardCurlAmount);
        inwardCurlLift = Mathf.Max(0f, inwardCurlLift);
        inwardCurlVerticalTuck = Mathf.Max(0f, inwardCurlVerticalTuck);
        cornerRollLift = Mathf.Max(0f, cornerRollLift);
        cornerRollCurl = Mathf.Max(0f, cornerRollCurl);
        cornerRollTuck = Mathf.Max(0f, cornerRollTuck);
        cornerFollowDelay = Mathf.Clamp01(cornerFollowDelay);
        silhouetteHold = Mathf.Clamp01(silhouetteHold);
        settleFlex = Mathf.Max(0f, settleFlex);
        customGripStartPosition = new Vector2(
            Mathf.Clamp(customGripStartPosition.x, -1f, 1f),
            Mathf.Clamp(customGripStartPosition.y, -1f, 1f));
        diagonalTilt = Mathf.Clamp(diagonalTilt, -45f, 45f);
        autoPlayDelay = Mathf.Max(0f, autoPlayDelay);
        cardSpacing = Mathf.Max(0.1f, cardSpacing);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            PlayRiverReveal();
        }
    }

    [ContextMenu("Play River Reveal")]
    public void PlayRiverReveal()
    {
        if (isFlipping)
        {
            return;
        }

        ResetToBackVisual();
        PlayFlip();
    }

    [ContextMenu("Play Flip")]
    public void PlayFlip()
    {
        if (isFlipping)
        {
            return;
        }

        isFlipping = true;

        Vector3 startPosition = riverSlotPosition;
        Quaternion startRotation = Quaternion.identity;
        float progress = 0f;

        flipSequence?.Kill(false);
        flipSequence = DOTween.Sequence();

        float remainingDuration = Mathf.Max(0.05f, totalDuration - peekHoldTime);
        float ratioSum = Mathf.Max(0.01f, peekDurationRatio + snapDurationRatio + settleDurationRatio);
        float peekDuration = remainingDuration * (peekDurationRatio / ratioSum);
        float snapDuration = remainingDuration * (snapDurationRatio / ratioSum);
        float settleDuration = remainingDuration * (settleDurationRatio / ratioSum);

        AppendRotationTween(
            peekProgress,
            peekDuration,
            peekEase,
            startPosition,
            startRotation,
            () => progress,
            value => progress = value);

        if (peekHoldTime > 0f)
        {
            flipSequence.AppendInterval(peekHoldTime);
        }

        AppendRotationTween(
            snapEndProgress,
            snapDuration,
            snapEase,
            startPosition,
            startRotation,
            () => progress,
            value => progress = value);

        AppendRotationTween(
            1f,
            settleDuration,
            settleEase,
            startPosition,
            startRotation,
            () => progress,
            value => progress = value);

        flipSequence.OnComplete(() =>
        {
            transform.localPosition = startPosition;
            transform.localRotation = Quaternion.Euler(0f, 180f * GetFlipDirection(), 0f);
            ResetMeshDeformation();
            isFlipping = false;
        });

        flipSequence.OnKill(() =>
        {
            if (isFlipping)
            {
                ResetToBackVisual();
                isFlipping = false;
            }
        });
    }

    private void AppendRotationTween(
        float targetProgress,
        float duration,
        AnimationCurve ease,
        Vector3 startPosition,
        Quaternion startRotation,
        System.Func<float> getProgress,
        System.Action<float> setProgress)
    {
        DG.Tweening.Core.DOGetter<float> progressGetter = () => getProgress();
        DG.Tweening.Core.DOSetter<float> progressSetter = value =>
            {
                setProgress(value);
                float currentProgress = getProgress();
                float liftEnvelope = GetRevealLiftEnvelope(currentProgress);
                float yAngle = Mathf.Lerp(0f, 180f * GetFlipDirection(), GetRotationProgress(currentProgress));
                float zTilt = revealTiltDegrees * liftEnvelope;

                ApplyBend(currentProgress);
                transform.localPosition = startPosition + revealLiftOffset * liftEnvelope;
                transform.localRotation = startRotation * GetFlipRotation(currentProgress, yAngle, zTilt);
            };

        flipSequence.Append(DOTween
            .To(progressGetter, progressSetter, targetProgress, Mathf.Max(0.01f, duration))
            .SetEase(ease));
    }

    private Quaternion GetFlipRotation(float progress, float yAngle, float zTilt)
    {
        if (!useCornerRoll)
        {
            return Quaternion.Euler(0f, yAngle, zTilt);
        }

        float rotationProgress = GetRotationProgress(progress);
        float flipAngle = 180f * GetFlipDirection() * rotationProgress;
        float handLiftTilt = revealTiltDegrees * GetHingeFlexEnvelope(progress);
        Quaternion flipRotation = Quaternion.Euler(0f, flipAngle, 0f);
        Quaternion liftTilt = Quaternion.AngleAxis(handLiftTilt + zTilt, Vector3.forward);
        return flipRotation * liftTilt;
    }

    private void BuildCardMesh()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (cardMesh != null)
        {
            cardMesh.Clear();
        }
        else
        {
            cardMesh = new Mesh();
            cardMesh.name = "Two Sided 3D Poker Card";
            cardMesh.MarkDynamic();
        }

        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float halfThickness = thickness * 0.5f;
        int gridSegments = Mathf.Max(2, segments);
        int gridStride = gridSegments + 1;

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> frontTriangles = new List<int>();
        List<int> backTriangles = new List<int>();
        List<int> edgeTriangles = new List<int>();

        int AddVertex(Vector3 vertex, Vector2 uv)
        {
            int index = vertices.Count;
            vertices.Add(vertex);
            uvs.Add(uv);
            return index;
        }

        void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);
        }

        void AddEdgeQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            AddVertex(a, new Vector2(0f, 0f));
            AddVertex(b, new Vector2(1f, 0f));
            AddVertex(c, new Vector2(1f, 1f));
            AddVertex(d, new Vector2(0f, 1f));
            AddQuad(edgeTriangles, start, start + 1, start + 2, start + 3);
        }

        int frontStart = vertices.Count;
        for (int y = 0; y <= gridSegments; y++)
        {
            float v = y / (float)gridSegments;
            float yPosition = Mathf.Lerp(-halfHeight, halfHeight, v);
            for (int x = 0; x <= gridSegments; x++)
            {
                float u = x / (float)gridSegments;
                float xPosition = Mathf.Lerp(-halfWidth, halfWidth, u);
                AddVertex(new Vector3(xPosition, yPosition, halfThickness), new Vector2(1f - u, v));
            }
        }

        for (int y = 0; y < gridSegments; y++)
        {
            for (int x = 0; x < gridSegments; x++)
            {
                int bottomLeft = frontStart + y * gridStride + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + gridStride;
                int topRight = topLeft + 1;
                AddQuad(frontTriangles, bottomLeft, bottomRight, topRight, topLeft);
            }
        }

        int backStart = vertices.Count;
        for (int y = 0; y <= gridSegments; y++)
        {
            float v = y / (float)gridSegments;
            float yPosition = Mathf.Lerp(-halfHeight, halfHeight, v);
            for (int x = 0; x <= gridSegments; x++)
            {
                float u = x / (float)gridSegments;
                float xPosition = Mathf.Lerp(-halfWidth, halfWidth, u);
                AddVertex(new Vector3(xPosition, yPosition, -halfThickness), new Vector2(u, v));
            }
        }

        for (int y = 0; y < gridSegments; y++)
        {
            for (int x = 0; x < gridSegments; x++)
            {
                int bottomLeft = backStart + y * gridStride + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + gridStride;
                int topRight = topLeft + 1;
                AddQuad(backTriangles, bottomLeft, topLeft, topRight, bottomRight);
            }
        }

        for (int i = 0; i < gridSegments; i++)
        {
            float u0 = i / (float)gridSegments;
            float u1 = (i + 1) / (float)gridSegments;
            float x0 = Mathf.Lerp(-halfWidth, halfWidth, u0);
            float x1 = Mathf.Lerp(-halfWidth, halfWidth, u1);
            float y0 = Mathf.Lerp(-halfHeight, halfHeight, u0);
            float y1 = Mathf.Lerp(-halfHeight, halfHeight, u1);

            AddEdgeQuad(
                new Vector3(x0, -halfHeight, -halfThickness),
                new Vector3(x1, -halfHeight, -halfThickness),
                new Vector3(x1, -halfHeight, halfThickness),
                new Vector3(x0, -halfHeight, halfThickness));

            AddEdgeQuad(
                new Vector3(x0, halfHeight, halfThickness),
                new Vector3(x1, halfHeight, halfThickness),
                new Vector3(x1, halfHeight, -halfThickness),
                new Vector3(x0, halfHeight, -halfThickness));

            AddEdgeQuad(
                new Vector3(-halfWidth, y0, halfThickness),
                new Vector3(-halfWidth, y1, halfThickness),
                new Vector3(-halfWidth, y1, -halfThickness),
                new Vector3(-halfWidth, y0, -halfThickness));

            AddEdgeQuad(
                new Vector3(halfWidth, y0, -halfThickness),
                new Vector3(halfWidth, y1, -halfThickness),
                new Vector3(halfWidth, y1, halfThickness),
                new Vector3(halfWidth, y0, halfThickness));
        }

        cardMesh.vertices = vertices.ToArray();
        cardMesh.uv = uvs.ToArray();
        cardMesh.subMeshCount = 3;
        cardMesh.SetTriangles(frontTriangles.ToArray(), 0);
        cardMesh.SetTriangles(backTriangles.ToArray(), 1);
        cardMesh.SetTriangles(edgeTriangles.ToArray(), 2);
        cardMesh.RecalculateNormals();
        ExpandCardMeshBounds();
        meshFilter.sharedMesh = cardMesh;
    }

    private void EnsureMaterials()
    {
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        frontMaterial = CreateCardMaterial("River Card Front", frontSpriteResourcePath, frontColor);
        backMaterial = CreateCardMaterial("River Card Back", backSpriteResourcePath, backColor);
        edgeMaterial = CreateSolidMaterial("River Card Edge", edgeColor);
        meshRenderer.sharedMaterials = new[] { frontMaterial, backMaterial, edgeMaterial };
        ResetShaderDeformation();
    }

    private void BuildCommunityReferenceLayout()
    {
        if (!buildCommunityLayout || !Application.isPlaying)
        {
            riverSlotPosition = transform.localPosition;
            return;
        }

        DestroyGeneratedLayoutObjects();

        Vector3 layoutCenter = transform.localPosition + layoutCenterOffset;
        riverSlotPosition = layoutCenter + Vector3.right * (cardSpacing * 2f);
        transform.localPosition = riverSlotPosition;

        int staticCardCount = Mathf.Min(4, staticCommunitySpritePaths != null ? staticCommunitySpritePaths.Length : 0);
        generatedLayoutObjects = new GameObject[staticCardCount];
        for (int i = 0; i < staticCardCount; i++)
        {
            Vector3 position = layoutCenter + Vector3.right * ((i - 2f) * cardSpacing);
            generatedLayoutObjects[i] = CreateStaticCard($"CommunityCard{i + 1}", staticCommunitySpritePaths[i], position);
        }
    }

    private GameObject CreateStaticCard(string objectName, string spritePath, Vector3 localPosition)
    {
        GameObject card = new GameObject(objectName);
        card.transform.SetParent(transform.parent, false);
        card.transform.localPosition = localPosition;
        card.transform.localRotation = Quaternion.identity;

        MeshFilter filter = card.AddComponent<MeshFilter>();
        MeshRenderer renderer = card.AddComponent<MeshRenderer>();
        filter.sharedMesh = CreateStaticCardMesh($"{objectName} Mesh");

        Material front = CreateCardMaterial($"{objectName} Front", spritePath, frontColor);
        Material edge = CreateSolidMaterial($"{objectName} Edge", edgeColor);
        renderer.sharedMaterials = new[] { front, edge };
        return card;
    }

    private Mesh CreateStaticCardMesh(string meshName)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float halfThickness = thickness * 0.5f;

        Mesh mesh = new Mesh
        {
            name = meshName,
            vertices = new[]
            {
                new Vector3(-halfWidth, -halfHeight, -halfThickness),
                new Vector3(-halfWidth, halfHeight, -halfThickness),
                new Vector3(halfWidth, halfHeight, -halfThickness),
                new Vector3(halfWidth, -halfHeight, -halfThickness),
                new Vector3(-halfWidth, -halfHeight, halfThickness),
                new Vector3(halfWidth, -halfHeight, halfThickness),
                new Vector3(halfWidth, halfHeight, halfThickness),
                new Vector3(-halfWidth, halfHeight, halfThickness)
            },
            uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            },
            subMeshCount = 2
        };

        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        mesh.SetTriangles(new[] { 4, 5, 6, 4, 6, 7 }, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void ResetToBackVisual()
    {
        transform.localPosition = riverSlotPosition;
        transform.localRotation = Quaternion.identity;
        ResetMeshDeformation();
    }

    private float GetRevealLiftEnvelope(float progress)
    {
        if (useCornerRoll)
        {
            float cornerRise = Smooth01(Mathf.InverseLerp(0.08f, 0.5f, progress));
            float cornerSettle = Smooth01(Mathf.InverseLerp(0.78f, 1f, progress));
            return cornerRise * (1f - cornerSettle);
        }

        float rise = Smooth01(Mathf.InverseLerp(0.04f, 0.42f, progress));
        float settle = Smooth01(Mathf.InverseLerp(0.72f, 1f, progress));
        return rise * (1f - settle);
    }

    private void ApplyBend(float progress)
    {
        ApplyShaderDeformation(progress);
    }

    private void ResetMeshDeformation()
    {
        ResetShaderDeformation();
    }

    private void ApplyShaderDeformation(float progress)
    {
        shaderFlipProgress = Mathf.Clamp01(progress);
        ApplyShaderProperties(frontMaterial);
        ApplyShaderProperties(backMaterial);
        ApplyShaderProperties(edgeMaterial);
    }

    private void ResetShaderDeformation()
    {
        shaderFlipProgress = 0f;
        ApplyShaderProperties(frontMaterial);
        ApplyShaderProperties(backMaterial);
        ApplyShaderProperties(edgeMaterial);
    }

    private void ApplyShaderProperties(Material material)
    {
        if (material == null)
        {
            return;
        }

        SetFloatIfPresent(material, FlipProgressProperty, shaderFlipProgress);
        SetFloatIfPresent(material, BendAmountProperty, maxBend);
        SetFloatIfPresent(material, LiftAmountProperty, 0f);
        SetFloatIfPresent(material, FlipRotationProperty, 0f);
        SetFloatIfPresent(material, ShineAmountProperty, 0f);
        SetFloatIfPresent(material, CardWidthProperty, width);
        SetFloatIfPresent(material, CardHeightProperty, height);
        SetFloatIfPresent(material, FlipStyleProperty, useCornerRoll ? 1f : 0f);
        SetFloatIfPresent(material, HingeCurlProperty, cornerRollCurl);
        SetFloatIfPresent(material, HingeTuckProperty, cornerRollTuck);
        SetFloatIfPresent(material, HingeFollowDelayProperty, cornerFollowDelay);
        SetFloatIfPresent(material, CornerRollProperty, useCornerRoll ? 1f : 0f);
        SetVectorIfPresent(material, GripStartProperty, new Vector4(customGripStartPosition.x, customGripStartPosition.y, 0f, 0f));
        SetFloatIfPresent(material, InwardCurlAmountProperty, inwardCurlAmount);
        SetFloatIfPresent(material, InwardCurlLiftProperty, inwardCurlLift);
        SetFloatIfPresent(material, InwardCurlVerticalTuckProperty, inwardCurlVerticalTuck);
        SetFloatIfPresent(material, DiagonalTiltProperty, diagonalTilt);
        SetFloatIfPresent(material, CornerRollLiftProperty, cornerRollLift);
        SetFloatIfPresent(material, CornerRollCurlProperty, cornerRollCurl);
        SetFloatIfPresent(material, CornerRollTuckProperty, cornerRollTuck);
        SetFloatIfPresent(material, SettleFlexProperty, settleFlex);
    }

    private void ExpandCardMeshBounds()
    {
        if (cardMesh == null)
        {
            return;
        }

        float boundsLift = revealLiftOffset.magnitude + cornerRollLift + inwardCurlLift;
        float boundsBend = maxBend + cornerRollCurl + settleFlex;
        float boundsDepth = Mathf.Max(width, height) * 2.5f + boundsLift + boundsBend;
        cardMesh.bounds = new Bounds(Vector3.zero, new Vector3(width * 3f, height * 3f, boundsDepth));
    }

    private static float GetHingeFlexEnvelope(float progress)
    {
        float liftIn = Smooth01(Mathf.InverseLerp(0.34f, 0.68f, progress));
        float liftOut = Smooth01(Mathf.InverseLerp(0.84f, 1f, progress));
        return liftIn * (1f - liftOut);
    }

    private float GetRotationProgress(float progress)
    {
        if (useCornerRoll)
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

    private float GetFlipDirection()
    {
        return customGripStartPosition.x < 0f ? -1f : 1f;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private Material CreateCardMaterial(string materialName, string spritePath, Color fallbackColor)
    {
        if (!useCardSprites)
        {
            return CreateSolidMaterial(materialName, fallbackColor);
        }

        Sprite sprite = Resources.Load<Sprite>(spritePath);
        if (sprite == null)
        {
            Debug.LogWarning($"[CardFlipDemo] Missing card sprite at Resources/{spritePath}");
            return CreateSolidMaterial(materialName, fallbackColor);
        }

        Material material = CreateSolidMaterial(materialName, Color.white);
        ApplyMaterialVisual(material, sprite.texture, GetSpriteScaleOffset(sprite), Color.white);
        return material;
    }

    private static Material CreateSolidMaterial(string materialName, Color color)
    {
        Material material = new Material(FindCardShader())
        {
            name = materialName
        };

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", 2f);
        }

        ApplyMaterialVisual(material, Texture2D.whiteTexture, new Vector4(1f, 1f, 0f, 0f), color);
        return material;
    }

    private static Vector4 GetSpriteScaleOffset(Sprite sprite)
    {
        Rect textureRect = sprite.textureRect;
        Texture2D texture = sprite.texture;
        return new Vector4(
            textureRect.width / texture.width,
            textureRect.height / texture.height,
            textureRect.x / texture.width,
            textureRect.y / texture.height);
    }

    private static void ApplyMaterialVisual(Material material, Texture texture, Vector4 scaleOffset, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(BaseMapProperty))
        {
            material.SetTexture(BaseMapProperty, texture);
            material.SetVector("_BaseMap_ST", scaleOffset);
            material.SetColor(BaseColorProperty, color);
        }

        if (material.HasProperty(MainTexProperty))
        {
            material.SetTexture(MainTexProperty, texture);
            material.SetVector("_MainTex_ST", scaleOffset);
            material.SetColor(ColorProperty, color);
        }

        material.color = color;
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }

    private static void SetVectorIfPresent(Material material, string property, Vector4 value)
    {
        if (material.HasProperty(property))
        {
            material.SetVector(property, value);
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

        shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
        {
            return shader;
        }

        return Shader.Find("Standard");
    }

    private void DestroyGeneratedLayoutObjects()
    {
        if (generatedLayoutObjects == null)
        {
            return;
        }

        foreach (GameObject generatedObject in generatedLayoutObjects)
        {
            if (generatedObject == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(generatedObject);
            }
            else
            {
                DestroyImmediate(generatedObject);
            }
        }

        generatedLayoutObjects = null;
    }

    private void OnDestroy()
    {
        flipSequence?.Kill(false);
        DestroyGeneratedLayoutObjects();

        if (Application.isPlaying && cardMesh != null)
        {
            Destroy(cardMesh);
        }
    }
}
