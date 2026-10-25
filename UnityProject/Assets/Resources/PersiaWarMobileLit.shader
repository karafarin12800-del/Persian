Shader "PersiaWar/MobileLit"
{
    Properties
    {
        _Color ("Albedo Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _EmissionColor ("Emission", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _EmissionColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPosition : TEXCOORD2;
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                half3 normal = normalize(i.worldNormal);
                half3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.worldPosition);
                half hemiPosition = saturate(normal.y * 0.5h + 0.5h);
                half3 lowerAmbient = lerp(unity_AmbientGround.rgb, unity_AmbientEquator.rgb,
                    saturate(hemiPosition * 2.0h));
                half3 upperAmbient = lerp(unity_AmbientEquator.rgb, unity_AmbientSky.rgb,
                    saturate((hemiPosition - 0.5h) * 2.0h));
                half3 ambient = hemiPosition < 0.5h ? lowerAmbient : upperAmbient;
                half3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                half lambert = saturate(dot(normal, lightDirection));
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                half3 diffuse = albedo.rgb * (ambient + _LightColor0.rgb * lambert);
                half rim = pow(1.0h - saturate(dot(normal, viewDirection)), 3.0h) * 0.18h;
                half3 color = diffuse + _EmissionColor.rgb + albedo.rgb * rim;
                return fixed4(color, albedo.a);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
