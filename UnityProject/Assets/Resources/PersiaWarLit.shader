Shader "PersiaWar/Lit"
{
 Properties { _Color ("Tint", Color) = (1,1,1,1) _MainTex ("Albedo", 2D) = "white" {} _Glossiness ("Smoothness", Range(0,1)) = 0.25 _Metallic ("Metallic", Range(0,1)) = 0 }
 SubShader
 {
  Tags { "RenderType"="Opaque" "Queue"="Geometry" }
  LOD 200
  CGPROGRAM
  #pragma surface surf Lambert fullforwardshadows addshadow
  #pragma target 3.0
  sampler2D _MainTex;
  fixed4 _Color;
  half _Glossiness;
  half _Metallic;
  struct Input { float2 uv_MainTex; float4 color : COLOR; };
  void surf(Input IN, inout SurfaceOutput o)
  {
   fixed4 texel = tex2D(_MainTex, IN.uv_MainTex);
   fixed3 vertexTint = (IN.color.a > 0.0) ? IN.color.rgb : fixed3(1,1,1);
   o.Albedo = texel.rgb * _Color.rgb * vertexTint;
   o.Alpha = texel.a * _Color.a;
  }
  ENDCG
 }
 FallBack "Diffuse"
}