Shader "PersiaWar/VertexLit"
{
    Properties
    {
        _Color ("Base Color", Color) = (1,1,1,1)
        _EmissionBoost ("Emission Boost", Range(0,2)) = 0
        _RimStrength ("Rim Strength", Range(0,2)) = 0.18
        _SpecStrength ("Specular Strength", Range(0,1)) = 0.22
        _Gloss ("Gloss", Range(0,1)) = 0.35
        _AmbientFloor ("Ambient Floor", Range(0,1)) = 0.22
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        LOD 150

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull Back
            ZWrite On

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
                float4 position : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                fixed4 vertexColor : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.vertexColor = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half3 normal = normalize(i.worldNormal);
                half3 lightDirection = normalize(UnityWorldSpaceLightDir(i.worldPosition));
                half3 viewDirection = normalize(UnityWorldSpaceViewDir(i.worldPosition));
                half3 halfDirection = normalize(lightDirection + viewDirection);

                half diffuseDot = dot(normal, lightDirection);
                half wrappedDiffuse = saturate((diffuseDot + 0.35h) / 1.35h);

                fixed4 baseColor = _Color * i.vertexColor;
                half3 ambient = max(UNITY_LIGHTMODEL_AMBIENT.rgb, _AmbientFloor.xxx);
                half3 diffuse = baseColor.rgb * (ambient + _LightColor0.rgb * wrappedDiffuse);

                half specularPower = exp2(_Gloss * 10.0h + 1.0h);
                half specular = pow(saturate(dot(normal, halfDirection)), specularPower);
                specular *= _SpecStrength * saturate(diffuseDot);

                half rim = pow(1.0h - saturate(dot(normal, viewDirection)), 3.0h);
                rim *= _RimStrength;

                half3 finalColor = diffuse
                    + _LightColor0.rgb * specular
                    + baseColor.rgb * rim
                    + baseColor.rgb * _EmissionBoost;

                return fixed4(finalColor, baseColor.a);
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}
