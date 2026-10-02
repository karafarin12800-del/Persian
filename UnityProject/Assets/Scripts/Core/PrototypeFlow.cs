            // Test 1: core scene/player stays alive for 120 frames.
            for (int i = 0; i < 120; i++)
                yield return null;

            startupStatus = "STABLE TEST 2/4: enabling camera...";
            StartupCheckpoint.Set("AndroidIsolationCoreStable");
            Camera activeCamera = mainCameraRoot != null ? mainCameraRoot.GetComponent<Camera>() : null;
            CameraFollow25D follow = activeCamera != null ? activeCamera.GetComponent<CameraFollow25D>() : null;
            if (follow != null)
            {
                follow.SetTarget(player != null ? player.transform : null);
                follow.enabled = true;
            }
            yield return null;
            if (activeCamera != null)
                activeCamera.enabled = true;

            for (int i = 0; i < 120; i++)
                yield return null;

            startupStatus = "STABLE TEST 3A/4: activating input object only...";
            StartupCheckpoint.Set("AndroidIsolationCameraStable");
            MobileInputHub.SetAndroidExecutionArmed(false);

            if (mobileInputRoot != null && !mobileInputRoot.activeSelf)
            {
                mobileInputRoot.SetActive(true);
                yield return null;
                mobileInput = mobileInputRoot.GetComponent<MobileInputHub>();
            }

            // 3A: GameObject/Awake only. No Update/OnGUI/touch code is allowed yet.
            for (int i = 0; i < 60; i++)
                yield return null;

            startupStatus = "STABLE TEST 3B/4: enabling input component (logic still blocked)...";
            if (mobileInput != null)
            {
                mobileInput.EnableMinimap();
                mobileInput.enabled = true;
            }

            // 3B: MonoBehaviour enabled lifecycle only. Update and OnGUI remain blocked.
            for (int i = 0; i < 120; i++)
                yield return null;

            startupStatus = "STABLE TEST 3C/4: enabling touch/UI logic...";
            MobileInputHub.SetAndroidExecutionArmed(true);
            matchInputArmed = true;

            for (int i = 0; i < 120; i++)
                yield return null;

            startupStatus = "STABLE TEST 4/4: enabling enemies...";
            StartupCheckpoint.Set("AndroidIsolationInputStable");
            if (enemySpawner == null)
                enemySpawner = gameRoot != null ? gameRoot.GetComponentInChildren<EnemySpawner>(true) : null;
            if (enemySpawner != null)
            {
                enemySpawner.Configure(player.transform, 8, 44f, 0f);
                enemySpawner.enabled = true;
            }

            for (int i = 0; i < 120; i++)
                yield return null;

            startupStatus = "STABLE: enabling HUD...";
            StartupCheckpoint.Set("AndroidIsolationEnemyStable");
            if (combatHud == null)
                combatHud = gameRoot != null ? gameRoot.GetComponentInChildren<RuntimeCombatHUD>(true) : null;
            if (combatHud != null)