using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public static class RuntimeMaterialFactory
    {
        private static Shader cachedShader;

        public static Material Create(string materialName, Color color)
        {
            Shader shader = GetShader();
            if (shader == null)
            {
                Debug.LogError("PersiaWar: no compatible runtime material shader was found.");
                return null;
            }

            Material material = new Material(shader)
            {
                name = materialName,
                color = color,
                enableInstancing = true
            };
            return material;
        }

        public static Material CreateTextured(string materialName, Color tint, Texture2D texture, float tiling)
        {
            Material material = Create(materialName, tint);
            if (material == null || texture == null)
                return material;

            if (material.HasProperty("_MainTex"))
            {
                material.mainTexture = texture;
                material.mainTextureScale = Vector2.one * Mathf.Max(0.01f, tiling);
            }

            return material;
        }

        public static Shader GetShader()
        {
            if (cachedShader != null)
                return cachedShader;

            cachedShader = Resources.Load<Shader>("PersiaWarLit");
            if (cachedShader == null)
                cachedShader = Shader.Find("PersiaWar/Lit");
            if (cachedShader == null)
                cachedShader = Shader.Find("Standard");
            if (cachedShader == null)
                cachedShader = Shader.Find("Legacy Shaders/Diffuse");
            if (cachedShader == null)
                cachedShader = Shader.Find("VertexLit");
            if (cachedShader == null)
                cachedShader = Shader.Find("Sprites/Default");

            return cachedShader;
        }
    }
}