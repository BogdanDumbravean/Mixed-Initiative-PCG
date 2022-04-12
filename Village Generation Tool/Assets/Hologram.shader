Shader "Unlit/Hologram"
{
    SubShader
    {
        Tags { 
            "RenderType"="Transparent"
            "Queue" = "Transparent" 
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 wPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.wPos = mul(UNITY_MATRIX_M, float4(v.vertex.xyz, 1));
                o.normal = mul((float3x3)UNITY_MATRIX_M, v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dirToCam = normalize(_WorldSpaceCameraPos.xyzx - i.wPos);
                float fresnel = pow(1 - dot(dirToCam, normalize(i.normal)), 2);
                fresnel = lerp(0.3, 0.8, fresnel);

                return float4(0, 1, 0, fresnel);
            }
            ENDCG
        }
    }
}
