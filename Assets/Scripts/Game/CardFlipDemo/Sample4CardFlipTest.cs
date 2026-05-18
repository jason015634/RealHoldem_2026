using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class Sample4CardFlipTest : MonoBehaviour
{
    private const string BaseMapProperty = "_BaseMap";
    private const string MainTexProperty = "_MainTex";
    private const string BaseColorProperty = "_BaseColor";
    private const string ColorProperty = "_Color";
    private const string CullProperty = "_Cull";

    [Header("Timing")]
    [SerializeField] private float flipDuration = 0.58f;
    [SerializeField] private float settleDuration = 0.16f;

    [Header("Motion")]
    [SerializeField] private float liftHeight = 0.18f;
    [SerializeField] private float tiltAngle = 8f;
    [SerializeField] private float overshootAngle = 7f;

    [Header("Test")]
    [SerializeField] private bool autoPlay;
    [SerializeField] private bool startFaceUp;

    [Header("Renderers")]
    [SerializeField] private MeshRenderer targetRenderer;
    [SerializeField] private Renderer frontRenderer;
    [SerializeField] private Renderer backRenderer;

    [Header("Materials")]
    [SerializeField] private Material frontMaterial;
    [SerializeField] private Material backMaterial;
    [SerializeField] private Material edgeMaterial;

    [Header("Fallback Sprites")]
    [SerializeField] private string frontSpriteResourcePath = "Sprites/Cards/Heart_Ace";
    [SerializeField] private string backSpriteResourcePath = "Sprites/Cards/back";
    [SerializeField] private Color edgeColor = new Color(0.82f, 0.82f, 0.76f, 1f);

    private MeshFilter meshFilter;
    private Sequence flipSequence;
    private Material runtimeFrontMaterial;
    private Material runtimeBackMaterial;
    private Material runtimeEdgeMaterial;
    private Mesh runtimeMesh;
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalScale;
    private bool hasInitialPose;
    private bool isFaceUp;
    private bool isAnimating;

    private void Awake()
    {
        CacheInitialPose();
        EnsureReady();
        isFaceUp = startFaceUp;
        SnapToFaceState(isFaceUp);
    }

    private void Start()
    {
        if (autoPlay)
        {
            PlayFlip();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            PlayFlip();
        }
    }

    private void OnValidate()
    {
        flipDuration = Mathf.Max(0.05f, flipDuration);
        settleDuration = Mathf.Max(0.01f, settleDuration);
        liftHeight = Mathf.Max(0f, liftHeight);
        tiltAngle = Mathf.Max(0f, tiltAngle);
        overshootAngle = Mathf.Max(0f, overshootAngle);
    }

    private void OnDisable()
    {
        flipSequence?.Kill(false);
        isAnimating = false;
    }

    private void OnDestroy()
    {
        flipSequence?.Kill(false);
        DestroyRuntimeObject(runtimeFrontMaterial);
        DestroyRuntimeObject(runtimeBackMaterial);
        DestroyRuntimeObject(runtimeEdgeMaterial);
        DestroyRuntimeObject(runtimeMesh);
    }

    [ContextMenu("Play Flip")]
    public void PlayFlip()
    {
        if (isAnimating)
        {
            return;
        }

        EnsureReady();
        CacheInitialPose();

        bool fromFaceUp = isFaceUp;
        bool toFaceUp = !fromFaceUp;
        float startAngle = fromFaceUp ? 180f : 0f;
        float endAngle = toFaceUp ? 180f : 0f;
        float progress = 0f;
        bool visualSwapped = false;

        isAnimating = true;
        ApplyFaceVisual(fromFaceUp);

        flipSequence?.Kill(false);
        flipSequence = DOTween.Sequence()
            .SetTarget(this)
            .Append(DOTween.To(() => progress, value =>
            {
                progress = value;
                ApplyFlipPose(progress, startAngle, endAngle);

                if (!visualSwapped && progress >= 0.5f)
                {
                    ApplyFaceVisual(toFaceUp);
                    visualSwapped = true;
                }
            }, 0.82f, flipDuration).SetEase(Ease.InOutCubic))
            .Append(DOTween.To(() => progress, value =>
            {
                progress = value;
                ApplyFlipPose(progress, startAngle, endAngle);

                if (!visualSwapped && progress >= 0.5f)
                {
                    ApplyFaceVisual(toFaceUp);
                    visualSwapped = true;
                }
            }, 1f, settleDuration).SetEase(Ease.OutSine))
            .OnComplete(() =>
            {
                isFaceUp = toFaceUp;
                SnapToFaceState(isFaceUp);
                isAnimating = false;
            })
            .OnKill(() =>
            {
                if (isAnimating)
                {
                    SnapToFaceState(isFaceUp);
                    isAnimating = false;
                }
            });
    }

    [ContextMenu("Reset To Start Face")]
    public void ResetToStartFace()
    {
        EnsureReady();
        isFaceUp = startFaceUp;
        SnapToFaceState(isFaceUp);
    }

    private void CacheInitialPose()
    {
        if (hasInitialPose)
        {
            return;
        }

        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        initialLocalScale = transform.localScale;
        hasInitialPose = true;
    }

    private void EnsureReady()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<MeshRenderer>();
        }

        AutoFindFaceRenderers();
        EnsureMesh();
        EnsureMaterials();
        ApplyRendererMaterials();
    }

    private void AutoFindFaceRenderers()
    {
        if (frontRenderer != null && backRenderer != null)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            string rendererName = renderer.name.ToLowerInvariant();
            if (frontRenderer == null && rendererName.Contains("front"))
            {
                frontRenderer = renderer;
            }
            else if (backRenderer == null && rendererName.Contains("back"))
            {
                backRenderer = renderer;
            }
        }
    }

    private void EnsureMesh()
    {
        if (meshFilter == null || meshFilter.sharedMesh != null)
        {
            return;
        }

        runtimeMesh = BuildSimpleCardMesh(0.5f, 0.72f, 0.018f);
        meshFilter.sharedMesh = runtimeMesh;
        Debug.LogWarning($"[{nameof(Sample4CardFlipTest)}] Sample4 had no mesh, so a runtime test mesh was created.");
    }

    private void EnsureMaterials()
    {
        if (frontMaterial == null)
        {
            runtimeFrontMaterial = CreateSpriteMaterial("Sample4 Flip Front", frontSpriteResourcePath, Color.white);
            frontMaterial = runtimeFrontMaterial;
        }

        if (backMaterial == null)
        {
            runtimeBackMaterial = CreateSpriteMaterial("Sample4 Flip Back", backSpriteResourcePath, Color.white);
            backMaterial = runtimeBackMaterial;
        }

        if (edgeMaterial == null)
        {
            runtimeEdgeMaterial = CreateSolidMaterial("Sample4 Flip Edge", edgeColor);
            edgeMaterial = runtimeEdgeMaterial;
        }

        if (targetRenderer == null && frontRenderer == null && backRenderer == null)
        {
            Debug.LogWarning($"[{nameof(Sample4CardFlipTest)}] Assign targetRenderer, or connect frontRenderer/backRenderer in the Inspector.");
        }
    }

    private void ApplyRendererMaterials()
    {
        if (targetRenderer != null)
        {
            Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (mesh != null && mesh.subMeshCount >= 3)
            {
                targetRenderer.sharedMaterials = new[] { frontMaterial, backMaterial, edgeMaterial };
            }
            else if (mesh != null && mesh.subMeshCount == 2)
            {
                targetRenderer.sharedMaterials = new[] { frontMaterial, backMaterial };
            }
            else if (targetRenderer.sharedMaterial == null)
            {
                targetRenderer.sharedMaterial = startFaceUp ? frontMaterial : backMaterial;
            }

            targetRenderer.shadowCastingMode = ShadowCastingMode.Off;
            targetRenderer.receiveShadows = false;
        }

        if (frontRenderer != null && frontRenderer.sharedMaterial == null)
        {
            frontRenderer.sharedMaterial = frontMaterial;
        }

        if (backRenderer != null && backRenderer.sharedMaterial == null)
        {
            backRenderer.sharedMaterial = backMaterial;
        }
    }

    private void ApplyFlipPose(float progress, float startAngle, float endAngle)
    {
        progress = Mathf.Clamp01(progress);

        float rotationProgress = GetRotationProgress(progress);
        float direction = Mathf.Sign(endAngle - startAngle);
        if (Mathf.Approximately(direction, 0f))
        {
            direction = 1f;
        }

        float settleProgress = Mathf.Clamp01(Mathf.InverseLerp(0.68f, 1f, progress));
        float overshoot = Mathf.Sin(settleProgress * Mathf.PI) * overshootAngle * direction;
        float yAngle = Mathf.Lerp(startAngle, endAngle, rotationProgress) + overshoot;
        float lift = Mathf.Sin(progress * Mathf.PI) * liftHeight;
        float pitch = Mathf.Sin(Mathf.Clamp01(progress / 0.32f) * Mathf.PI) * tiltAngle;
        pitch -= Mathf.Sin(settleProgress * Mathf.PI) * tiltAngle * 0.28f;
        float roll = Mathf.Sin(progress * Mathf.PI) * tiltAngle * 0.22f * direction;
        float scalePulse = Mathf.Sin(progress * Mathf.PI) * 0.015f;

        transform.localPosition = initialLocalPosition + Vector3.up * lift;
        transform.localRotation = initialLocalRotation
            * Quaternion.AngleAxis(yAngle, Vector3.up)
            * Quaternion.AngleAxis(pitch, Vector3.right)
            * Quaternion.AngleAxis(roll, Vector3.forward);
        transform.localScale = new Vector3(
            initialLocalScale.x * (1f + scalePulse),
            initialLocalScale.y * (1f - scalePulse * 0.35f),
            initialLocalScale.z);
    }

    private static float GetRotationProgress(float progress)
    {
        if (progress < 0.18f)
        {
            return Mathf.Lerp(0f, 0.08f, Smooth01(progress / 0.18f));
        }

        if (progress < 0.72f)
        {
            return Mathf.Lerp(0.08f, 0.92f, Smooth01((progress - 0.18f) / 0.54f));
        }

        return Mathf.Lerp(0.92f, 1f, Smooth01((progress - 0.72f) / 0.28f));
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void SnapToFaceState(bool faceUp)
    {
        CacheInitialPose();
        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation * Quaternion.AngleAxis(faceUp ? 180f : 0f, Vector3.up);
        transform.localScale = initialLocalScale;
        ApplyFaceVisual(faceUp);
    }

    private void ApplyFaceVisual(bool faceUp)
    {
        bool hasSeparateRenderers = frontRenderer != null
            && backRenderer != null
            && frontRenderer != backRenderer;

        if (hasSeparateRenderers)
        {
            frontRenderer.enabled = faceUp;
            backRenderer.enabled = !faceUp;
            return;
        }

        if (targetRenderer == null)
        {
            return;
        }

        Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
        if (mesh != null && mesh.subMeshCount >= 2)
        {
            ApplyRendererMaterials();
            return;
        }

        targetRenderer.sharedMaterial = faceUp ? frontMaterial : backMaterial;
    }

    private Material CreateSpriteMaterial(string materialName, string spritePath, Color fallbackColor)
    {
        Material material = CreateSolidMaterial(materialName, fallbackColor);
        Sprite sprite = Resources.Load<Sprite>(spritePath);
        SetTexture(material, sprite != null ? sprite.texture : Texture2D.whiteTexture);

        if (sprite == null)
        {
            Debug.LogWarning($"[{nameof(Sample4CardFlipTest)}] Missing sprite at Resources/{spritePath}. Connect a material in the Inspector if this is not expected.");
        }

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
        DisableBackfaceCulling(material);
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

    private static void DisableBackfaceCulling(Material material)
    {
        if (material.HasProperty(CullProperty))
        {
            material.SetFloat(CullProperty, (float)CullMode.Off);
        }
    }

    private static Mesh BuildSimpleCardMesh(float width, float height, float thickness)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float halfThickness = thickness * 0.5f;

        Vector3[] vertices =
        {
            new Vector3(-halfWidth, -halfHeight, halfThickness),
            new Vector3(halfWidth, -halfHeight, halfThickness),
            new Vector3(halfWidth, halfHeight, halfThickness),
            new Vector3(-halfWidth, halfHeight, halfThickness),
            new Vector3(-halfWidth, -halfHeight, -halfThickness),
            new Vector3(halfWidth, -halfHeight, -halfThickness),
            new Vector3(halfWidth, halfHeight, -halfThickness),
            new Vector3(-halfWidth, halfHeight, -halfThickness)
        };

        Vector2[] uvs =
        {
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };

        int[] frontTriangles = { 0, 1, 2, 0, 2, 3 };
        int[] backTriangles = { 4, 7, 6, 4, 6, 5 };
        int[] edgeTriangles =
        {
            0, 4, 5, 0, 5, 1,
            1, 5, 6, 1, 6, 2,
            2, 6, 7, 2, 7, 3,
            3, 7, 4, 3, 4, 0
        };

        Mesh mesh = new Mesh
        {
            name = "Sample4 Runtime Test Card Mesh",
            vertices = vertices,
            uv = uvs,
            subMeshCount = 3
        };
        mesh.SetTriangles(frontTriangles, 0);
        mesh.SetTriangles(backTriangles, 1);
        mesh.SetTriangles(edgeTriangles, 2);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
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
