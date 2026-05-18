using System.Collections.Generic;
using UnityEngine;

// Scene setup guide:
// 1. Open CardOpenTestScene or create an Empty GameObject for the hold-card root.
// 2. Create child objects named LeftCard and RightCard.
// 3. Add CardOpenPeekingDemo to each child. MeshFilter, MeshRenderer, and BoxCollider are required automatically.
// 4. Add CardOpenHoldCardPairDemo to the parent and assign the two child card references.
// 5. In CardOpenTestScene the parent root uses X=90 degrees, so local -Z lifts toward the camera/up.
// 6. Place the cards so the player-near edge is local -Y and the far/readable edge is local +Y.
// 7. Set the input camera and collider layer mask if the default MainCamera/~0 setup is too broad.

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(BoxCollider))]
public sealed class CardOpenPeekingDemo : MonoBehaviour
{
    private const string BaseMapProperty = "_BaseMap";
    private const string MainTexProperty = "_MainTex";
    private const string BaseColorProperty = "_BaseColor";
    private const string ColorProperty = "_Color";

    [Header("Card Mesh")]
    [SerializeField] private float width = 0.5f;
    [SerializeField] private float height = 0.72f;
    [SerializeField] private float thickness = 0.018f;
    [SerializeField] private int segments = 32;

    [Header("Peeking Input")]
    [SerializeField] private bool handleInput = true;
    [SerializeField] private Camera inputCamera;
    [SerializeField] private LayerMask inputLayers = ~0;
    [SerializeField] private float dragPixelsForFullLift = 240f;
    [Range(0f, 1f)]
    [Tooltip("0 = local -Y/player-near edge, 1 = local +Y/far edge.")]
    [SerializeField] private float hingePosition = 0.08f;

    [Header("Curl Limit")]
    [SerializeField] private float maxLiftHeight = 0.08f;
    [Range(10f, 180f)]
    [SerializeField] private float maxCurlAngle = 44f;
    [SerializeField] private float curlDepth = 0.24f;
    [SerializeField] private float widthRoundness = 0.018f;
    [SerializeField] private float sideEdgeLag = 0.08f;

    [Header("Curl Shape")]
    [SerializeField] private AnimationCurve bendCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.2f),
        new Keyframe(0.38f, 0.08f, 0.35f, 0.85f),
        new Keyframe(0.78f, 0.56f, 1.35f, 1.25f),
        new Keyframe(1f, 1f, 0.3f, 0f));
    [SerializeField] private AnimationCurve liftCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0.1f),
        new Keyframe(0.55f, 0.36f, 1.1f, 1.6f),
        new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private float dragFollowSpeed = 18f;
    [SerializeField] private float returnSpeed = 10f;

    [Header("Card Materials")]
    [SerializeField] private Color frontColor = Color.white;
    [SerializeField] private Color backColor = new Color(0.16f, 0.27f, 0.52f, 1f);
    [SerializeField] private Color edgeColor = new Color(0.82f, 0.82f, 0.76f, 1f);
    [SerializeField] private bool useCardSprites = true;
    [SerializeField] private string backSpriteResourcePath = "Sprites/Cards/back";
    [SerializeField] private string frontSpriteResourcePath = "Sprites/Cards/Diamond_King";

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private BoxCollider boxCollider;
    private Mesh cardMesh;
    private Vector3[] flatVertices;
    private Vector3[] deformedVertices;
    private Material frontMaterial;
    private Material backMaterial;
    private Material edgeMaterial;
    private float dragStartScreenY;
    private int activeTouchId = -1;
    private float targetPeekProgress;
    private float currentPeekProgress;
    private bool isDragging;
    private bool pendingRuntimeRefresh;

    public float Width => width;
    public float Height => height;

    private void Awake()
    {
        EnsureReady();
        ApplyPeek(0f);
    }

    private void OnValidate()
    {
        width = Mathf.Max(0.05f, width);
        height = Mathf.Max(0.05f, height);
        thickness = Mathf.Max(0.001f, thickness);
        segments = Mathf.Clamp(segments, 4, 96);
        dragPixelsForFullLift = Mathf.Max(1f, dragPixelsForFullLift);
        hingePosition = Mathf.Clamp01(hingePosition);
        maxLiftHeight = Mathf.Max(0f, maxLiftHeight);
        curlDepth = Mathf.Max(0f, curlDepth);
        widthRoundness = Mathf.Max(0f, widthRoundness);
        sideEdgeLag = Mathf.Clamp01(sideEdgeLag);
        dragFollowSpeed = Mathf.Max(0.01f, dragFollowSpeed);
        returnSpeed = Mathf.Max(0.01f, returnSpeed);

        if (Application.isPlaying)
        {
            pendingRuntimeRefresh = true;
        }
    }

    private void Update()
    {
        if (pendingRuntimeRefresh)
        {
            pendingRuntimeRefresh = false;
            EnsureReady();
            ApplyPeek(currentPeekProgress);
        }

        if (handleInput)
        {
            HandlePointerInput();
        }

        UpdatePeekProgress(Time.deltaTime);
    }

    public void SetInputEnabled(bool enabled)
    {
        handleInput = enabled;
        if (!enabled)
        {
            isDragging = false;
            activeTouchId = -1;
            targetPeekProgress = 0f;
        }
    }

    public void SetPeekProgressTarget(float progress)
    {
        targetPeekProgress = Mathf.Clamp01(progress);
    }

    public void SetPeekProgressImmediate(float progress)
    {
        currentPeekProgress = Mathf.Clamp01(progress);
        targetPeekProgress = currentPeekProgress;
        EnsureReady();
        ApplyPeek(currentPeekProgress);
    }

    public void Configure(string frontSpritePath)
    {
        frontSpriteResourcePath = frontSpritePath;
        EnsureComponents();
        EnsureMaterials();
    }

    [ContextMenu("Test Peek 0.7")]
    private void TestPeek07()
    {
        SetPeekProgressImmediate(0.7f);
    }

    [ContextMenu("Reset Peek")]
    private void ResetPeek()
    {
        SetPeekProgressImmediate(0f);
    }

    private void EnsureReady()
    {
        EnsureComponents();
        BuildCardMesh();
        EnsureMaterials();
        UpdateCollider();
    }

    private void EnsureComponents()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider>();
        }

        if (inputCamera == null)
        {
            inputCamera = Camera.main;
        }
    }

    private void HandlePointerInput()
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
            float dragDelta = position.y - dragStartScreenY;
            targetPeekProgress = Mathf.Clamp01(dragDelta / dragPixelsForFullLift);
        }

        if (TryGetPointerUp())
        {
            isDragging = false;
            activeTouchId = -1;
            targetPeekProgress = 0f;
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

        if (hit.collider != boxCollider)
        {
            return false;
        }

        Vector3 localHit = transform.InverseTransformPoint(hit.point);
        float hingeY = Mathf.Lerp(-height * 0.5f, height * 0.5f, hingePosition);
        if (localHit.y > hingeY + height * 0.25f)
        {
            return false;
        }

        isDragging = true;
        dragStartScreenY = screenPosition.y;
        return true;
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

    private void UpdatePeekProgress(float deltaTime)
    {
        float speed = isDragging ? dragFollowSpeed : returnSpeed;
        float lerp = 1f - Mathf.Exp(-speed * deltaTime);
        float nextProgress = Mathf.Lerp(currentPeekProgress, targetPeekProgress, lerp);

        if (Mathf.Abs(nextProgress - targetPeekProgress) < 0.0001f)
        {
            nextProgress = targetPeekProgress;
        }

        if (!Mathf.Approximately(nextProgress, currentPeekProgress))
        {
            currentPeekProgress = nextProgress;
            ApplyPeek(currentPeekProgress);
        }
    }

    private void BuildCardMesh()
    {
        EnsureComponents();

        int segmentCount = Mathf.Max(4, segments);
        int columns = segmentCount + 1;
        float halfThickness = thickness * 0.5f;

        List<Vector3> vertices = new List<Vector3>(columns * columns * 2 + segmentCount * 16);
        List<Vector2> uvs = new List<Vector2>(vertices.Capacity);
        List<int> frontTriangles = new List<int>(segmentCount * segmentCount * 6);
        List<int> backTriangles = new List<int>(segmentCount * segmentCount * 6);
        List<int> edgeTriangles = new List<int>(segmentCount * 24);

        int frontStart = vertices.Count;
        AddGrid(vertices, uvs, halfThickness, segmentCount, mirrorU: false);
        int backStart = vertices.Count;
        AddGrid(vertices, uvs, -halfThickness, segmentCount, mirrorU: true);

        for (int y = 0; y < segmentCount; y++)
        {
            for (int x = 0; x < segmentCount; x++)
            {
                int bottomLeft = frontStart + y * columns + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + columns;
                int topRight = topLeft + 1;
                AddQuad(frontTriangles, bottomLeft, bottomRight, topRight, topLeft);

                bottomLeft = backStart + y * columns + x;
                bottomRight = bottomLeft + 1;
                topLeft = bottomLeft + columns;
                topRight = topLeft + 1;
                AddQuad(backTriangles, bottomLeft, topLeft, topRight, bottomRight);
            }
        }

        AddEdges(vertices, uvs, edgeTriangles, segmentCount);

        flatVertices = vertices.ToArray();
        deformedVertices = new Vector3[flatVertices.Length];

        if (cardMesh == null)
        {
            cardMesh = new Mesh { name = "Peeking Segmented Poker Card" };
            cardMesh.MarkDynamic();
        }
        else
        {
            cardMesh.Clear();
        }

        cardMesh.vertices = flatVertices;
        cardMesh.uv = uvs.ToArray();
        cardMesh.subMeshCount = 3;
        cardMesh.SetTriangles(frontTriangles.ToArray(), 0);
        cardMesh.SetTriangles(backTriangles.ToArray(), 1);
        cardMesh.SetTriangles(edgeTriangles.ToArray(), 2);
        cardMesh.RecalculateNormals();
        cardMesh.RecalculateBounds();
        meshFilter.sharedMesh = cardMesh;
    }

    private void AddGrid(List<Vector3> vertices, List<Vector2> uvs, float z, int segmentCount, bool mirrorU)
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
                uvs.Add(new Vector2(mirrorU ? 1f - u : u, v));
            }
        }
    }

    private void AddEdges(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, int segmentCount)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float halfThickness = thickness * 0.5f;

        for (int i = 0; i < segmentCount; i++)
        {
            float t0 = i / (float)segmentCount;
            float t1 = (i + 1) / (float)segmentCount;
            float x0 = Mathf.Lerp(-halfWidth, halfWidth, t0);
            float x1 = Mathf.Lerp(-halfWidth, halfWidth, t1);
            float y0 = Mathf.Lerp(-halfHeight, halfHeight, t0);
            float y1 = Mathf.Lerp(-halfHeight, halfHeight, t1);

            AddEdgeQuad(vertices, uvs, triangles,
                new Vector3(x0, -halfHeight, -halfThickness),
                new Vector3(x1, -halfHeight, -halfThickness),
                new Vector3(x1, -halfHeight, halfThickness),
                new Vector3(x0, -halfHeight, halfThickness));

            AddEdgeQuad(vertices, uvs, triangles,
                new Vector3(x0, halfHeight, halfThickness),
                new Vector3(x1, halfHeight, halfThickness),
                new Vector3(x1, halfHeight, -halfThickness),
                new Vector3(x0, halfHeight, -halfThickness));

            AddEdgeQuad(vertices, uvs, triangles,
                new Vector3(-halfWidth, y0, halfThickness),
                new Vector3(-halfWidth, y1, halfThickness),
                new Vector3(-halfWidth, y1, -halfThickness),
                new Vector3(-halfWidth, y0, -halfThickness));

            AddEdgeQuad(vertices, uvs, triangles,
                new Vector3(halfWidth, y0, -halfThickness),
                new Vector3(halfWidth, y1, -halfThickness),
                new Vector3(halfWidth, y1, halfThickness),
                new Vector3(halfWidth, y0, halfThickness));
        }
    }

    private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);
        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
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
        AddQuad(triangles, start, start + 1, start + 2, start + 3);
    }

    private void ApplyPeek(float progress)
    {
        if (cardMesh == null || flatVertices == null || deformedVertices == null)
        {
            return;
        }

        progress = Mathf.Clamp01(progress);
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float hingeY = Mathf.Lerp(-halfHeight, halfHeight, hingePosition);
        float curlLength = Mathf.Max(0.0001f, halfHeight - hingeY);
        float totalAngle = maxCurlAngle * Mathf.Deg2Rad * progress;
        float radius = Mathf.Abs(totalAngle) < 0.0001f ? 0f : curlLength / totalAngle;

        for (int i = 0; i < flatVertices.Length; i++)
        {
            Vector3 flat = flatVertices[i];
            float along = Mathf.Clamp01((flat.y - hingeY) / curlLength);
            float centerWeight = halfWidth > 0.0001f ? 1f - Mathf.Clamp01(Mathf.Abs(flat.x) / halfWidth) : 1f;
            float edgeLag = Mathf.Lerp(1f - sideEdgeLag, 1f, centerWeight);
            float bend = bendCurve != null ? Mathf.Clamp01(bendCurve.Evaluate(along)) : along;
            float lift = liftCurve != null ? Mathf.Clamp01(liftCurve.Evaluate(along)) : along;
            float localAngle = totalAngle * bend * edgeLag;

            float centerY = flat.y;
            float centerZ = 0f;
            float normalY = 0f;
            float normalZ = 1f;

            if (along > 0f && Mathf.Abs(totalAngle) > 0.0001f)
            {
                // Curl the far edge back toward the player-near hinge. This keeps the
                // card-back outside of the curl and reveals the front sprite on the inside.
                float arcY = -Mathf.Sin(localAngle) * radius;
                float arcZ = (1f - Mathf.Cos(localAngle)) * radius * curlDepth;
                centerY = hingeY + arcY;
                centerZ = -(arcZ + maxLiftHeight * progress * lift);
                normalY = -Mathf.Sin(localAngle);
                normalZ = Mathf.Cos(localAngle);
            }

            Vector3 curved = flat;
            curved.y = centerY + flat.z * normalY;
            curved.z = centerZ + flat.z * normalZ;
            curved.z += widthRoundness * progress * Mathf.Sin(along * Mathf.PI) * centerWeight;
            deformedVertices[i] = curved;
        }

        cardMesh.vertices = deformedVertices;
        cardMesh.RecalculateNormals();
        cardMesh.RecalculateBounds();
    }

    private void EnsureMaterials()
    {
        EnsureComponents();
        DestroyRuntimeMaterials();

        frontMaterial = CreateCardMaterial("Peek Card Front", frontSpriteResourcePath, frontColor);
        backMaterial = CreateCardMaterial("Peek Card Back", backSpriteResourcePath, backColor);
        edgeMaterial = CreateSolidMaterial("Peek Card Edge", edgeColor);
        // Submesh 1 is the upper face in CardOpenTestScene's table pose, so it must be the
        // card back at progress 0. Submesh 0 is the hidden underside/front revealed by curl.
        meshRenderer.sharedMaterials = new[] { frontMaterial, backMaterial, edgeMaterial };
    }

    private void UpdateCollider()
    {
        if (boxCollider == null)
        {
            return;
        }

        boxCollider.center = new Vector3(0f, 0f, maxLiftHeight * 0.35f);
        boxCollider.size = new Vector3(width, height, Mathf.Max(0.1f, thickness * 3f + maxLiftHeight));
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
            Debug.LogWarning($"[CardOpenPeekingDemo] Missing card sprite at Resources/{spritePath}");
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
            material.SetFloat("_Cull", 0f);
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

    private static Shader FindCardShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
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

    private void DestroyRuntimeMaterials()
    {
        DestroyMaterial(frontMaterial);
        DestroyMaterial(backMaterial);
        DestroyMaterial(edgeMaterial);
        frontMaterial = null;
        backMaterial = null;
        edgeMaterial = null;
    }

    private static void DestroyMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(material);
        }
        else
        {
            DestroyImmediate(material);
        }
    }

    private void OnDestroy()
    {
        DestroyRuntimeMaterials();

        if (cardMesh == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(cardMesh);
        }
        else
        {
            DestroyImmediate(cardMesh);
        }
    }
}
