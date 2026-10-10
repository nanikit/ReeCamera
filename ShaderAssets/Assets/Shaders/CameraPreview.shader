Shader "Ree/CameraPreview" {
    Properties {
        [PerRendererData] _MainTex ("Camera image", 2D) = "white" {}
        _ShowBackground ("Show background", Float) = 1
        _PreviewMask ("Camera coverage", 2D) = "black" {}
    }
    SubShader {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One OneMinusSrcAlpha, Zero OneMinusSrcAlpha

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _PreviewMask;
            fixed _ShowBackground;

            v2f vert(appdata input) {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target {
                fixed4 color = tex2D(_MainTex, input.uv);
                fixed alpha = 1;
                if (_ShowBackground < 0.5) {
                    alpha = tex2D(_PreviewMask, input.uv).r;
                }
                // Separate alpha blending attenuates scene bloom without adding any.
                return fixed4(color.rgb * alpha, alpha);
            }
            ENDCG
        }

        Pass {
            ZTest Greater
            Blend Off
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex coverageVertex
            #pragma fragment coverageFragment
            #include "UnityCG.cginc"

            float4 coverageVertex(uint id : SV_VertexID) : SV_POSITION {
                float2 uv = float2((id << 1) & 2, id & 2);
                #if defined(UNITY_REVERSED_Z)
                    return float4(uv * 2 - 1, 0, 1);
                #else
                    return float4(uv * 2 - 1, 1, 1);
                #endif
            }

            fixed4 coverageFragment() : SV_Target {
                return 1;
            }
            ENDCG
        }
    }
}
