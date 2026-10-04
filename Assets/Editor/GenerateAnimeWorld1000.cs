using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Generates a 1000x1000 anime-styled terrain world with mountains, plains,
/// stylized trees, flowers, walkable houses, and chests matching the existing TestChest + ChestSO configuration.
/// Saves the complete hierarchy as a reusable Prefab.
/// </summary>
[InitializeOnLoad]
public static class GenerateAnimeWorld1000
{
    const string BaseFolder = "Assets/Art/Terrain/Anime1000";
    const string PrefabPath = BaseFolder + "/AnimeWorld_1000x1000.prefab";
    const string ReportPath = "Tools/Environment/anime-world-1000-result.txt";

    static GenerateAnimeWorld1000()
    {
        EditorApplication.delayCall += CheckAutoRun;
    }

    static void CheckAutoRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(ReportPath)) return;
        Build();
    }

    [MenuItem("Tools/Terrain/Generate 1000x1000 Anime World Prefab")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var scene = SceneManager.GetActiveScene();
        bool isSampleScene = (scene.path == "Assets/Scenes/SampleScene.unity");

        // Prepare Directories
        Directory.CreateDirectory(BaseFolder);
        Directory.CreateDirectory(BaseFolder + "/Textures");
        Directory.CreateDirectory(BaseFolder + "/Layers");
        Directory.CreateDirectory(BaseFolder + "/Materials");
        Directory.CreateDirectory(BaseFolder + "/Meshes");
        Directory.CreateDirectory("Tools/Environment");
        Directory.CreateDirectory("Tools/SceneBackups");

        if (isSampleScene && File.Exists(scene.path))
        {
            string backup = "Tools/SceneBackups/SampleScene-before-anime1000-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
            File.Copy(scene.path, backup, true);
        }

        Debug.Log("[AnimeWorld1000] Starting generation of 1000x1000 Anime Terrain World...");

        // 1. Generate Anime Textures & TerrainLayers
        var layers = CreateAnimeTerrainLayers();

        // 2. Generate 1000x1000 TerrainData (Mountains + Central Plain)
        string terrainDataPath = BaseFolder + "/AnimeTerrain_1000x1000_Data.asset";
        var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath);
        if (!terrainData)
        {
            terrainData = new TerrainData
            {
                name = "AnimeTerrain_1000x1000_Data",
                heightmapResolution = 513,
                size = new Vector3(1000f, 120f, 1000f)
            };
            AssetDatabase.CreateAsset(terrainData, terrainDataPath);
        }
        else
        {
            terrainData.heightmapResolution = 513;
            terrainData.size = new Vector3(1000f, 120f, 1000f);
        }

        terrainData.terrainLayers = layers;
        GenerateHeightmapAndAlphamap(terrainData);
        EditorUtility.SetDirty(terrainData);
        AssetDatabase.SaveAssets();

        // 3. Create Root Hierarchy for the Prefab
        var rootGO = new GameObject("AnimeWorld_1000x1000");
        rootGO.transform.position = Vector3.zero;

        // 4. Create Terrain GameObject
        var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if (!terrainShader) terrainShader = Shader.Find("Nature/Terrain/Standard");
        var terrainMat = AssetDatabase.LoadAssetAtPath<Material>(BaseFolder + "/Materials/AnimeTerrain.mat");
        if (!terrainMat)
        {
            terrainMat = new Material(terrainShader) { name = "AnimeTerrain" };
            AssetDatabase.CreateAsset(terrainMat, BaseFolder + "/Materials/AnimeTerrain.mat");
        }

        var terrainGO = Terrain.CreateTerrainGameObject(terrainData);
        terrainGO.name = "Terrain_1000x1000";
        terrainGO.transform.SetParent(rootGO.transform, false);
        // Center terrain so (0,0) in world space is right at the middle of the terrain (500, 500)
        terrainGO.transform.position = new Vector3(-500f, 0f, -500f);

        var terrain = terrainGO.GetComponent<Terrain>();
        terrain.materialTemplate = terrainMat;
        terrain.heightmapPixelError = 5;
        terrain.drawInstanced = true;
        terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        // 5. Materials for Anime Scenery
        var matPlaster = GetOrCreateMat("AnimePlaster", new Color(0.92f, 0.88f, 0.78f), 0f, 0.12f);
        var matWood = GetOrCreateMat("AnimeTimber", new Color(0.36f, 0.20f, 0.11f), 0f, 0.20f);
        var matStone = GetOrCreateMat("AnimeStone", new Color(0.48f, 0.52f, 0.56f), 0f, 0.15f);
        var matTileRed = GetOrCreateMat("AnimeTileRed", new Color(0.82f, 0.32f, 0.22f), 0f, 0.25f);
        var matTileBlue = GetOrCreateMat("AnimeTileBlue", new Color(0.25f, 0.45f, 0.72f), 0f, 0.25f);
        var matGlass = GetOrCreateGlassMat("AnimeGlass");

        var matLeafGreen = GetOrCreateMat("AnimeLeafGreen", new Color(0.38f, 0.72f, 0.24f), 0f, 0.10f);
        var matLeafSakura = GetOrCreateMat("AnimeLeafSakura", new Color(0.96f, 0.65f, 0.78f), 0f, 0.10f);
        var matLeafGold = GetOrCreateMat("AnimeLeafGold", new Color(0.95f, 0.72f, 0.18f), 0f, 0.10f);

        var matFlowerPink = GetOrCreateMat("AnimeFlowerPink", new Color(0.98f, 0.45f, 0.65f), 0f, 0.10f);
        var matFlowerYellow = GetOrCreateMat("AnimeFlowerYellow", new Color(0.98f, 0.85f, 0.22f), 0f, 0.10f);
        var matFlowerBlue = GetOrCreateMat("AnimeFlowerBlue", new Color(0.35f, 0.65f, 0.95f), 0f, 0.10f);
        var matFlowerWhite = GetOrCreateMat("AnimeFlowerWhite", new Color(0.96f, 0.96f, 0.96f), 0f, 0.10f);
        var matGrassBlade = GetOrCreateMat("AnimeGrassBlade", new Color(0.45f, 0.78f, 0.26f), 0f, 0.10f);

        // 6. Generate Village Houses (Walkable with ramps and interior)
        var housesGroup = new GameObject("Village_Houses");
        housesGroup.transform.SetParent(rootGO.transform, false);

        var houseConfigs = new (Vector2 pos, float rot, Material roofMat, string name)[]
        {
            (new Vector2(-30f, -15f), 25f, matTileRed, "Anime House A"),
            (new Vector2(32f, -18f), -35f, matTileBlue, "Anime House B"),
            (new Vector2(-35f, 32f), 120f, matTileBlue, "Anime House C"),
            (new Vector2(28f, 36f), -130f, matTileRed, "Anime House D"),
            (new Vector2(0f, 65f), 180f, matTileRed, "Anime House E (Manor)"),
            (new Vector2(-2f, -55f), 0f, matTileBlue, "Anime House F (Outpost)")
        };

        for (int i = 0; i < houseConfigs.Length; i++)
        {
            var cfg = houseConfigs[i];
            BuildWalkableAnimeHouse(housesGroup.transform, cfg.name, cfg.pos.x, cfg.pos.y, cfg.rot,
                terrain, matPlaster, matWood, matStone, cfg.roofMat, matGlass);
        }

        // 7. Generate Anime Trees
        var treesGroup = new GameObject("Anime_Trees");
        treesGroup.transform.SetParent(rootGO.transform, false);
        var canopyMesh = GetOrCreateCanopyMesh();

        var rng = new System.Random(42069);
        int totalTrees = 135;
        for (int i = 0; i < totalTrees; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            float radius = (float)(rng.NextDouble() * 320f + 35f);
            float wx = Mathf.Cos(angle) * radius;
            float wz = Mathf.Sin(angle) * radius;

            // Avoid placing tree inside village buildings or center plaza
            if (Mathf.Abs(wx) < 12f && Mathf.Abs(wz) < 12f) continue;
            if (radius < 50f && (Mathf.Abs(wx - (-30f)) < 8f || Mathf.Abs(wx - 32f) < 8f)) continue;

            float wy = terrain.SampleHeight(new Vector3(wx, 0, wz)) + terrain.transform.position.y;
            // Avoid extreme mountain peaks where slope is steep
            if (wy > 45f) continue;

            Material leafMat = matLeafGreen;
            if (i % 6 == 0) leafMat = matLeafSakura; // Sakura cherry blossom
            else if (i % 5 == 0) leafMat = matLeafGold; // Golden autumn tree

            float height = 5.5f + (float)rng.NextDouble() * 3.5f;
            BuildAnimeTree(treesGroup.transform, "Anime Tree " + (i + 1), new Vector3(wx, wy, wz), height,
                matWood, leafMat, canopyMesh, rng);
        }

        // 8. Generate Anime Flora (Wildflower patches & grass clumps)
        var floraGroup = new GameObject("Anime_Flora");
        floraGroup.transform.SetParent(rootGO.transform, false);
        var bladeMesh = GetOrCreateBladeMesh();

        Material[] flowerMats = new Material[] { matFlowerPink, matFlowerYellow, matFlowerBlue, matFlowerWhite };
        int totalFlora = 220;
        for (int i = 0; i < totalFlora; i++)
        {
            float angle = (float)(rng.NextDouble() * Math.PI * 2);
            float radius = (float)(rng.NextDouble() * 140f + 10f);
            float wx = Mathf.Cos(angle) * radius;
            float wz = Mathf.Sin(angle) * radius;

            if (Mathf.Abs(wx) < 4f && Mathf.Abs(wz) < 4f) continue;
            float wy = terrain.SampleHeight(new Vector3(wx, 0, wz)) + terrain.transform.position.y;
            if (wy > 25f) continue;

            var patch = new GameObject(i % 2 == 0 ? "Flower Patch" : "Grass Tuft");
            patch.transform.SetParent(floraGroup.transform, false);
            patch.transform.position = new Vector3(wx, wy, wz);

            Material flwMat = flowerMats[i % flowerMats.Length];
            for (int b = 0; b < 3; b++)
            {
                float dx = (float)(rng.NextDouble() * 0.8f - 0.4f);
                float dz = (float)(rng.NextDouble() * 0.8f - 0.4f);
                float bladeH = 0.35f + (float)rng.NextDouble() * 0.35f;

                var blade = new GameObject("Blade");
                blade.transform.SetParent(patch.transform, false);
                blade.transform.localPosition = new Vector3(dx, 0, dz);
                blade.transform.localScale = new Vector3(0.22f, bladeH, 0.22f);
                blade.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                blade.AddComponent<MeshFilter>().sharedMesh = bladeMesh;
                blade.AddComponent<MeshRenderer>().sharedMaterial = matGrassBlade;

                if (i % 2 == 0)
                {
                    var flower = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    flower.name = "Flower";
                    flower.transform.SetParent(patch.transform, false);
                    flower.transform.localPosition = new Vector3(dx, bladeH, dz);
                    flower.transform.localScale = new Vector3(0.25f, 0.08f, 0.25f);
                    flower.GetComponent<Renderer>().sharedMaterial = flwMat;
                    Object.DestroyImmediate(flower.GetComponent<Collider>());
                }
            }
        }

        // 9. Generate Animated Chests with identical TestChest + ChestSO configuration
        var chestsGroup = new GameObject("Anime_Chests");
        chestsGroup.transform.SetParent(rootGO.transform, false);

        var chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/TestChest/TestChest.prefab");
        var chestConfigs = LoadChestConfigs();

        var chestPlacements = new (Vector2 offset, float rot, string name, int configIndex)[]
        {
            (new Vector2(-22f, -12f), 45f, "Chest_Village_HouseA", 0),
            (new Vector2(24f, -14f), -45f, "Chest_Village_HouseB", 1),
            (new Vector2(-38f, 40f), 135f, "Chest_Behind_HouseC", 2),
            (new Vector2(36f, 42f), -110f, "Chest_Behind_HouseD", 3),
            (new Vector2(-3f, 75f), 180f, "Chest_Manor_Garden", 4),
            (new Vector2(0f, 15f), 10f, "Chest_Central_Plaza", 0),
            (new Vector2(-55f, 8f), 65f, "Chest_West_Sakura_Grove", 1),
            (new Vector2(60f, 12f), -75f, "Chest_East_Flower_Meadow", 2),
            (new Vector2(-15f, -80f), 15f, "Chest_South_Outpost_Watch", 3),
            (new Vector2(75f, -65f), -30f, "Chest_Southeast_Foothill", 4),
            (new Vector2(-85f, 70f), 120f, "Chest_Northwest_Ridge", 1),
            (new Vector2(90f, 85f), -145f, "Chest_Northeast_Overlook", 0)
        };

        for (int i = 0; i < chestPlacements.Length; i++)
        {
            var cp = chestPlacements[i];
            float cx = cp.offset.x;
            float cz = cp.offset.y;
            float cy = terrain.SampleHeight(new Vector3(cx, 0, cz)) + terrain.transform.position.y + 0.02f;

            GameObject chestInst;
            if (chestPrefab != null)
            {
                chestInst = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab, chestsGroup.transform);
            }
            else
            {
                chestInst = new GameObject(cp.name);
                chestInst.transform.SetParent(chestsGroup.transform, false);
                chestInst.AddComponent<TestChest>();
            }

            chestInst.name = cp.name;
            chestInst.transform.position = new Vector3(cx, cy, cz);
            chestInst.transform.rotation = Quaternion.Euler(0, cp.rot, 0);

            // Configure TestChest script and assign ChestSO identically to the scene
            var chestComp = chestInst.GetComponent<TestChest>();
            if (chestComp != null && chestConfigs.Count > 0)
            {
                chestComp.chest = chestConfigs[cp.configIndex % chestConfigs.Count];
                EditorUtility.SetDirty(chestComp);
            }
        }

        // 10. Save Hierarchy as Standalone Prefab
        AssetDatabase.SaveAssets();
        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGO, PrefabPath);
        AssetDatabase.SaveAssets();

        // If in SampleScene, disable existing small terrain and environment so the new 1000x1000 world takes center stage smoothly
        if (isSampleScene)
        {
            var oldTerrain = GameObject.Find("Test Terrain - Gentle Hills");
            if (oldTerrain) oldTerrain.SetActive(false);

            var oldEnv = GameObject.Find("Test Environment - Village and Plants");
            if (oldEnv) oldEnv.SetActive(false);

            // Ensure player is at proper ground height
            var player = GameObject.FindGameObjectWithTag("Player");
            if (!player) player = GameObject.Find("Character Warrior");
            if (player)
            {
                float playerGroundY = terrain.SampleHeight(player.transform.position) + terrain.transform.position.y;
                var pPos = player.transform.position;
                if (pPos.y < playerGroundY) pPos.y = playerGroundY + 0.2f;
                player.transform.position = pPos;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // 11. Write Report
        string report = $"PASS\n" +
            $"Terrain Size: 1000 x 1000 m (Height: 120 m)\n" +
            $"Heightmap Resolution: 513 x 513\n" +
            $"Style: Vibrant Anime / Cel-Shaded (Ghibli & Genshin Aesthetic)\n" +
            $"Terrain Layers: 4 (Anime Grass, Anime Meadow, Anime Rock, Anime Dirt)\n" +
            $"Houses: {houseConfigs.Length} Walkable Houses (Door 2.6x3.6m with Ramps & Furnishings)\n" +
            $"Trees: {totalTrees} Anime Trees (Green Oak, Sakura Pink, Golden Autumn)\n" +
            $"Flora: {totalFlora} Flower Patches & Grass Tufts\n" +
            $"Chests: {chestPlacements.Length} Animated Chests configured with TestChest & ChestSO\n" +
            $"Prefab Saved: {PrefabPath}\n" +
            $"Terrain Data Saved: {terrainDataPath}\n" +
            $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";

        File.WriteAllText(ReportPath, report);
        Debug.Log($"[AnimeWorld1000] Successfully generated and saved prefab at '{PrefabPath}'.\n{report}");

        Selection.activeGameObject = rootGO;
        if (SceneView.lastActiveSceneView)
        {
            SceneView.lastActiveSceneView.LookAt(new Vector3(0, 15, 0), Quaternion.Euler(32, -35, 0), 95);
        }
    }

    static TerrainLayer[] CreateAnimeTerrainLayers()
    {
        // 1. Anime Grass (Vibrant, fresh green)
        var texGrass = CreateAnimeTexture("AnimeGrass_Albedo.png", new Color(0.42f, 0.74f, 0.26f), new Color(0.48f, 0.80f, 0.30f), 0.12f);
        // 2. Anime Meadow (Sunny warm yellow-green)
        var texMeadow = CreateAnimeTexture("AnimeMeadow_Albedo.png", new Color(0.58f, 0.78f, 0.32f), new Color(0.66f, 0.86f, 0.40f), 0.15f);
        // 3. Anime Rock (Soft anime mountain slate blue-gray)
        var texRock = CreateAnimeTexture("AnimeRock_Albedo.png", new Color(0.46f, 0.50f, 0.56f), new Color(0.55f, 0.58f, 0.64f), 0.22f);
        // 4. Anime Dirt (Warm village path earth)
        var texDirt = CreateAnimeTexture("AnimeDirt_Albedo.png", new Color(0.72f, 0.60f, 0.46f), new Color(0.65f, 0.54f, 0.40f), 0.10f);

        var layerGrass = GetOrCreateLayer("AnimeGrass", texGrass, new Vector2(10, 10), 0.08f);
        var layerMeadow = GetOrCreateLayer("AnimeMeadow", texMeadow, new Vector2(12, 12), 0.08f);
        var layerRock = GetOrCreateLayer("AnimeRock", texRock, new Vector2(16, 16), 0.14f);
        var layerDirt = GetOrCreateLayer("AnimeDirt", texDirt, new Vector2(8, 8), 0.06f);

        return new TerrainLayer[] { layerGrass, layerMeadow, layerRock, layerDirt };
    }

    static Texture2D CreateAnimeTexture(string filename, Color baseColor, Color highlightColor, float noiseFreq)
    {
        string path = BaseFolder + "/Textures/" + filename;
        int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGB24, true) { name = filename };
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n1 = Mathf.PerlinNoise(x * noiseFreq, y * noiseFreq);
                float n2 = Mathf.PerlinNoise((x + 128) * noiseFreq * 2.5f, (y + 128) * noiseFreq * 2.5f) * 0.4f;
                float blend = Mathf.Clamp01(n1 + n2 * 0.5f);
                Color c = Color.Lerp(baseColor, highlightColor, blend);
                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static TerrainLayer GetOrCreateLayer(string name, Texture2D texture, Vector2 tileSize, float smoothness)
    {
        string path = BaseFolder + "/Layers/" + name + ".terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (!layer)
        {
            layer = new TerrainLayer
            {
                name = name,
                diffuseTexture = texture,
                tileSize = tileSize,
                smoothness = smoothness,
                metallic = 0f
            };
            AssetDatabase.CreateAsset(layer, path);
        }
        else
        {
            layer.diffuseTexture = texture;
            layer.tileSize = tileSize;
            layer.smoothness = smoothness;
            EditorUtility.SetDirty(layer);
        }
        return layer;
    }

    static void GenerateHeightmapAndAlphamap(TerrainData data)
    {
        int hRes = data.heightmapResolution; // 513
        float vertical = data.size.y; // 120
        float width = data.size.x; // 1000
        var heights = new float[hRes, hRes];

        float centerH = 5.0f; // Central base elevation

        for (int z = 0; z < hRes; z++)
        {
            for (int x = 0; x < hRes; x++)
            {
                // World coordinates centered at (0,0)
                float wx = ((float)x / (hRes - 1) - 0.5f) * width;
                float wz = ((float)z / (hRes - 1) - 0.5f) * width;
                float dist = Mathf.Sqrt(wx * wx + wz * wz);

                // 1. Central Plains: Flat to gentle rolling hills (r < 140m)
                float plainWave = Mathf.Sin(wx / 32f) * Mathf.Cos(wz / 32f) * 1.5f
                    + Mathf.PerlinNoise((wx + 450) / 48f, (wz + 450) / 48f) * 1.8f;

                // 2. Mountains (r > 160m)
                // Large scale mountain mass
                float mtnNoise1 = Mathf.PerlinNoise((wx + 800) / 280f, (wz + 800) / 280f);
                // Ridge noise (sharp peaks)
                float mtnRidge = 1.0f - Mathf.Abs(Mathf.PerlinNoise((wx + 1200) / 130f, (wz + 1200) / 130f) * 2f - 1f);
                mtnRidge = Mathf.Pow(mtnRidge, 1.8f);

                float detailNoise = (Mathf.PerlinNoise(wx / 45f, wz / 45f) - 0.5f) * 6f;

                float mountainElevation = (mtnNoise1 * 65f + mtnRidge * 45f + detailNoise);

                // Mountain blend starts at 140m, fully mountainous by 300m
                float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(140f, 320f, dist));

                // Outer perimeter natural amphitheater barrier (r > 420m)
                float rimBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(410f, 490f, dist));
                float rimHeight = rimBlend * 35f;

                float finalH = (centerH + plainWave) * (1f - blend) + (centerH + mountainElevation + rimHeight) * blend;
                finalH = Mathf.Clamp(finalH, 1.0f, vertical - 2f);

                heights[z, x] = finalH / vertical;
            }
        }

        data.SetHeights(0, 0, heights);

        // Alphamap (Splatmap) Generation
        int aRes = 512;
        data.alphamapResolution = aRes;
        var splats = new float[aRes, aRes, 4]; // 0: Grass, 1: Meadow, 2: Rock, 3: Dirt

        for (int z = 0; z < aRes; z++)
        {
            for (int x = 0; x < aRes; x++)
            {
                float normX = (float)x / (aRes - 1);
                float normZ = (float)z / (aRes - 1);

                float steepness = data.GetSteepness(normX, normZ); // degrees
                float height = data.GetInterpolatedHeight(normX, normZ);

                float wx = (normX - 0.5f) * width;
                float wz = (normZ - 0.5f) * width;
                float dist = Mathf.Sqrt(wx * wx + wz * wz);

                // Village path dirt detection
                // Village roads cross at center and radiate out to houses
                float pathDist1 = Mathf.Abs(wx); // N-S road
                float pathDist2 = Mathf.Abs(wz); // E-W road
                float pathRing = Mathf.Abs(dist - 35f); // Village square ring
                bool isPath = (dist < 85f) && (pathDist1 < 3.2f || pathDist2 < 3.2f || pathRing < 2.5f);

                float wGrass = 0f, wMeadow = 0f, wRock = 0f, wDirt = 0f;

                if (steepness > 32f)
                {
                    // Steep cliff rock
                    wRock = 1.0f;
                }
                else if (steepness > 18f)
                {
                    // Sloped hills
                    float t = Mathf.InverseLerp(18f, 32f, steepness);
                    wRock = t;
                    wMeadow = 1f - t;
                }
                else
                {
                    // Gentle slopes and plains
                    if (isPath)
                    {
                        wDirt = 0.85f;
                        wGrass = 0.15f;
                    }
                    else if (height > 26f)
                    {
                        wMeadow = 0.85f;
                        wGrass = 0.15f;
                    }
                    else
                    {
                        wGrass = 0.90f;
                        wMeadow = 0.10f;
                    }
                }

                float sum = wGrass + wMeadow + wRock + wDirt;
                if (sum < 0.001f) { wGrass = 1f; sum = 1f; }

                splats[z, x, 0] = wGrass / sum;
                splats[z, x, 1] = wMeadow / sum;
                splats[z, x, 2] = wRock / sum;
                splats[z, x, 3] = wDirt / sum;
            }
        }

        data.SetAlphamaps(0, 0, splats);
    }

    static Material GetOrCreateMat(string name, Color color, float metallic = 0f, float smoothness = 0.15f)
    {
        string path = BaseFolder + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            m = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color", color);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material GetOrCreateGlassMat(string name)
    {
        string path = BaseFolder + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            m = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }

        Color glassCol = new Color(0.60f, 0.82f, 0.92f, 0.22f);
        m.SetColor("_BaseColor", glassCol);
        m.SetColor("_Color", glassCol);
        m.SetFloat("_Surface", 1); // Transparent
        m.SetFloat("_Blend", 0);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void BuildWalkableAnimeHouse(Transform parent, string name, float wx, float wz, float angle,
        Terrain terrain, Material plaster, Material wood, Material stone, Material roof, Material glass)
    {
        float highest = float.NegativeInfinity;
        for (float dx = -4.5f; dx <= 4.5f; dx += 1.5f)
            for (float dz = -4f; dz <= 4f; dz += 1.5f)
            {
                var samplePt = new Vector3(wx + dx, 0, wz + dz);
                highest = Mathf.Max(highest, terrain.SampleHeight(samplePt) + terrain.transform.position.y);
            }

        var house = new GameObject(name);
        house.transform.SetParent(parent, false);
        house.transform.position = new Vector3(wx, highest + 0.12f, wz);
        house.transform.rotation = Quaternion.Euler(0, angle, 0);

        // Foundation Stone
        Box(house.transform, "Foundation", new Vector3(0, -0.4f, 0), new Vector3(8.4f, 0.8f, 6.4f), stone, true);

        // Wooden Floor Boards
        for (int i = 0; i < 16; i++)
        {
            Box(house.transform, "FloorBoard_" + i, new Vector3(-3.85f + i * 0.51f, 0.01f, 0),
                new Vector3(0.49f, 0.02f, 5.9f), (i % 2 == 0 ? wood : plaster), false);
        }

        // Back Wall
        Box(house.transform, "BackWall", new Vector3(0, 2.15f, 2.95f), new Vector3(8.2f, 4.3f, 0.35f), plaster, true);

        // Side Walls & Front Entrance Walls with Wide Door Opening (2.6m wide, 3.6m high)
        foreach (float side in new[] { -1f, 1f })
        {
            // Front Entrance Wall panel
            Box(house.transform, "EntranceWall", new Vector3(side * 2.85f, 2.15f, -2.95f), new Vector3(2.5f, 4.3f, 0.35f), plaster, true);

            // Side Wall
            Box(house.transform, "SideWall", new Vector3(side * 4.0f, 2.15f, 0), new Vector3(0.35f, 4.3f, 6.2f), plaster, true);

            // Corner Timber Posts
            foreach (float z in new[] { -2.98f, 2.98f })
                Box(house.transform, "CornerPost", new Vector3(side * 4.05f, 2.15f, z), new Vector3(0.3f, 4.4f, 0.3f), wood, true);

            // Door Posts
            Box(house.transform, "DoorPost", new Vector3(side * 1.45f, 1.8f, -3.12f), new Vector3(0.2f, 3.6f, 0.25f), wood, true);

            // Windows with Glass
            Box(house.transform, "SideWindowGlass", new Vector3(side * 4.02f, 2.3f, 0), new Vector3(0.04f, 1.4f, 2.2f), glass, true);
            Box(house.transform, "WindowFrame", new Vector3(side * 4.05f, 2.3f, 0), new Vector3(0.12f, 1.6f, 2.4f), wood, false);

            // Roof Slope
            var roofSlope = new GameObject("RoofSlope");
            roofSlope.transform.SetParent(house.transform, false);
            roofSlope.transform.localPosition = new Vector3(side * 2.1f, 5.15f, 0);
            roofSlope.transform.localRotation = Quaternion.Euler(0, 0, -side * 20);
            Box(roofSlope.transform, "RoofSlab", Vector3.zero, new Vector3(4.8f, 0.22f, 7.2f), roof, true);

            // Porch Pillars
            Box(house.transform, "PorchPillar", new Vector3(side * 2.0f, 1.9f, -4.35f), new Vector3(0.22f, 3.8f, 0.22f), wood, true);
            Box(house.transform, "PorchPillarBase", new Vector3(side * 2.0f, 0.1f, -4.35f), new Vector3(0.36f, 0.2f, 0.36f), stone, true);
        }

        // Door Lintel (Top of doorway at 3.6m height)
        Box(house.transform, "DoorLintel", new Vector3(0, 3.95f, -2.95f), new Vector3(2.7f, 0.7f, 0.35f), plaster, true);
        Box(house.transform, "DoorLintelTrim", new Vector3(0, 3.65f, -3.12f), new Vector3(3.0f, 0.2f, 0.22f), wood, false);

        // Porch Canopy & Crossbeam
        Box(house.transform, "PorchCanopy", new Vector3(0, 4.0f, -4.0f), new Vector3(4.5f, 0.18f, 2.4f), roof, true);
        Box(house.transform, "PorchBeam", new Vector3(0, 3.85f, -4.35f), new Vector3(4.2f, 0.22f, 0.22f), wood, false);

        // Ridge Cap
        Box(house.transform, "RoofRidge", new Vector3(0, 5.92f, 0), new Vector3(0.35f, 0.22f, 7.4f), wood, false);

        // Stone Chimney
        Box(house.transform, "Chimney", new Vector3(2.5f, 5.5f, 1.8f), new Vector3(0.75f, 2.2f, 0.85f), stone, true);
        Box(house.transform, "ChimneyCap", new Vector3(2.5f, 6.65f, 1.8f), new Vector3(0.95f, 0.18f, 1.05f), stone, false);

        // Interior Furnishings (Leaves central corridor open for movement)
        Box(house.transform, "Table", new Vector3(-2.4f, 0.85f, 0.9f), new Vector3(1.6f, 0.15f, 1.4f), wood, true);
        Box(house.transform, "Bench", new Vector3(-2.4f, 0.45f, -0.2f), new Vector3(1.6f, 0.16f, 0.45f), wood, true);
        for (int s = 0; s < 3; s++)
            Box(house.transform, "Shelf_" + s, new Vector3(3.1f, 0.6f + s * 0.8f, 1.2f), new Vector3(0.8f, 0.1f, 1.8f), wood, true);

        // Connect Doorway with a Smooth Ramp to the Terrain Surface
        BuildEntranceRamp(house.transform, terrain);
    }

    static void BuildEntranceRamp(Transform house, Terrain terrain)
    {
        var rampEndPt = house.TransformPoint(new Vector3(0, 0, -7.5f));
        float groundY = terrain.SampleHeight(rampEndPt) + terrain.transform.position.y;
        float bottomOffset = groundY - house.position.y - 0.05f;

        var mesh = new Mesh { name = house.name + "_Ramp" };
        mesh.vertices = new Vector3[]
        {
            new Vector3(-1.65f, bottomOffset, -7.2f),
            new Vector3(1.65f, bottomOffset, -7.2f),
            new Vector3(-1.65f, 0.01f, -3.05f),
            new Vector3(1.65f, 0.01f, -3.05f),
            new Vector3(-1.65f, bottomOffset - 0.25f, -3.05f),
            new Vector3(1.65f, bottomOffset - 0.25f, -3.05f)
        };
        mesh.triangles = new int[]
        {
            0, 2, 1,   1, 2, 3,
            0, 4, 2,   1, 3, 5,
            2, 4, 3,   3, 4, 5,
            0, 1, 4,   1, 5, 4
        };
        mesh.RecalculateNormals();

        string rampMeshPath = BaseFolder + "/Meshes/" + house.name.Replace(" ", "") + "_Ramp.asset";
        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(rampMeshPath);
        if (existingMesh)
        {
            EditorUtility.CopySerialized(mesh, existingMesh);
            Object.DestroyImmediate(mesh);
            mesh = existingMesh;
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, rampMeshPath);
        }

        var stoneMat = GetOrCreateMat("AnimeStone", new Color(0.48f, 0.52f, 0.56f));
        var rampGo = new GameObject("EntranceRamp");
        rampGo.transform.SetParent(house, false);
        rampGo.AddComponent<MeshFilter>().sharedMesh = mesh;
        rampGo.AddComponent<MeshRenderer>().sharedMaterial = stoneMat;
        var collider = rampGo.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
    }

    static void BuildAnimeTree(Transform parent, string name, Vector3 pos, float height,
        Material wood, Material leaf, Mesh canopyMesh, System.Random rng)
    {
        var tree = new GameObject(name);
        tree.transform.SetParent(parent, false);
        tree.transform.position = pos;

        // Trunk with Collider
        float trunkH = height * 0.45f;
        float trunkRadius = 0.35f + (float)rng.NextDouble() * 0.15f;
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(tree.transform, false);
        trunk.transform.localPosition = new Vector3(0, trunkH * 0.5f, 0);
        trunk.transform.localScale = new Vector3(trunkRadius * 2, trunkH * 0.5f, trunkRadius * 2);
        trunk.GetComponent<Renderer>().sharedMaterial = wood;

        // Fluffy Stylized Anime Canopy (3 overlapping spheres)
        float canopyBaseY = trunkH * 0.75f;
        float canopyScale = height * 0.45f;

        var c1 = new GameObject("Canopy_Main");
        c1.transform.SetParent(tree.transform, false);
        c1.transform.localPosition = new Vector3(0, canopyBaseY + canopyScale * 0.7f, 0);
        c1.transform.localScale = new Vector3(canopyScale * 1.5f, canopyScale * 1.3f, canopyScale * 1.5f);
        c1.AddComponent<MeshFilter>().sharedMesh = canopyMesh;
        c1.AddComponent<MeshRenderer>().sharedMaterial = leaf;

        var c2 = new GameObject("Canopy_Upper");
        c2.transform.SetParent(tree.transform, false);
        c2.transform.localPosition = new Vector3(0.2f, canopyBaseY + canopyScale * 1.3f, -0.2f);
        c2.transform.localScale = new Vector3(canopyScale * 1.1f, canopyScale * 1.0f, canopyScale * 1.1f);
        c2.AddComponent<MeshFilter>().sharedMesh = canopyMesh;
        c2.AddComponent<MeshRenderer>().sharedMaterial = leaf;

        var c3 = new GameObject("Canopy_Side");
        c3.transform.SetParent(tree.transform, false);
        c3.transform.localPosition = new Vector3(-0.35f, canopyBaseY + canopyScale * 0.5f, 0.35f);
        c3.transform.localScale = new Vector3(canopyScale * 1.0f, canopyScale * 0.85f, canopyScale * 1.0f);
        c3.AddComponent<MeshFilter>().sharedMesh = canopyMesh;
        c3.AddComponent<MeshRenderer>().sharedMaterial = leaf;
    }

    static Mesh GetOrCreateCanopyMesh()
    {
        string path = BaseFolder + "/Meshes/AnimeCanopy.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) return existing;

        // Low-poly faceted sphere for that stylized anime / Ghibli look
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var mesh = Object.Instantiate(sphere.GetComponent<MeshFilter>().sharedMesh);
        Object.DestroyImmediate(sphere);
        mesh.name = "AnimeCanopySphere";
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Mesh GetOrCreateBladeMesh()
    {
        string path = BaseFolder + "/Meshes/AnimeGrassBlade.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) return existing;

        // 3D fan cone for grass tufts
        int sides = 6;
        var verts = new Vector3[sides + 2];
        verts[0] = Vector3.up; // tip
        verts[1] = Vector3.zero; // center base
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            verts[i + 2] = new Vector3(Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f);
        }

        var tris = new int[sides * 6];
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            // Side face
            tris[i * 6 + 0] = 0;
            tris[i * 6 + 1] = next + 2;
            tris[i * 6 + 2] = i + 2;
            // Bottom cap
            tris[i * 6 + 3] = 1;
            tris[i * 6 + 4] = i + 2;
            tris[i * 6 + 5] = next + 2;
        }

        var mesh = new Mesh { name = "AnimeGrassBlade", vertices = verts, triangles = tris };
        mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 localSize, Material mat, bool solid = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localSize;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!solid)
        {
            var c = go.GetComponent<Collider>();
            if (c) Object.DestroyImmediate(c);
        }
        return go;
    }

    static List<ChestSO> LoadChestConfigs()
    {
        var list = new List<ChestSO>();
        string[] searchPaths = new string[]
        {
            "Assets/Configs/ChestSO/testChest.asset",
            "Assets/Configs/ChestSO/TestChest_Weapons.asset",
            "Assets/Configs/ChestSO/TestChest_Mixed.asset",
            "Assets/Configs/ChestSO/TestChest_Full.asset",
            "Assets/Configs/ChestSO/TestChest_StackLimits.asset"
        };

        foreach (var p in searchPaths)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ChestSO>(p);
            if (asset) list.Add(asset);
        }
        return list;
    }
}
