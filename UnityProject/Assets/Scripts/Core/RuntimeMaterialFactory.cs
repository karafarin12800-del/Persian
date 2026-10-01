using System.Collections.Generic;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public static class RuntimeMaterialFactory
    {
        private static Shader cachedShader;
        private static readonly Dictionary<int, Material> sharedMaterials = new Dictionary<int, Material>();

        public static Material Create(string materialName, Color color)
        {
            Shader shader = GetShader();
            if (shader == null)
            {
                Debug.LogError("PersiaWar: no compatible runtime material shader was found.");
                return null;
            }

            // The procedural city creates hundreds of renderers. Reusing materials by
            // shader+color prevents a large material/heap spike on Android while keeping
            // the visual appearance unchanged. Callers still receive a sharedMaterial.
            int key = ComputeKey(shader, color);
            if (sharedMaterials.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            Material material = new Material(shader)
            {
                name = materialName,
                color = color,
                enableInstancing = true
            };
            sharedMaterials[key] = material;
            return material;
        }

        public static Material CreateTextured(string materialName, Color tint, Texture2D texture, float tiling)
        {
            // Textured materials are intentionally unique because their texture/tiling
            // state is part of the material identity.
            Material material = CreateUnique(materialName, tint);
            if (material == null || texture == null)
                return material;

            if (material.HasProperty("_MainTex"))
            {
                material.mainTexture = texture;
                material.mainTextureScale = Vector2.one * Mathf.Max(0.01f, tiling);
            }

            return material;
        }

        private static Material CreateUnique(string materialName, Color color)
        {
            Shader shader = GetShader();
            if (shader == null)
            {
                Debug.LogError("PersiaWar: no compatible runtime material shader was found.");
                return null;
            }

            return new Material(shader)
            {
                name = materialName,
                color = color,
                enableInstancing = true
            };
        }

        private static int ComputeKey(Shader shader, Color color)
        {
            unchecked
            {
                int hash = shader.GetInstanceID();
                hash = hash * 31 + Mathf.RoundToInt(color.r * 255f);
                hash = hash * 31 + Mathf.RoundToInt(color.g * 255f);
                hash = hash * 31 + Mathf.RoundToInt(color.b * 255f);
                hash = hash * 31 + Mathf.RoundToInt(color.a * 255f);
                return hash;
            }
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
