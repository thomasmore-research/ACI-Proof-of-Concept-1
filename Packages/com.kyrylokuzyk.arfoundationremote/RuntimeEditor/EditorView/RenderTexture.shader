Shader "ARFoundationRemote/RenderTexture" {
    Properties {
        _MainTex("_MainTex", 2D) = "white"{}
    }
    
    SubShader {
        Tags {
            "Queue" = "Geometry"
        }
        
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        ENDCG

        Pass {
            Cull Off
            ZTest Always
            ZWrite On
            
            CGPROGRAM
            struct fragmentOutput {
                float4 color : SV_Target;
            };

            #pragma vertex vert_img

            sampler2D _CameraDepthTexture;
            #pragma fragment frag
            fragmentOutput frag (const v2f_img i) {
                fragmentOutput o;
                o.color = tex2D(_MainTex, i.uv);
                return o;
            }
            ENDCG
        }
    }
    
    FallBack Off
}
