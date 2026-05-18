using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class CornerPeelCardFlipTest : MonoBehaviour
{
    private const string BaseMapProperty = "_BaseMap";
    private const string MainTexProperty = "_MainTex";
    private const string BaseColorProperty = "_BaseColor";
    private const string ColorProperty = "_Color";

    [Header("Timing")]
    [SerializeField] private float duration = 0.72f;
    [SerializeField] private float settleDuration = 0.22f;
    [SerializeField] private float startDelay = 0f;

    [Header("Corner Peel")]
    [SerializeField] private float liftHeight = 0.28f;
    [SerializeField] private float curlAmount = 0.12f;
    [SerializeField] private float flipAngle = 180f;
    [SerializeField] private float overshootAmount = 12f;

    [Header("Card Mesh")]
    [SerializeField] private float width = 0.5f;
    [SerializeField] private float height = 0.72f;
    [SerializeField] private float thickness = 0.018f;
    [SerializeField] private int segments = 28;

    [Header("Sprites")]
    [SerializeField] private string backSpriteResourcePath = "Sprites/Cards/back";
    [SerializeField] private string frontSpriteResourcePath = "Sprites/Cards/Heart_Ace";
    [SerializeField] private Color edgeColor = new Color(0.82f, 0.82f, 0.76f, 1f);

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh cardMesh;
    private Vector3[] flatVertices;
    private Vector3[] deformedVertices;
    private Material frontMaterial;
    private Material backMaterial;
    private Material edgeMaterial;
    private Sequence flipSequence;
    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;
    private bool hasStartPose;
    private bool isPlayingFlip;

    private void Awake()
    {
        CacheStartPose();
        EnsureReady();
        ResetToBack();
    }

    private void OnValidate()
    {
        duration = Mathf.Max(0.05f, duration);
        settleDuration = Mathf.Max(0.01f, settleDuration);
        startDelay = Mathf.Max(0f, startDelay);
        liftHeight = Mathf.Max(0f, liftHeight);
        curlAmount = Mathf.Max(0f, curlAmount);
        flipAngle = Mathf.Max(1f, flipAngle);
        overshootAmount = Mathf.Max(0f, overshootAmount);
        width = Mathf.Max(0.05f, width);
        height = Mathf.Max(0.05f, height);
        thickness = Mathf.Max(0.001f, thickness);
        segments = Mathf.Clamp(segments, 4, 64);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            Play();
        }
    }

    private void OnDisable()
    {
        flipSequence?.Kill(false);
        isPlayingFlip = false;
    }

    private void OnDestroy()
    {
        flipSequence?.Kill(false);
        DestroyRuntimeObject(cardMesh);
        DestroyRuntimeObject(frontMaterial);
        DestroyRuntimeObject(backMaterial);
        DestroyRuntimeObject(edgeMaterial);
    }

    [ContextMenu("Play Corner Peel Flip")]
    public void Play()
    {
        EnsureReady();

        flipSequence?.Kill(false);
        isPlayingFlip = true;

        ResetToBack();

        float progress = 0f;
        float peelDuration = duration * 0.36f;
        float snapDuration = duration * 0.64f;

        DG.Tweening.Core.DOGetter<float> getter = () => progress;
        DG.Tweening.Core.DOSetter<float> setter = value =>
        {
            progress = value;
            ApplyPose(progress);
        };

        flipSequence = DOTween.Sequence();

        if (startDelay > 0f)
        {
            flipSequence.AppendInterval(startDelay);
        }

        flipSequence
            .Append(DOTween.To(getter, setter, 0.36f, peelDuration).SetEase(Ease.OutSine))
            .Append(DOTween.To(getter, setter, 0.84f, snapDuration).SetEase(Ease.InOutCubic))
            .Append(DOTween.To(getter, setter, 1f, settleDuration).SetEase(Ease.OutBack))
            .OnComplete(() =>
            {
                ApplyFinalFrontPose();
                isPlayingFlip = false;
            })
            .OnKill(() =>
            {
                if (isPlayingFlip)
                {
                    ResetToBack();
                    isPlayingFlip = false;
                }
            });
    }

    [ContextMenu("Reset To Back")]
    public void ResetToBack()
    {
        CacheStartPoseIfNeeded();
        transform.localPosition = startLocalPosition;
        transform.localRotation = startLocalRotation;
        ApplyFixedFaceMaterials();
        ResetMeshDeformation();
    }

    private void ApplyFinalFrontPose()
    {
        transform.localPosition = startLocalPosition;
        transform.localRotation = startLocalRotation * Quaternion.AngleAxis(flipAngle, GetDiagonalFlipAxis());
        ApplyFixedFaceMaterials();
        ResetMeshDeformation();
    }

    private void ApplyPose(float progress)
    {
        progress = Mathf.Clamp01(progress);

        float rotationProgress = GetRotationProgress(progress);
        float overshoot = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.68f, 1f, progress)) * Mathf.PI) * overshootAmount;
        float diagonalFlipAngle = flipAngle * rotationProgress + overshoot;
        float liftTilt = Mathf.Lerp(0f, -10f, GetWholeCardLift(progress));
        float handTwist = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.08f, 0.68f, progress)) * Mathf.PI) * -13f;
        float releaseTwist = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.45f, 0.95f, progress)) * Mathf.PI) * 7f;

        transform.localPosition = startLocalPosition;
        transform.localRotation = startLocalRotation
            * Quaternion.AngleAxis(diagonalFlipAngle, GetDiagonalFlipAxis())
            * Quaternion.AngleAxis(handTwist + releaseTwist, Vector3.forward)
            * Quaternion.AngleAxis(liftTilt, Vector3.right);

        ApplyCornerPeelDeformation(progress);
    }

    private Vector3 GetDiagonalFlipAxis()
    {
        // Axis perpendicular to the hand path from bottom-left to top-right.
        return new Vector3(height, -width, 0f).normalized;
    }

    private float GetRotationProgress(float progress)
    {
        if (progress < 0.36f)
        {
            return Mathf.Lerp(0f, 0.035f, Smooth01(progress / 0.36f));
        }

        if (progress < 0.72f)
        {
            return Mathf.Lerp(0.035f, 0.96f, Smooth01(Mathf.InverseLerp(0.36f, 0.72f, progress)));
        }

        return Mathf.Lerp(0.96f, 0f, Smooth01(Mathf.InverseLerp(0.72f, 1f, progress)));
    }

    private float GetWholeCardLift(float progress)
    {
        float liftIn = Smooth01(Mathf.InverseLerp(0.42f, 0.64f, progress));
        float liftOut = Smooth01(Mathf.InverseLerp(0.82f, 1f, progress));
        return liftIn * (1f - liftOut);
    }

    private void ApplyCornerPeelDeformation(float progress)
    {
        if (cardMesh == null || flatVertices == null || deformedVertices == null)
        {
            return;
        }

        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        Vector2 grip = new Vector2(-1f, -1f);
        Vector2 fixedCorner = new Vector2(1f, 1f);
        Vector2 diagonal = (fixedCorner - grip).normalized;
        float diagonalLength = Vector2.Distance(grip, fixedCorner);

        float prePeel = Smooth01(Mathf.InverseLerp(0.02f, 0.34f, progress));
        float releaseFixedCorner = Smooth01(Mathf.InverseLerp(0.42f, 0.62f, progress));
        float settle = Smooth01(Mathf.InverseLerp(0.78f, 1f, progress));
        float wholeLift = GetWholeCardLift(progress);
        float snapFlex = Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.48f, 0.9f, progress)) * Mathf.PI);
        float centerToCornerRoll = Smooth01(Mathf.InverseLerp(0.02f, 0.3f, progress))
            * (1f - Smooth01(Mathf.InverseLerp(0.62f, 0.92f, progress)));

        for (int i = 0; i < flatVertices.Length; i++)
        {
            Vector3 flat = flatVertices[i];
            Vector2 point = new Vector2(flat.x / halfWidth, flat.y / halfHeight);
            Vector2 fromGrip = point - grip;
            float along = Mathf.Clamp01(Vector2.Dot(fromGrip, diagonal) / diagonalLength);
            float cross = fromGrip.x * diagonal.y - fromGrip.y * diagonal.x;
            float distanceFromGrip = fromGrip.magnitude;
            float distanceFromFixed = Vector2.Distance(point, fixedCorner);

            float delayedLift = Smooth01(Mathf.InverseLerp(along * 0.34f, along * 0.34f + 0.26f, progress));
            float gripInfluence = 1f - Smooth01(Mathf.InverseLerp(0.2f, 1.85f, distanceFromGrip));
            float fixedInfluence = 1f - Smooth01(Mathf.InverseLerp(0.05f, 1.25f, distanceFromFixed));
            float fixedPin = Mathf.Lerp(1f - fixedInfluence, 1f, releaseFixedCorner);
            float diagonalWave = Mathf.Sin(along * Mathf.PI);
            float curlRidge = Mathf.Clamp01(1f - Mathf.Abs(cross) * 0.9f);
            float handPathBand = Mathf.Clamp01(1f - Mathf.Abs(cross) * 1.35f);
            float rollFromCenter = Mathf.Exp(-Mathf.Pow((along - Mathf.Lerp(0.5f, 0f, prePeel)) * 4.2f, 2f));
            float travelingFold = Mathf.Exp(-Mathf.Pow((along - progress * 1.05f) * 3.1f, 2f));

            float cornerLift = liftHeight * delayedLift * (1f - settle);
            cornerLift *= Mathf.Lerp(0.08f, 1.22f, gripInfluence) * fixedPin;

            float curl = curlAmount * prePeel * (1f - settle) * fixedPin;
            float curlNearHand = curl * Mathf.Lerp(0.28f, 1.18f, gripInfluence);
            float diagonalCurl = curl * diagonalWave * curlRidge;
            float foldCurl = curlAmount * (1.05f * travelingFold + 1.2f * rollFromCenter)
                * handPathBand
                * (1f - settle)
                * fixedPin;

            Vector3 bent = flat;
            bent.z -= cornerLift;
            bent.z -= curlNearHand;
            bent.z -= diagonalCurl;
            bent.z -= foldCurl;
            bent.z -= liftHeight * 0.45f * wholeLift;
            bent.z -= curlAmount * 0.55f * snapFlex * diagonalWave;

            Vector3 towardGrip = new Vector3(-halfWidth, -halfHeight, 0f).normalized;
            Vector3 towardCenter = new Vector3(-point.x * halfWidth, -point.y * halfHeight, 0f).normalized;
            bent += towardCenter * (curlAmount * 0.12f * prePeel * gripInfluence * fixedPin);
            bent += towardGrip * (curlAmount * 0.16f * centerToCornerRoll * rollFromCenter * handPathBand * fixedPin);
            bent.x += cross * curlAmount * 0.06f * prePeel * (1f - settle);
            bent.y += Mathf.Abs(cross) * curlAmount * 0.04f * prePeel * gripInfluence * (1f - settle);

            deformedVertices[i] = bent;
        }

        cardMesh.vertices = deformedVertices;
        cardMesh.RecalculateNormals();
        cardMesh.RecalculateBounds();
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

        if (cardMesh == null)
        {
            BuildCardMesh();
        }

        if (frontMaterial == null || backMaterial == null || edgeMaterial == null)
        {
            frontMaterial = CreateCardMaterial("Sample4 Corner Peel Front", frontSpriteResourcePath, Color.white);
            backMaterial = CreateCardMaterial("Sample4 Corner Peel Back", backSpriteResourcePath, Color.white);
            edgeMaterial = CreateSolidMaterial("Sample4 Corner Peel Edge", edgeColor);
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        ApplyFixedFaceMaterials();
    }

    private void BuildCardMesh()
    {
        int segmentCount = Mathf.Max(4, segments);
        int gridStride = segmentCount + 1;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float halfThickness = thickness * 0.5f;

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
        for (int y = 0; y <= segmentCount; y++)
        {
            float v = y / (float)segmentCount;
            float yPosition = Mathf.Lerp(-halfHeight, halfHeight, v);
            for (int x = 0; x <= segmentCount; x++)
            {
                float u = x / (float)segmentCount;
                float xPosition = Mathf.Lerp(-halfWidth, halfWidth, u);
                AddVertex(new Vector3(xPosition, yPosition, halfThickness), new Vector2(1f - u, v));
            }
        }

        for (int y = 0; y < segmentCount; y++)
        {
            for (int x = 0; x < segmentCount; x++)
            {
                int bottomLeft = frontStart + y * gridStride + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + gridStride;
                int topRight = topLeft + 1;
                AddQuad(frontTriangles, bottomLeft, bottomRight, topRight, topLeft);
            }
        }

        int backStart = vertices.Count;
        for (int y = 0; y <= segmentCount; y++)
        {
            float v = y / (float)segmentCount;
            float yPosition = Mathf.Lerp(-halfHeight, halfHeight, v);
            for (int x = 0; x <= segmentCount; x++)
            {
                float u = x / (float)segmentCount;
                float xPosition = Mathf.Lerp(-halfWidth, halfWidth, u);
                AddVertex(new Vector3(xPosition, yPosition, -halfThickness), new Vector2(u, v));
            }
        }

        for (int y = 0; y < segmentCount; y++)
        {
            for (int x = 0; x < segmentCount; x++)
            {
                int bottomLeft = backStart + y * gridStride + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + gridStride;
                int topRight = topLeft + 1;
                AddQuad(backTriangles, bottomLeft, topLeft, topRight, bottomRight);
            }
        }

        for (int i = 0; i < segmentCount; i++)
        {
            float t0 = i / (float)segmentCount;
            float t1 = (i + 1) / (float)segmentCount;
            float x0 = Mathf.Lerp(-halfWidth, halfWidth, t0);
            float x1 = Mathf.Lerp(-halfWidth, halfWidth, t1);
            float y0 = Mathf.Lerp(-halfHeight, halfHeight, t0);
            float y1 = Mathf.Lerp(-halfHeight, halfHeight, t1);

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

        cardMesh = new Mesh
        {
            name = "Sample4 Corner Peel Card Mesh"
        };
        cardMesh.MarkDynamic();

        flatVertices = vertices.ToArray();
        deformedVertices = new Vector3[flatVertices.Length];
        flatVertices.CopyTo(deformedVertices, 0);

        cardMesh.vertices = deformedVertices;
        cardMesh.uv = uvs.ToArray();
        cardMesh.subMeshCount = 3;
        cardMesh.SetTriangles(frontTriangles.ToArray(), 0);
        cardMesh.SetTriangles(backTriangles.ToArray(), 1);
        cardMesh.SetTriangles(edgeTriangles.ToArray(), 2);
        cardMesh.RecalculateNormals();
        cardMesh.bounds = new Bounds(Vector3.zero, new Vector3(width * 3f, height * 3f, Mathf.Max(width, height) * 3f));
        meshFilter.sharedMesh = cardMesh;
    }

    private void ResetMeshDeformation()
    {
        if (cardMesh == null || flatVertices == null || deformedVertices == null)
        {
            return;
        }

        flatVertices.CopyTo(deformedVertices, 0);
        cardMesh.vertices = deformedVertices;
        cardMesh.RecalculateNormals();
        cardMesh.RecalculateBounds();
    }

    private void ApplyFixedFaceMaterials()
    {
        if (meshRenderer == null || frontMaterial == null || backMaterial == null || edgeMaterial == null)
        {
            return;
        }

        meshRenderer.sharedMaterials = new[] { frontMaterial, backMaterial, edgeMaterial };
    }

    private void ApplySpriteToMaterial(Material material, string spritePath, Color fallbackColor)
    {
        Sprite sprite = Resources.Load<Sprite>(spritePath);
        SetTexture(material, sprite != null ? sprite.texture : Texture2D.whiteTexture);
        SetColor(material, fallbackColor);

        if (sprite == null)
        {
            Debug.LogWarning($"[{nameof(CornerPeelCardFlipTest)}] Missing sprite at Resources/{spritePath}");
        }
    }

    private Material CreateCardMaterial(string materialName, string spritePath, Color fallbackColor)
    {
        Material material = new Material(FindCardShader())
        {
            name = materialName,
            color = fallbackColor
        };

        ApplySpriteToMaterial(material, spritePath, fallbackColor);
        return material;
    }

    private Material CreateSolidMaterial(string materialName, Color color)
    {
        Material material = new Material(FindCardShader())
        {
            name = materialName,
            color = color
        };
        SetTexture(material, Texture2D.whiteTexture);
        SetColor(material, color);
        return material;
    }

    private static Shader FindCardShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null)
        {
            return shader;
        }

        shader = Shader.Find("Unlit/Texture");
        return shader != null ? shader : Shader.Find("Standard");
    }

    private static void SetTexture(Material material, Texture texture)
    {
        if (material.HasProperty(BaseMapProperty))
        {
            material.SetTexture(BaseMapProperty, texture);
        }

        if (material.HasProperty(MainTexProperty))
        {
            material.SetTexture(MainTexProperty, texture);
        }
    }

    private static void SetColor(Material material, Color color)
    {
        if (material.HasProperty(BaseColorProperty))
        {
            material.SetColor(BaseColorProperty, color);
        }

        if (material.HasProperty(ColorProperty))
        {
            material.SetColor(ColorProperty, color);
        }
    }

    private void CacheStartPose()
    {
        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;
        hasStartPose = true;
    }

    private void CacheStartPoseIfNeeded()
    {
        if (!hasStartPose)
        {
            CacheStartPose();
        }
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static void DestroyRuntimeObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
