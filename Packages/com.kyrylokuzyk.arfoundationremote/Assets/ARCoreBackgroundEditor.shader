Shader "ARFoundationRemote/ARCoreBackgroundEditor" {
    Properties {
        _MainTex("Texture", 2D) = "white" {}
        _EnvironmentDepth("Texture", 2D) = "black" {}
    }

    SubShader {
        Tags {
            "Queue" = "Background"
            "RenderType" = "Background"
            "ForceNoShadowCasting" = "True"
        }
        
        Pass {
            Cull Off
            ZTest Always
            ZWrite On
            Lighting Off
            LOD 100
            Tags
            {
                "LightMode" = "Always"
            }
            
            CGPROGRAM
            #include "UnityCG.cginc"
            #pragma multi_compile_local __ ARCORE_ENVIRONMENT_DEPTH_ENABLED
            uniform float4x4 _UnityDisplayTransform;
            uniform float _UnityCameraForwardScale;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 remapped_uv : TEXCOORD1;
            };
            
            struct fragmentOutput {
                float4 color : SV_Target;
                float depth : SV_Depth;
            };
                 
            #pragma vertex vert
            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                float2 uv = v.uv;
                o.remapped_uv = mul(_UnityDisplayTransform, float4(uv.x, 1.0f - uv.y, 1.0f, 0.0f)).xy;
                return o;
            }
            
            float ConvertDistanceToDepth(float d) {
                d = _UnityCameraForwardScale > 0.0 ? _UnityCameraForwardScale * d : d;

                // Note: On DX11/12, PS4, XboxOne and Metal, the Z buffer range is 1–0 and UNITY_REVERSED_Z is defined. On other platforms, the range is 0–1:
                // https://docs.unity3d.com/Manual/SL-DepthTextures.html
                #if UNITY_REVERSED_Z
                    return (d < _ProjectionParams.y) ? 0.0f : ((1.0f / _ZBufferParams.z) * ((1.0f / d) - _ZBufferParams.w));
                #else
                    // from ARCoreBackground.shader
                    float zBufferParamsW = 1.0 / _ProjectionParams.y;
                    float zBufferParamsY = _ProjectionParams.z * zBufferParamsW;
                    float zBufferParamsX = 1.0 - zBufferParamsY;
                    float zBufferParamsZ = zBufferParamsX * _ProjectionParams.w;
                    return (d < _ProjectionParams.y) ? 1.0f : ((1.0 / zBufferParamsZ) * ((1.0 / d) - zBufferParamsW));
                #endif
            }
       
            sampler2D _MainTex;
            #ifdef ARCORE_ENVIRONMENT_DEPTH_ENABLED
                sampler2D _EnvironmentDepth;
            #endif

            #pragma fragment frag
            fragmentOutput frag (v2f i) {
                fragmentOutput o;
                
                float4 color = tex2D(_MainTex, i.remapped_uv);
                color.a = 1.0;
                o.color = color;
                
                #if UNITY_REVERSED_Z
                    float depth = 0.0;
                #else 
                    float depth = 1.0;
                #endif
                
                #ifdef ARCORE_ENVIRONMENT_DEPTH_ENABLED
                    float envDistance = tex2D(_EnvironmentDepth, i.remapped_uv).x;
                    depth = ConvertDistanceToDepth(envDistance);
                    
                    // uncomment to visualize depth
                    /*float4 black = float4(0,0,0,1);
                    float4 white = float4(1,1,1,1);
                    
                    float _MinDistance = 0.5;
                    float _MaxDistance = 4;
                    float lerpFactor = (envDistance - _MinDistance) / (_MaxDistance - _MinDistance);
                    
                    float4 lerpedColor = lerp(black, white, lerpFactor);
                    o.color = lerpedColor;*/
                #endif
                o.depth = depth;
                
                return o;
            }
            ENDCG
        }
    }
    
    FallBack Off
}
