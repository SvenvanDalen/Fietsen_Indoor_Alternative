// Terrein: mengt vier bodemlagen (gras, rots, kale grond, bosbodem) op de gewichten in de vertexkleur
// (r, g, b, a), uitgerekend door TerrainBlend. Texturen liggen in wereldruimte, zodat de mesh geen UV's nodig heeft.
// Rots wordt triplanar gelegd, zodat hij op steile wanden niet uitrekt. Tegen zichtbare herhaling: elke laag
// ook op een grovere schaal, die het verder weg overneemt, en grote vlekken in kleur en helderheid.
Shader "FietsApp/Terrein"
{
    Properties
    {
        [NoScaleOffset] _GrassMap("Gras", 2D) = "white" {}
        [NoScaleOffset][Normal] _GrassNormal("Gras normaal", 2D) = "bump" {}
        [NoScaleOffset] _RockMap("Rots", 2D) = "white" {}
        [NoScaleOffset][Normal] _RockNormal("Rots normaal", 2D) = "bump" {}
        [NoScaleOffset] _BareMap("Kale grond", 2D) = "white" {}
        [NoScaleOffset][Normal] _BareNormal("Kale grond normaal", 2D) = "bump" {}
        [NoScaleOffset] _ForestMap("Bosbodem", 2D) = "white" {}
        [NoScaleOffset][Normal] _ForestNormal("Bosbodem normaal", 2D) = "bump" {}

        _TileSize("Tegelmaat in m (gras, rots, kaal, bos)", Vector) = (4, 10, 3, 4)
        _Tint0("Kleur gras", Color) = (1, 1, 1, 1)
        _Tint1("Kleur rots", Color) = (1, 1, 1, 1)
        _Tint2("Kleur kale grond", Color) = (1, 1, 1, 1)
        _Tint3("Kleur bosbodem", Color) = (1, 1, 1, 1)
        _FarScale("Grove schaal (keer de tegelmaat)", Float) = 7
        _FarDistance("Afstand waarop de grove schaal het overneemt (m)", Float) = 120
        _NormalStrength("Sterkte normal maps", Range(0, 2)) = 1
        _NormalDistance("Normal maps tot (m)", Float) = 80
        _MacroStrength("Sterkte vlekken", Range(0, 1)) = 0.35
        _DryGrass("Droog gras in de vlekken", Color) = (0.86, 0.80, 0.52, 1)
        _Smoothness("Gladheid", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _TileSize;
            half4 _Tint0, _Tint1, _Tint2, _Tint3;
            float _FarScale, _FarDistance, _NormalStrength, _NormalDistance, _MacroStrength;
            half4 _DryGrass;
            half _Smoothness;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_GrassMap); TEXTURE2D(_GrassNormal);
            TEXTURE2D(_RockMap); TEXTURE2D(_RockNormal);
            TEXTURE2D(_BareMap); TEXTURE2D(_BareNormal);
            TEXTURE2D(_ForestMap); TEXTURE2D(_ForestNormal);
            SAMPLER(sampler_GrassMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 weights : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.weights = input.color;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            // Waarderuis 0..1 voor de grote vlekken; goedkoper dan een extra textuur.
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            // Kleur en normaal van één laag, van bovenaf gelegd; dichtbij en grof gemengd op afstand.
            void SampleTop(TEXTURE2D_PARAM(colorMap, samplerColor), TEXTURE2D(normalMap), float2 uv, float far, bool withNormal,
                inout half3 color, inout half2 normal, half weight)
            {
                half3 nearColor = SAMPLE_TEXTURE2D(colorMap, samplerColor, uv).rgb;
                half3 farColor = SAMPLE_TEXTURE2D(colorMap, samplerColor, uv / _FarScale).rgb;
                color += lerp(nearColor, farColor, far) * weight;
                if (withNormal)
                    normal += UnpackNormal(SAMPLE_TEXTURE2D(normalMap, samplerColor, uv)).xy * weight * (1.0 - far);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 positionWS = input.positionWS;
                float3 normalWS = normalize(input.normalWS);
                float distance = length(GetCameraPositionWS() - positionWS);
                float far = saturate(distance / _FarDistance - 0.25);
                bool withNormal = distance < _NormalDistance;

                half4 w = input.weights;
                w /= max(w.r + w.g + w.b + w.a, 1e-3);

                half3 color = 0;
                half2 normalTop = 0;   // afwijking van de normaal voor de lagen van bovenaf (in xz)
                half3 rockNormal = 0;  // triplanare rotsnormaal, al in wereldruimte

                UNITY_BRANCH if (w.r > 0.004)
                {
                    half3 c = 0; half2 n = 0;
                    SampleTop(TEXTURE2D_ARGS(_GrassMap, sampler_GrassMap), _GrassNormal, positionWS.xz / _TileSize.x, far, withNormal, c, n, 1);
                    // Droge en donkere plekken in het gras, zoals op een echte berghelling.
                    float dry = smoothstep(0.45, 0.8, ValueNoise(positionWS.xz / 90.0 + 17.0));
                    c = lerp(c, c * _DryGrass.rgb * 1.25, dry * _MacroStrength * 1.6);
                    color += c * _Tint0.rgb * w.r;
                    normalTop += n * w.r;
                }
                UNITY_BRANCH if (w.b > 0.004)
                {
                    half3 c = 0; half2 n = 0;
                    SampleTop(TEXTURE2D_ARGS(_BareMap, sampler_GrassMap), _BareNormal, positionWS.xz / _TileSize.z, far, withNormal, c, n, 1);
                    color += c * _Tint2.rgb * w.b;
                    normalTop += n * w.b;
                }
                UNITY_BRANCH if (w.a > 0.004)
                {
                    half3 c = 0; half2 n = 0;
                    SampleTop(TEXTURE2D_ARGS(_ForestMap, sampler_GrassMap), _ForestNormal, positionWS.xz / _TileSize.w, far, withNormal, c, n, 1);
                    color += c * _Tint3.rgb * w.a;
                    normalTop += n * w.a;
                }
                UNITY_BRANCH if (w.g > 0.004)
                {
                    // Triplanar: drie projecties, gewogen naar de richting van de helling.
                    float3 blend = pow(abs(normalWS), 4.0);
                    blend /= blend.x + blend.y + blend.z;
                    float3 p = positionWS / _TileSize.y;
                    float scale = lerp(1.0, 1.0 / _FarScale, far);
                    half3 cx = SAMPLE_TEXTURE2D(_RockMap, sampler_GrassMap, p.zy * scale).rgb;
                    half3 cy = SAMPLE_TEXTURE2D(_RockMap, sampler_GrassMap, p.xz * scale).rgb;
                    half3 cz = SAMPLE_TEXTURE2D(_RockMap, sampler_GrassMap, p.xy * scale).rgb;
                    color += (cx * blend.x + cy * blend.y + cz * blend.z) * _Tint1.rgb * w.g;
                    if (withNormal)
                    {
                        // Whiteout-menging per projectie, terug naar wereldruimte.
                        half3 nx = UnpackNormal(SAMPLE_TEXTURE2D(_RockNormal, sampler_GrassMap, p.zy));
                        half3 ny = UnpackNormal(SAMPLE_TEXTURE2D(_RockNormal, sampler_GrassMap, p.xz));
                        half3 nz = UnpackNormal(SAMPLE_TEXTURE2D(_RockNormal, sampler_GrassMap, p.xy));
                        float3 tx = float3(0, nx.y, nx.x) * sign(normalWS.x);
                        float3 ty = float3(ny.x, 0, ny.y) * sign(normalWS.y);
                        float3 tz = float3(nz.x, nz.y, 0) * sign(normalWS.z);
                        rockNormal = (tx * blend.x + ty * blend.y + tz * blend.z) * w.g * (1.0 - far);
                    }
                }

                // Grote vlekken lichter en donkerder, over alle lagen.
                float macro = ValueNoise(positionWS.xz / 260.0) * 0.65 + ValueNoise(positionWS.xz / 45.0) * 0.35;
                color *= lerp(1.0 - _MacroStrength * 0.5, 1.0 + _MacroStrength * 0.35, macro);

                float3 bumped = normalWS + (float3(normalTop.x, 0, normalTop.y) + rockNormal) * _NormalStrength;
                bumped = normalize(bumped);

                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = bumped;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(bumped);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = color;
                surface.smoothness = _Smoothness;
                surface.occlusion = 1;
                surface.alpha = 1;

                half4 lit = UniversalFragmentPBR(inputData, surface);
                lit.rgb = MixFog(lit.rgb, inputData.fogCoord);
                return lit;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };

            float4 Vert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection)));
            }

            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Vert(float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            // Voor de schaduw in hoeken en kuilen (SSAO).
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target { return half4(NormalizeNormalPerPixel(input.normalWS), 0); }
            ENDHLSL
        }
    }
}
