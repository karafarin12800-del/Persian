Shader "PersiaWar/CharacterLit"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.58
        _SpecStrength ("Metallic Response", Range(0,1)) = 0.95
        _Gloss ("Surface Gloss", Range(4,128)) = 72
        _EmissionBoost ("Emission Boost", Range(0,4)) = 2.4
        _AmbientFloor ("Ambient Floor", Range(0,0.6)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        #include "UnityCG.cginc"

        fixed4 _Color;
        half _RimStrength;
        half _SpecStrength;
        half _Gloss;
        half _EmissionBoost;
        half _AmbientFloor;

        struct Input
        {
            fixed4 color : COLOR;
            float3 worldPos;
            float3 viewDir;
            INTERNAL_DATA
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 vertex = IN.color;
            half3 albedo = vertex.rgb * _Color.rgb;

            #if !defined(UNITY_COLORSPACE_GAMMA)
                albedo = GammaToLinearSpace(albedo);
            #endif

            // MeshKit vertex alpha encodes the surface: matte, plastic, metal, emissive.
            half surfaceCode = vertex.a;
            o.Albedo = albedo;
            o.Metallic = saturate((surfaceCode - 0.18h) * 3.1h) * _SpecStrength;
            o.Smoothness = lerp(0.20h, 0.84h, saturate(surfaceCode * 1.8h));

            half emissionMask = saturate((surfaceCode - 0.72h) * 3.6h);
            half3 worldNormal = normalize(WorldNormalVector(IN, o.Normal));
            half3 worldView = normalize(UnityWorldSpaceViewDir(IN.worldPos));
            half rim = pow(1.0h - saturate(dot(worldNormal, worldView)), 3.0h);

            o.Emission = albedo * (emissionMask * _EmissionBoost + rim * _RimStrength);
            o.Occlusion = saturate(0.72h + _AmbientFloor);
            o.Alpha = 1.0h;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
