using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class PokerCommunityCardFlip : MonoBehaviour
{
    private const string EdgePivotName = "EdgePivot";
    private const string CardVisualName = "CardVisual";
    private const string SliceNamePrefix = "PaperSlice_";
    private const string MainTexProperty = "_MainTex";
    private const string BaseMapProperty = "_BaseMap";
    private const string ColorProperty = "_Color";
    private const string BaseColorProperty = "_BaseColor";

    [Header("Sprites")]
    [SerializeField] private Sprite frontSprite;
    [SerializeField] private Sprite backSprite;

    [Header("Timing")]
    [SerializeField] private float revealDuration = 0.27f;
    [SerializeField] private float settleDuration = 0.06f;

    [Header("Start Pose")]
    [SerializeField] private float startRotationY = 82f;
    [SerializeField] private float startRotationZ = -7f;
    [SerializeField] private float startScaleX = 0.96f;

    [Header("Open Motion")]
    [SerializeField] private float overshootScale = 1.05f;
    [SerializeField] private float overshootRotationZ = 2f;

    [Header("Paper Bend")]
    [SerializeField] private float bendStrength = 18f;
    [SerializeField] private AnimationCurve bendCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2f),
        new Keyframe(0.22f, 1f, 0f, 0f),
        new Keyframe(0.65f, 0.45f, -1.6f, -1.6f),
        new Keyframe(1f, 0f, -0.2f, 0f));

    [Header("Card Shape")]
    [SerializeField] private Vector2 cardSize = new Vector2(0.62f, 0.88f);
    [SerializeField] private float cardThickness = 0.018f;
    [SerializeField] private Color edgeColor = new Color(0.84f, 0.82f, 0.74f, 1f);
    [SerializeField] private int sliceCount = 5;
    [SerializeField] private float sliceDepthStep;

    [Header("Test")]
    [SerializeField] private bool playOnStart;
    [SerializeField] private bool allowClickTest = true;

    private readonly List<Slice> slices = new List<Slice>(5);

    private Transform edgePivot;
    private Transform cardVisual;
    private Material faceMaterial;
    private Material edgeMaterial;
    private Sequence revealSequence;
    private Sprite displayedSprite;
    private bool faceSpriteApplied;
    private bool isFlipping;

    public bool IsFlipping => isFlipping;

    private sealed class Slice
    {
        public Transform Transform;
        public Mesh Mesh;
        public MeshRenderer Renderer;
        public MeshFilter Filter;
        public float FromHinge;
    }

    private void Awake()
    {
        EnsureStructure();
        ResetToBackPose();
    }

    private void Start()
    {
        if (playOnStart)
        {
            PlayReveal(frontSprite);
        }
    }

    private void Update()
    {
        if (!allowClickTest || isFlipping || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (IsPointerOverCard())
        {
            PlayReveal(frontSprite);
        }
    }

    private void OnDisable()
    {
        revealSequence?.Kill(false);
        revealSequence = null;
        isFlipping = false;
    }

    private void OnDestroy()
    {
        revealSequence?.Kill(false);
        DestroyRuntimeObject(faceMaterial);
        DestroyRuntimeObject(edgeMaterial);

        for (int i = 0; i < slices.Count; i++)
        {
            DestroyRuntimeObject(slices[i].Mesh);
        }
    }

    private void OnValidate()
    {
        revealDuration = Mathf.Max(0.05f, revealDuration);
        settleDuration = Mathf.Max(0.01f, settleDuration);
        startRotationY = Mathf.Clamp(startRotationY, 0f, 89f);
        startScaleX = Mathf.Clamp(startScaleX, 0.5f, 1f);
        overshootScale = Mathf.Max(1f, overshootScale);
        bendStrength = Mathf.Max(0f, bendStrength);
        cardSize.x = Mathf.Max(0.05f, cardSize.x);
        cardSize.y = Mathf.Max(0.05f, cardSize.y);
        cardThickness = Mathf.Clamp(cardThickness, 0.001f, 0.08f);
        sliceCount = Mathf.Clamp(sliceCount, 3, 9);
        sliceDepthStep = Mathf.Max(0f, sliceDepthStep);

        if (Application.isPlaying && edgePivot != null)
        {
            EnsureStructure();
            ApplyProgress(0f);
        }
    }

    [ContextMenu("Test Flip")]
    public void TestFlip()
    {
        PlayReveal(frontSprite);
    }

    public void PlayReveal(Sprite frontSprite)
    {
        if (isFlipping)
        {
            return;
        }

        this.frontSprite = frontSprite != null ? frontSprite : this.frontSprite;
        EnsureStructure();

        isFlipping = true;
        faceSpriteApplied = false;
        ApplySprite(backSprite);
        ApplyProgress(0f);

        float progress = 0f;
        float squeezeDuration = revealDuration * 0.24f;
        float openDuration = revealDuration * 0.76f;

        revealSequence?.Kill(false);
        revealSequence = DOTween.Sequence();
        revealSequence
            .Append(DOTween.To(() => progress, value =>
            {
                progress = value;
                ApplyProgress(progress);
            }, 0.22f, squeezeDuration).SetEase(Ease.OutCubic))
            .AppendCallback(() =>
            {
                faceSpriteApplied = true;
                ApplySprite(this.frontSprite);
            })
            .Append(DOTween.To(() => progress, value =>
            {
                progress = value;
                ApplyProgress(progress);
            }, 0.82f, openDuration).SetEase(Ease.OutQuart))
            .Append(DOTween.To(() => progress, value =>
            {
                progress = value;
                ApplyProgress(progress);
            }, 1f, settleDuration).SetEase(Ease.OutBack))
            .OnComplete(() =>
            {
                ApplySprite(this.frontSprite);
                ApplyProgress(1f);
                isFlipping = false;
                revealSequence = null;
            })
            .OnKill(() =>
            {
                if (isFlipping)
                {
                    isFlipping = false;
                }
            });
    }

    [ContextMenu("Reset To Back Pose")]
    public void ResetToBackPose()
    {
        EnsureStructure();
        isFlipping = false;
        faceSpriteApplied = false;
        ApplySprite(backSprite);
        ApplyProgress(0f);
    }

    private void EnsureStructure()
    {
        edgePivot = FindOrCreateChild(transform, EdgePivotName);
        cardVisual = FindOrCreateChild(edgePivot, CardVisualName);

        edgePivot.localPosition = new Vector3(-cardSize.x * 0.5f, 0f, 0f);
        cardVisual.localPosition = new Vector3(cardSize.x * 0.5f, 0f, 0f);
        cardVisual.localRotation = Quaternion.identity;

        EnsureMaterial();
        EnsureSlices();
        ApplySprite(displayedSprite != null ? displayedSprite : backSprite);
    }

    private void EnsureSlices()
    {
        while (slices.Count < sliceCount)
        {
            int index = slices.Count;
            GameObject sliceObject = new GameObject($"{SliceNamePrefix}{index + 1:00}");
            sliceObject.transform.SetParent(cardVisual, false);

            Slice slice = new Slice
            {
                Transform = sliceObject.transform,
                Filter = sliceObject.AddComponent<MeshFilter>(),
                Renderer = sliceObject.AddComponent<MeshRenderer>(),
                Mesh = new Mesh { name = $"{name}_PaperSlice_{index + 1:00}" }
            };

            slice.Filter.sharedMesh = slice.Mesh;
            slice.Renderer.sharedMaterials = new[] { faceMaterial, edgeMaterial };
            slice.Renderer.shadowCastingMode = ShadowCastingMode.Off;
            slice.Renderer.receiveShadows = false;
            slices.Add(slice);
        }

        for (int i = slices.Count - 1; i >= sliceCount; i--)
        {
            Slice slice = slices[i];
            DestroyRuntimeObject(slice.Mesh);
            DestroyRuntimeObject(slice.Transform.gameObject);
            slices.RemoveAt(i);
        }

        for (int i = 0; i < slices.Count; i++)
        {
            ConfigureSlice(i, displayedSprite != null ? displayedSprite : backSprite);
        }
    }

    private void ConfigureSlice(int index, Sprite sprite)
    {
        Slice slice = slices[index];
        float width = cardSize.x / sliceCount;
        float left = -cardSize.x * 0.5f + width * index;
        float center = left + width * 0.5f;
        float fromHinge = sliceCount <= 1 ? 1f : index / (float)(sliceCount - 1);

        slice.FromHinge = fromHinge;
        slice.Transform.localPosition = new Vector3(center, 0f, -index * sliceDepthStep);
        slice.Transform.localScale = Vector3.one;

        UpdateSliceMesh(slice.Mesh, sprite, index, width);
        slice.Filter.sharedMesh = slice.Mesh;
        slice.Renderer.sharedMaterials = new[] { faceMaterial, edgeMaterial };
    }

    private void UpdateSliceMesh(Mesh mesh, Sprite sprite, int index, float width)
    {
        Rect textureRect = GetTextureRect(sprite);
        Texture texture = sprite != null ? sprite.texture : Texture2D.whiteTexture;

        float u0 = (textureRect.xMin + textureRect.width * index / sliceCount) / texture.width;
        float u1 = (textureRect.xMin + textureRect.width * (index + 1) / sliceCount) / texture.width;
        float v0 = textureRect.yMin / texture.height;
        float v1 = textureRect.yMax / texture.height;

        float halfWidth = width * 0.5f;
        float halfHeight = cardSize.y * 0.5f;
        float halfThickness = cardThickness * 0.5f;

        Vector3[] vertices =
        {
            new Vector3(-halfWidth, -halfHeight, -halfThickness),
            new Vector3(halfWidth, -halfHeight, -halfThickness),
            new Vector3(-halfWidth, halfHeight, -halfThickness),
            new Vector3(halfWidth, halfHeight, -halfThickness),
            new Vector3(-halfWidth, -halfHeight, halfThickness),
            new Vector3(halfWidth, -halfHeight, halfThickness),
            new Vector3(-halfWidth, halfHeight, halfThickness),
            new Vector3(halfWidth, halfHeight, halfThickness),
            new Vector3(-halfWidth, -halfHeight, halfThickness),
            new Vector3(-halfWidth, -halfHeight, -halfThickness),
            new Vector3(-halfWidth, halfHeight, halfThickness),
            new Vector3(-halfWidth, halfHeight, -halfThickness),
            new Vector3(halfWidth, -halfHeight, -halfThickness),
            new Vector3(halfWidth, -halfHeight, halfThickness),
            new Vector3(halfWidth, halfHeight, -halfThickness),
            new Vector3(halfWidth, halfHeight, halfThickness),
            new Vector3(-halfWidth, -halfHeight, halfThickness),
            new Vector3(halfWidth, -halfHeight, halfThickness),
            new Vector3(-halfWidth, -halfHeight, -halfThickness),
            new Vector3(halfWidth, -halfHeight, -halfThickness),
            new Vector3(-halfWidth, halfHeight, -halfThickness),
            new Vector3(halfWidth, halfHeight, -halfThickness),
            new Vector3(-halfWidth, halfHeight, halfThickness),
            new Vector3(halfWidth, halfHeight, halfThickness)
        };
        Vector2[] uvs =
        {
            new Vector2(u0, v0),
            new Vector2(u1, v0),
            new Vector2(u0, v1),
            new Vector2(u1, v1),
            new Vector2(u1, v0),
            new Vector2(u0, v0),
            new Vector2(u1, v1),
            new Vector2(u0, v1),
            Vector2.zero,
            Vector2.right,
            Vector2.up,
            Vector2.one,
            Vector2.zero,
            Vector2.right,
            Vector2.up,
            Vector2.one,
            Vector2.zero,
            Vector2.right,
            Vector2.up,
            Vector2.one,
            Vector2.zero,
            Vector2.right,
            Vector2.up,
            Vector2.one
        };
        int[] faceTriangles =
        {
            0, 2, 1,
            2, 3, 1,
            4, 5, 6,
            6, 5, 7
        };
        List<int> edgeTriangles = new List<int>(24);
        if (index == 0)
        {
            AddQuad(edgeTriangles, 8, 10, 9, 11);
        }

        if (index == sliceCount - 1)
        {
            AddQuad(edgeTriangles, 12, 14, 13, 15);
        }

        AddQuad(edgeTriangles, 16, 18, 17, 19);
        AddQuad(edgeTriangles, 20, 22, 21, 23);

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(faceTriangles, 0);
        mesh.SetTriangles(edgeTriangles, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void ApplyProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);

        float thinPoint = 0.22f;
        float openPoint = 0.82f;
        float thinScaleX = Mathf.Min(startScaleX, 0.92f);
        float rotationY;
        float rotationZ;
        float scaleX;
        float uniformScale;

        if (progress <= thinPoint)
        {
            float t = EaseOutCubic(progress / thinPoint);
            rotationY = Mathf.Lerp(startRotationY, 88f, t);
            rotationZ = Mathf.Lerp(startRotationZ, startRotationZ * 0.65f, t);
            scaleX = Mathf.Lerp(startScaleX, thinScaleX, t);
            uniformScale = 1f;
        }
        else if (progress <= openPoint)
        {
            float t = EaseOutQuart((progress - thinPoint) / (openPoint - thinPoint));
            rotationY = Mathf.Lerp(88f, -3f, t);
            rotationZ = Mathf.Lerp(startRotationZ * 0.65f, overshootRotationZ, t);
            scaleX = Mathf.Lerp(thinScaleX, overshootScale, t);
            uniformScale = Mathf.Lerp(1f, 1.025f, t);
        }
        else
        {
            float t = EaseOutBack((progress - openPoint) / (1f - openPoint));
            rotationY = Mathf.Lerp(-3f, 0f, t);
            rotationZ = Mathf.Lerp(overshootRotationZ, 0f, t);
            scaleX = Mathf.Lerp(overshootScale, 1f, t);
            uniformScale = Mathf.Lerp(1.025f, 1f, t);
        }

        edgePivot.localRotation = Quaternion.Euler(0f, rotationY, rotationZ);
        cardVisual.localScale = new Vector3(scaleX * uniformScale, uniformScale, uniformScale);

        float bendAmount = bendStrength * Mathf.Max(0f, bendCurve != null ? bendCurve.Evaluate(progress) : 0f);
        for (int i = 0; i < slices.Count; i++)
        {
            ApplySliceBend(slices[i], i, bendAmount, progress);
        }

        if (!faceSpriteApplied && progress >= thinPoint)
        {
            faceSpriteApplied = true;
            ApplySprite(frontSprite);
        }
    }

    private void ApplySliceBend(Slice slice, int index, float bendAmount, float progress)
    {
        float fromHinge = Mathf.Clamp01(slice.FromHinge);
        float bendWeight = Mathf.Pow(fromHinge, 1.35f);
        float curlSign = 1f;
        float yRotation = bendAmount * bendWeight * curlSign;
        float zLift = Mathf.Sin(fromHinge * Mathf.PI) * bendAmount * 0.0009f;
        float verticalTuck = Mathf.Sin(progress * Mathf.PI) * Mathf.Lerp(0f, 0.006f, fromHinge);

        slice.Transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
        Vector3 localPosition = slice.Transform.localPosition;
        localPosition.z = -index * sliceDepthStep - zLift;
        localPosition.y = -verticalTuck;
        slice.Transform.localPosition = localPosition;
    }

    private void ApplySprite(Sprite sprite)
    {
        displayedSprite = sprite;
        Texture texture = sprite != null ? sprite.texture : Texture2D.whiteTexture;

        EnsureMaterial();
        SetTexture(faceMaterial, texture);

        for (int i = 0; i < slices.Count; i++)
        {
            ConfigureSlice(i, sprite);
        }
    }

    private void EnsureMaterial()
    {
        if (faceMaterial != null && edgeMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Texture");
        }

        if (faceMaterial == null)
        {
            faceMaterial = new Material(shader)
            {
                name = $"{nameof(PokerCommunityCardFlip)}_FaceRuntimeMaterial"
            };

            SetColor(faceMaterial, Color.white);
            if (faceMaterial.HasProperty("_Cull"))
            {
                faceMaterial.SetFloat("_Cull", (float)CullMode.Off);
            }
        }

        if (edgeMaterial == null)
        {
            edgeMaterial = new Material(shader)
            {
                name = $"{nameof(PokerCommunityCardFlip)}_EdgeRuntimeMaterial"
            };

            SetTexture(edgeMaterial, Texture2D.whiteTexture);
            SetColor(edgeMaterial, edgeColor);
            if (edgeMaterial.HasProperty("_Cull"))
            {
                edgeMaterial.SetFloat("_Cull", (float)CullMode.Off);
            }
        }
    }

    private static Transform FindOrCreateChild(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(parent, false);
        return childObject.transform;
    }

    private bool IsPointerOverCard()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return true;
        }

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            return hit.transform == transform || hit.transform.IsChildOf(transform);
        }

        Bounds bounds;
        if (!TryGetRendererBounds(out bounds))
        {
            return true;
        }

        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0f);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0f);
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 world = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 screen = camera.WorldToScreenPoint(world);
                    min = Vector3.Min(min, screen);
                    max = Vector3.Max(max, screen);
                }
            }
        }

        Rect rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return rect.Contains(Input.mousePosition);
    }

    private bool TryGetRendererBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < slices.Count; i++)
        {
            MeshRenderer renderer = slices[i].Renderer;
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 3f);
    }

    private static float EaseOutQuart(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - Mathf.Pow(1f - t, 4f);
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private static void SetTexture(Material material, Texture texture)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(MainTexProperty))
        {
            material.SetTexture(MainTexProperty, texture);
        }

        if (material.HasProperty(BaseMapProperty))
        {
            material.SetTexture(BaseMapProperty, texture);
        }
    }

    private static void SetColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(ColorProperty))
        {
            material.SetColor(ColorProperty, color);
        }

        if (material.HasProperty(BaseColorProperty))
        {
            material.SetColor(BaseColorProperty, color);
        }
    }

    private static void AddQuad(List<int> triangles, int bottomLeft, int topLeft, int bottomRight, int topRight)
    {
        triangles.Add(bottomLeft);
        triangles.Add(topLeft);
        triangles.Add(bottomRight);
        triangles.Add(topLeft);
        triangles.Add(topRight);
        triangles.Add(bottomRight);
    }

    private static Rect GetTextureRect(Sprite sprite)
    {
        if (sprite == null)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        try
        {
            return sprite.textureRect;
        }
        catch (UnityException)
        {
            return sprite.rect;
        }
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
