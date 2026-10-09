using System.Collections.Generic;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public static class RuntimeMaterialFactory
    {
        private static Shader cachedShader;
        private static readonly Dictionary<int, Material> sharedMaterials = new Dictionary<int, Material>();
        private static bool shaderCheckpointWritten;

        public static Material Create(string materialName, Color color)
        {
            Shader shader = GetShader();
            if (shader == null)
            {
                Debug.LogError("PersiaWar: no compatible runtime material shader was found.");
                StartupCheckpoint.Set("RuntimeShaderMissing");
                return null;
            }

            int key = ComputeKey(shader, color);
            if (sharedMaterials.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            Material material = new Material(shader)
            {
                name = materialName,
                color = color,
                enableInstancing = true
            };
            ApplyTintToSupportedColorProperties(material, color);
            ApplyWhiteBaseTextureWhenRequired(material);
            sharedMaterials[key] = material;
            return material;
        }

        public static Material CreateTextured(string materialName, Color tint, Texture2D texture, float tiling)
        {
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
                StartupCheckpoint.Set("RuntimeShaderMissing");
                return null;
            }

            Material material = new Material(shader)
            {
                name = materialName,
                color = color,
                enableInstancing = true
            };
            ApplyTintToSupportedColorProperties(material, color);
            ApplyWhiteBaseTextureWhenRequired(material);
            return material;
        }

        // Unlit/Texture is selected on Android for compatibility. Without a white
        // base texture, flat-color materials can render black or appear untinted.
        // Textured materials replace this with their real texture in CreateTextured.
        private static void ApplyWhiteBaseTextureWhenRequired(Material material)
        {
            if (material != null && material.HasProperty("_MainTex") && material.mainTexture == null)
                material.mainTexture = Texture2D.whiteTexture;
        }

        private static void ApplyTintToSupportedColorProperties(Material material, Color color)
        {
            if (material == null)
                return;

            // Explicit property assignment works with the bundled Android shader and
            // avoids depending on a shader's implicit "main color" alias.
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
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

            if (!shaderCheckpointWritten)
            {
                shaderCheckpointWritten = true;
                StartupCheckpoint.Set("RuntimeShaderLookupStarted");
            }

#if UNITY_ANDROID
            // A project-owned shader in Resources is included in the Android build.
            // Shader.Find fallbacks can be stripped; a lit fallback desaturates grass
            // and can make road meshes without explicit normals look almost black.
            cachedShader = Resources.Load<Shader>("PersiaWarAndroidFlat");
            if (cachedShader == null)
                cachedShader = Shader.Find("Unlit/Texture");
            if (cachedShader == null)
                cachedShader = Shader.Find("Unlit/Color");
#endif

            if (cachedShader == null)
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

            if (cachedShader != null)
            {
                StartupCheckpoint.Set("RuntimeShaderReady");
                Debug.Log("PERSIA_RUNTIME_SHADER: " + cachedShader.name);
            }

            return cachedShader;
        }
    }
}