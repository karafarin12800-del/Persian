            renderer.sharedMaterial = entry.Key;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        for (int i = 0; i < sourceObjects.Count; i++)