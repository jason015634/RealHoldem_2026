Shader "RealHoldem/PokerCardDeform"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _MainTex ("Main Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Color ("Color", Color) = (1, 1, 1, 1)
        _FlipProgress ("Flip Progress", Range(0, 1)) = 0
        _BendAmount ("Bend Amount", Float) = 0
        _LiftAmount ("Lift Amount", Float) = 0
        _FlipRotation ("Flip Rotation", Float) = 0
        _ShineAmount ("Shine Amount", Float) = 0
        _CardWidth ("Card Width", Float) = 0.62
        _CardHeight ("Card Height", Float) = 0.88
        _FlipStyle ("Flip Style", Float) = 0
        _HingeCurl ("Hinge Curl", Float) = 0
        _HingeTuck ("Hinge Tuck", Float) = 0
        _HingeFollowDelay ("Hinge Follow Delay", Float) = 0.48
        _CornerRoll ("Corner Roll", Float) = 0
        _GripStart ("Grip Start", Vector) = (-1, 0, 0, 0)
        _InwardCurlAmount ("Inward Curl Amount", Float) = 0.035
        _InwardCurlLift ("Inward Curl Lift", Float) = 0.02
        _InwardCurlVerticalTuck ("Inward Curl Vertical Tuck", Float) = 0.01
        _DiagonalTilt ("Diagonal Tilt", Float) = 0
        _CornerRollLift ("Corner Roll Lift", Float) = 0.5
        _CornerRollCurl ("Corner Roll Curl", Float) = 0.1
        _CornerRollTuck ("Corner Roll Tuck", Float) = 0.1
        _SettleFlex ("Settle Flex", Float) = 0.006
        _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _MainTex_ST;
                half4 _BaseColor;
                half4 _Color;
                float _FlipProgress;
                float _BendAmount;
                float _LiftAmount;
                float _FlipRotation;
                float _ShineAmount;
                float _CardWidth;
                float _CardHeight;
                float _FlipStyle;
                float _HingeCurl;
                float _HingeTuck;
                float _HingeFollowDelay;
                float _CornerRoll;
                float4 _GripStart;
                float _InwardCurlAmount;
                float _InwardCurlLift;
                float _InwardCurlVerticalTuck;
                float _DiagonalTilt;
                float _CornerRollLift;
                float _CornerRollCurl;
                float _CornerRollTuck;
                float _SettleFlex;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 cardUV : TEXCOORD1;
                float shine : TEXCOORD2;
            };

            float Smooth01(float value)
            {
                value = saturate(value);
                return value * value * (3.0 - 2.0 * value);
            }

            float InvLerp(float a, float b, float value)
            {
                return saturate((value - a) / max(0.0001, b - a));
            }

            float3 DeformCard(float3 positionOS, float2 cardUV)
            {
                // This is the shader port of CardFlipDemo.ApplyBend; C# still owns transform motion.
                float progress = saturate(_FlipProgress);
                float halfWidth = max(0.0001, _CardWidth * 0.5);
                float halfHeight = max(0.0001, _CardHeight * 0.5);
                float3 p = positionOS;

                if (_CornerRoll > 0.5)
                {
                    float edgeSign = _GripStart.x < 0.0 ? -1.0 : 1.0;
                    float fromHinge01 = edgeSign < 0.0
                        ? InvLerp(-halfWidth, halfWidth, positionOS.x)
                        : InvLerp(halfWidth, -halfWidth, positionOS.x);
                    float y01 = saturate(abs(positionOS.y) / halfHeight);
                    float middleBias = 1.0 - Smooth01(InvLerp(0.08, 0.92, y01));
                    float hingeBand = 1.0 - Smooth01(InvLerp(0.0, 0.28, fromHinge01));
                    float freeEdge = Smooth01(InvLerp(0.35, 1.0, fromHinge01));
                    float sheetArc = sin(fromHinge01 * PI);
                    float preCurl = Smooth01(InvLerp(0.12, 0.45, progress)) * (1.0 - Smooth01(InvLerp(0.55, 0.78, progress)));
                    float hingeFlex = Smooth01(InvLerp(0.34, 0.68, progress)) * (1.0 - Smooth01(InvLerp(0.84, 1.0, progress)));
                    float flipFlex = sin(saturate(InvLerp(0.48, 0.9, progress)) * PI);
                    float settleWindow = sin(saturate(InvLerp(0.86, 1.0, progress)) * PI);
                    float delayedFollow = Smooth01(InvLerp(fromHinge01 * _HingeFollowDelay, fromHinge01 * _HingeFollowDelay + 0.32, progress));

                    p.x += -edgeSign * _CornerRollTuck * preCurl * hingeBand * lerp(0.35, 1.0, middleBias);
                    p.z -= _CornerRollLift * 0.45 * preCurl * hingeBand * lerp(0.4, 1.0, middleBias);
                    p.z -= _CornerRollCurl * hingeFlex * freeEdge * lerp(0.45, 1.0, delayedFollow);
                    p.z -= _BendAmount * flipFlex * sheetArc * 0.55;
                    p.z -= _SettleFlex * settleWindow * sheetArc;
                    p.y += sign(positionOS.y) * _InwardCurlVerticalTuck * preCurl * hingeBand * (1.0 - middleBias);
                }
                else
                {
                    float curlIn = Smooth01(InvLerp(0.02, 0.34, progress));
                    float curlOut = Smooth01(InvLerp(0.64, 0.98, progress));
                    float curl = curlIn * (1.0 - curlOut);
                    float snapWindow = sin(saturate(InvLerp(0.2, 0.78, progress)) * PI);
                    float preFlipPull = Smooth01(InvLerp(0.0, 0.26, progress)) * (1.0 - Smooth01(InvLerp(0.34, 0.62, progress)));
                    float2 grip = clamp(_GripStart.xy, -1.0, 1.0);
                    float2 gripToCenter = -grip;
                    gripToCenter = dot(gripToCenter, gripToCenter) < 0.0001 ? float2(-1.0, 0.0) : gripToCenter;
                    float gripToCenterDistance = max(0.2, length(gripToCenter));
                    float2 gripToCenterDirection = normalize(gripToCenter);
                    float3 localTowardCenter = normalize(float3(gripToCenterDirection.x * halfWidth, gripToCenterDirection.y * halfHeight, 0.0));
                    float2 normalizedPoint = float2(positionOS.x / halfWidth, positionOS.y / halfHeight);
                    float2 fromGrip = normalizedPoint - grip;
                    float distanceFromGrip = length(fromGrip);
                    float along = dot(fromGrip, gripToCenterDirection);
                    float along01 = saturate(along / gripToCenterDistance);
                    float perpendicular = abs((fromGrip.x * gripToCenterDirection.y) - (fromGrip.y * gripToCenterDirection.x));
                    float corridorInfluence = 1.0 - Smooth01(InvLerp(0.22, 1.35, perpendicular));
                    float distanceInfluence = 1.0 - Smooth01(InvLerp(0.25, 1.9, distanceFromGrip));
                    float pathInfluence = saturate(corridorInfluence * max(distanceInfluence, 0.15));
                    float leadInfluence = pathInfluence * lerp(1.18, 0.38, along01);
                    float arcInfluence = pathInfluence * sin(along01 * PI);
                    float localCurlIn = Smooth01(InvLerp(0.02 + along01 * 0.22, 0.34 + along01 * 0.22, progress));
                    float localCurl = localCurlIn * (1.0 - curlOut);
                    float twistSign = (fromGrip.x * gripToCenterDirection.y) - (fromGrip.y * gripToCenterDirection.x);
                    float diagonalRadians = radians(_DiagonalTilt);

                    p += localTowardCenter * (_InwardCurlAmount * 0.2 * preFlipPull * leadInfluence);
                    p.y += localTowardCenter.y * _InwardCurlVerticalTuck * localCurl * pathInfluence;
                    p.z -= _InwardCurlLift * preFlipPull * leadInfluence;
                    p.z -= _BendAmount * localCurl * (arcInfluence + leadInfluence * 0.35);
                    p.z -= _BendAmount * 0.72 * snapWindow * pathInfluence * (0.45 + arcInfluence);
                    p.z -= diagonalRadians * 0.018 * curl * pathInfluence * twistSign;
                }

                return p;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float halfWidth = max(0.0001, _CardWidth * 0.5);
                float halfHeight = max(0.0001, _CardHeight * 0.5);
                float2 cardUV = float2(
                    saturate((input.positionOS.x + halfWidth) / (halfWidth * 2.0)),
                    saturate((input.positionOS.y + halfHeight) / (halfHeight * 2.0)));
                float3 deformedOS = DeformCard(input.positionOS.xyz, cardUV);

                output.positionHCS = TransformObjectToHClip(deformedOS);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.cardUV = cardUV;

                output.shine = 0.0;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                color.rgb += input.shine.xxx;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
