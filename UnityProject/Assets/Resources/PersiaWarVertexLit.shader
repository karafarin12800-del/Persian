Shader "PersiaWar/VertexLit"
{
    // One cheap pass for every procedural model (weapons, pickups, props).
    // Vertex colour rgb = albedo. Vertex colour alpha encodes the surface:
    //   0 matte, 64 plastic, 128 metal (specular), 255 emissive glow.
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _EmissionBoost ("Emission Boost", Range(0, 4)) = 1.6
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.30
        _SpecStrength ("Spec Strength", Range(0, 1)) = 0.55
        _Gloss ("Gloss", Range(4, 128)) = 36
        _AmbientFloor ("Ambient Floor", Range(0, 0.6)) = 0.20
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
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            fixed4 _Color;
            half _EmissionBoost;
            half _RimStrength;
            half _SpecStrength;
            half _Gloss;
            half _AmbientFloor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half3 n = normalize(i.worldNormal);
                half3 l = normalize(_WorldSpaceLightPos0.xyz);
                half3 v = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);

                half3 albedo = i.color.rgb * _Color.rgb;
                half a = i.color.a;
                half specMask = (a <= 0.5h) ? saturate(a * 2.0h) : 0.0h;
                half emission = saturate((a - 0.5h) * 2.0h);

                half ndl = dot(n, l);
                half wrap = saturate(ndl * 0.5h + 0.5h);
                wrap = wrap * wrap;

                half hemi = saturate(n.y * 0.5h + 0.5h);
                half3 ambient = lerp(unity_AmbientGround.rgb, unity_AmbientSky.rgb, hemi);
                ambient = ambient * 0.8h + _AmbientFloor;

                half3 color = albedo * (ambient + _LightColor0.rgb * wrap);

                half3 h = normalize(l + v);
                half spec = pow(saturate(dot(n, h)), _Gloss) * saturate(ndl * 2.0h);
                color += _LightColor0.rgb * spec * specMask * _SpecStrength;

                half rim = pow(1.0h - saturate(dot(n, v)), 3.0h) * _RimStrength;
                color += albedo * rim;

                color += albedo * emission * _EmissionBoost;
                return fixed4(color, 1.0h);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
