using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CardFlipDemo))]
public sealed class CardFlipDemoEditor : Editor
{
    private SerializedProperty width;
    private SerializedProperty height;
    private SerializedProperty thickness;
    private SerializedProperty segments;

    private SerializedProperty totalDuration;
    private SerializedProperty peekDurationRatio;
    private SerializedProperty snapDurationRatio;
    private SerializedProperty settleDurationRatio;
    private SerializedProperty peekHoldTime;
    private SerializedProperty peekProgress;
    private SerializedProperty snapEndProgress;

    private SerializedProperty maxBend;
    private SerializedProperty inwardCurlAmount;
    private SerializedProperty inwardCurlLift;
    private SerializedProperty inwardCurlVerticalTuck;
    private SerializedProperty customGripStartPosition;
    private SerializedProperty diagonalTilt;

    private SerializedProperty useCornerRoll;
    private SerializedProperty cornerRollLift;
    private SerializedProperty cornerRollCurl;
    private SerializedProperty cornerRollTuck;
    private SerializedProperty cornerFollowDelay;
    private SerializedProperty silhouetteHold;
    private SerializedProperty settleFlex;

    private SerializedProperty peekEase;
    private SerializedProperty snapEase;
    private SerializedProperty settleEase;

    private SerializedProperty backColor;
    private SerializedProperty frontColor;
    private SerializedProperty edgeColor;
    private SerializedProperty useCardSprites;
    private SerializedProperty backSpriteResourcePath;
    private SerializedProperty frontSpriteResourcePath;

    private SerializedProperty autoPlayOnStart;
    private SerializedProperty autoPlayDelay;
    private SerializedProperty revealLiftOffset;
    private SerializedProperty revealTiltDegrees;

    private void OnEnable()
    {
        width = Find("width");
        height = Find("height");
        thickness = Find("thickness");
        segments = Find("segments");

        totalDuration = Find("totalDuration");
        peekDurationRatio = Find("peekDurationRatio");
        snapDurationRatio = Find("snapDurationRatio");
        settleDurationRatio = Find("settleDurationRatio");
        peekHoldTime = Find("peekHoldTime");
        peekProgress = Find("peekProgress");
        snapEndProgress = Find("snapEndProgress");

        maxBend = Find("maxBend");
        inwardCurlAmount = Find("inwardCurlAmount");
        inwardCurlLift = Find("inwardCurlLift");
        inwardCurlVerticalTuck = Find("inwardCurlVerticalTuck");
        customGripStartPosition = Find("customGripStartPosition");
        diagonalTilt = Find("diagonalTilt");

        useCornerRoll = Find("useCornerRoll");
        cornerRollLift = Find("cornerRollLift");
        cornerRollCurl = Find("cornerRollCurl");
        cornerRollTuck = Find("cornerRollTuck");
        cornerFollowDelay = Find("cornerFollowDelay");
        silhouetteHold = Find("silhouetteHold");
        settleFlex = Find("settleFlex");

        peekEase = Find("peekEase");
        snapEase = Find("snapEase");
        settleEase = Find("settleEase");

        backColor = Find("backColor");
        frontColor = Find("frontColor");
        edgeColor = Find("edgeColor");
        useCardSprites = Find("useCardSprites");
        backSpriteResourcePath = Find("backSpriteResourcePath");
        frontSpriteResourcePath = Find("frontSpriteResourcePath");

        autoPlayOnStart = Find("autoPlayOnStart");
        autoPlayDelay = Find("autoPlayDelay");
        revealLiftOffset = Find("revealLiftOffset");
        revealTiltDegrees = Find("revealTiltDegrees");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawCardMesh();
        DrawTiming();

        bool hingeFlip = useCornerRoll.boolValue;
        if (hingeFlip)
        {
            DrawHingeFlip();
        }
        else
        {
            DrawLegacyFlipShape();
        }

        DrawEasing();
        DrawCardVisuals();
        DrawRevealMotion();

        serializedObject.ApplyModifiedProperties();
    }

    private SerializedProperty Find(string propertyName)
    {
        return serializedObject.FindProperty(propertyName);
    }

    private static void Header(string text)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
    }

    private static void DrawProperty(SerializedProperty property)
    {
        EditorGUILayout.PropertyField(property);
    }

    private void DrawCardMesh()
    {
        Header("Card Mesh");
        DrawProperty(width);
        DrawProperty(height);
        DrawProperty(thickness);
        DrawProperty(segments);
    }

    private void DrawTiming()
    {
        Header("Flip Timing");
        DrawProperty(totalDuration);
        DrawProperty(peekDurationRatio);
        DrawProperty(snapDurationRatio);
        DrawProperty(settleDurationRatio);
        DrawProperty(peekHoldTime);

        Header("Flip Progress");
        DrawProperty(peekProgress);
        DrawProperty(snapEndProgress);
    }

    private void DrawHingeFlip()
    {
        Header("Hinge Flip");
        DrawProperty(useCornerRoll);
        DrawHingeSide();
        DrawProperty(cornerRollLift);
        DrawProperty(cornerRollCurl);
        DrawProperty(cornerRollTuck);
        DrawProperty(cornerFollowDelay);
        DrawProperty(silhouetteHold);
        DrawProperty(maxBend);
        DrawProperty(inwardCurlVerticalTuck);
        DrawProperty(settleFlex);
    }

    private void DrawHingeSide()
    {
        Vector2 grip = customGripStartPosition.vector2Value;
        float hingeSide = EditorGUILayout.Slider("Hinge Side", grip.x, -1f, 1f);
        customGripStartPosition.vector2Value = new Vector2(hingeSide, 0f);
    }

    private void DrawLegacyFlipShape()
    {
        Header("Legacy Curl Shape");
        DrawProperty(useCornerRoll);
        DrawProperty(maxBend);
        DrawProperty(inwardCurlAmount);
        DrawProperty(inwardCurlLift);
        DrawProperty(inwardCurlVerticalTuck);
        DrawProperty(customGripStartPosition);
        DrawProperty(diagonalTilt);
    }

    private void DrawEasing()
    {
        Header("Flip Easing");
        DrawProperty(peekEase);
        DrawProperty(snapEase);
        DrawProperty(settleEase);
    }

    private void DrawCardVisuals()
    {
        Header("Card Visuals");
        DrawProperty(backColor);
        DrawProperty(frontColor);
        DrawProperty(edgeColor);
        DrawProperty(useCardSprites);
        if (useCardSprites.boolValue)
        {
            DrawProperty(backSpriteResourcePath);
            DrawProperty(frontSpriteResourcePath);
        }
    }

    private void DrawRevealMotion()
    {
        Header("Reveal Motion");
        DrawProperty(autoPlayOnStart);
        DrawProperty(autoPlayDelay);
        DrawProperty(revealLiftOffset);
        DrawProperty(revealTiltDegrees);
    }
}
