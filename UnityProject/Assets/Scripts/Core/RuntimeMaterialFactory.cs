using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Provides a build-safe lit material shader for runtime-generated 3D objects.
    /// The shader lives under Resources so Android builds cannot lose it to shader stripping.
    /// </summary>
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
