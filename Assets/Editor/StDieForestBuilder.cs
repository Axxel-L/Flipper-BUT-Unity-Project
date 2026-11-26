#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class StDieForestBuilder
{
    private const string RootName = "Foret St-Die";
    private const string GeneratedFolder = "Assets/Generated";
    private const string ConeAssetPath = GeneratedFolder + "/FirCone.asset";
    private const string TrunkMaterialPath = GeneratedFolder + "/TreeTrunk.mat";
    private const string WoodTexturePath = GeneratedFolder + "/WoodRailTexture.asset";
    private const string WoodMaterialPath = GeneratedFolder + "/WoodRail.mat";
    private const string BrassMaterialPath = GeneratedFolder + "/BrassRail.mat";
    private const string MetalMaterialPath = GeneratedFolder + "/StarterMetal.mat";
    private const string TrailTexturePath = GeneratedFolder + "/TrailSoft.asset";
    private const string TrailMaterialPath = GeneratedFolder + "/BallGlowTrail.mat";

    private const float TreeBaseY = 0.60f;

    private static readonly Vector2[] PlayfieldTreePositions =
    {
        new Vector2(8.4f, -5.8f),
        new Vector2(11.2f, -5.6f),
        new Vector2(12.4f, -3.2f),
        new Vector2(9.2f, -3.0f),
        new Vector2(11.6f, -0.8f),
        new Vector2(8.2f, -0.6f),
        new Vector2(7.6f, 2.2f),
        new Vector2(9.0f, 4.4f),
        new Vector2(7.2f, 6.6f),
        new Vector2(8.6f, 8.8f),
        new Vector2(6.2f, 10.4f),

        // Bosquet sous l'IUT
        new Vector2(-9.4f, -7.7f),
        new Vector2(-6.8f, -8.0f),
        new Vector2(-4.2f, -7.8f),
        new Vector2(-1.6f, -8.3f),
        new Vector2(-8.0f, -9.4f),
        new Vector2(-4.8f, -9.5f),
        new Vector2(-2.2f, -9.2f)
    };

    [MenuItem("Flipper/Construire la forêt St-Dié")]
    public static void BuildForest()
    {
        EnsureFolder();

        AssetDatabase.DeleteAsset(GeneratedFolder + "/ForestFloor.mat");
        AssetDatabase.DeleteAsset(GeneratedFolder + "/ForestFloorTexture.asset");

        Mesh cone = EnsureConeMesh();
        Material trunkMaterial = EnsureLitMaterial(TrunkMaterialPath, "TreeTrunk", new Color(0.20f, 0.12f, 0.06f), 0.15f);
        Material[] foliageMaterials =
        {
            EnsureLitMaterial(GeneratedFolder + "/SapinBleuSombre.mat", "SapinBleuSombre", new Color(0.055f, 0.19f, 0.14f), 0.08f),
            EnsureLitMaterial(GeneratedFolder + "/SapinVert.mat", "SapinVert", new Color(0.10f, 0.30f, 0.13f), 0.08f),
            EnsureLitMaterial(GeneratedFolder + "/SapinClair.mat", "SapinClair", new Color(0.17f, 0.40f, 0.17f), 0.08f),
            EnsureLitMaterial(GeneratedFolder + "/SapinMousse.mat", "SapinMousse", new Color(0.08f, 0.26f, 0.22f), 0.08f)
        };

        GameObject previous = GameObject.Find(RootName);
        if (previous != null)
        {
            Object.DestroyImmediate(previous);
        }

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Construire la forêt St-Dié");

        for (int i = 0; i < PlayfieldTreePositions.Length; i++)
        {
            Vector2 position = PlayfieldTreePositions[i];
            float scale = 0.72f + (i % 3) * 0.06f;
            int palette = (i * 2) % foliageMaterials.Length;
            BuildTree(root.transform, cone, trunkMaterial, foliageMaterials, i, position, scale, palette);
        }

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Forêt St-Dié] {PlayfieldTreePositions.Length} sapins placés sur le plateau.");
    }

    [MenuItem("Flipper/Améliorer le plateau (murs, traînée, IUT)")]
    public static void BuildVisualUpgrade()
    {
        EnsureFolder();
        ApplyWalls();
        ApplyTrail();
        FixGlassClearance();
        FixIutColliders();
        EnsureGameManager();

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[St-Dié] murs, traînée et collisions de l'IUT mis à jour.");
    }

    private static void BuildTree(
        Transform parent,
        Mesh cone,
        Material trunkMaterial,
        Material[] foliageMaterials,
        int index,
        Vector2 position,
        float scale,
        int palette)
    {
        GameObject tree = new GameObject($"Sapin {index:00}");
        tree.transform.SetParent(parent, false);
        tree.transform.position = new Vector3(position.x, TreeBaseY, position.y);
        tree.transform.rotation = Quaternion.Euler(0f, index * 47f, 0f);
        tree.transform.localScale = Vector3.one * scale;

        CapsuleCollider collider = tree.AddComponent<CapsuleCollider>();
        collider.direction = 1;
        collider.center = new Vector3(0f, 0.75f, 0f);
        collider.height = 1.5f;
        collider.radius = 0.28f;

        tree.AddComponent<ForestTree>();

        GameObject visual = new GameObject("Visuel");
        visual.transform.SetParent(tree.transform, false);

        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Tronc";
        trunk.transform.SetParent(visual.transform, false);
        trunk.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        trunk.transform.localScale = new Vector3(0.10f, 0.22f, 0.10f);
        Collider trunkCollider = trunk.GetComponent<Collider>();
        if (trunkCollider != null)
        {
            Object.DestroyImmediate(trunkCollider);
        }
        trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMaterial;

        float[] baseHeights = { 0.30f, 0.95f, 1.47f };
        float[] radii = { 1.0f, 0.78f, 0.56f };
        float[] heights = { 0.98f, 0.82f, 0.64f };

        for (int layer = 0; layer < 3; layer++)
        {
            GameObject foliage = new GameObject($"Feuillage {layer + 1}");
            foliage.transform.SetParent(visual.transform, false);
            foliage.transform.localPosition = new Vector3(0f, baseHeights[layer], 0f);
            foliage.transform.localScale = new Vector3(radii[layer], heights[layer], radii[layer]);

            MeshFilter filter = foliage.AddComponent<MeshFilter>();
            filter.sharedMesh = cone;

            MeshRenderer renderer = foliage.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = foliageMaterials[(palette + layer) % foliageMaterials.Length];
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private static void ApplyWalls()
    {
        Material wood = EnsureWoodMaterial();
        Material brass = EnsureBrassMaterial();

        string[] woodTargets =
        {
            "Left", "Left (1)", "Right", "Right (1)", "Back", "Front",
            "Sortie Gauche", "Sortie Droite",
            "Sortie Gauche (1)", "Sortie Droite (1)",
            "corner", "corner (1)", "Sortie"
        };

        foreach (string name in woodTargets)
        {
            GameObject target = GameObject.Find(name);
            if (target != null)
            {
                SetMaterial(target, wood);
            }
        }

        GameObject previousCaps = GameObject.Find("Rails Brass");
        if (previousCaps != null)
        {
            Object.DestroyImmediate(previousCaps);
        }

        GameObject starter = GameObject.Find("Starter");
        if (starter != null)
        {
            SetMaterial(starter, EnsureMetalMaterial());
        }

        GameObject capRoot = new GameObject("Rails Brass");
        string[] capTargets = { "Left", "Left (1)", "Right", "Right (1)", "Back", "Front", "Sortie Gauche", "Sortie Droite" };

        foreach (string name in capTargets)
        {
            GameObject target = GameObject.Find(name);
            if (target == null)
            {
                continue;
            }

            Renderer renderer = target.GetComponentInChildren<Renderer>();
            if (renderer == null)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cap.name = "BrassCap_" + name;
            cap.transform.SetParent(capRoot.transform, false);
            cap.transform.position = new Vector3(bounds.center.x, bounds.max.y + 0.045f, bounds.center.z);
            cap.transform.rotation = Quaternion.identity;
            cap.transform.localScale = new Vector3(bounds.size.x + 0.10f, 0.09f, bounds.size.z + 0.10f);

            Collider capCollider = cap.GetComponent<Collider>();
            if (capCollider != null)
            {
                Object.DestroyImmediate(capCollider);
            }

            MeshRenderer capRenderer = cap.GetComponent<MeshRenderer>();
            capRenderer.sharedMaterial = brass;
            capRenderer.shadowCastingMode = ShadowCastingMode.On;
        }
    }

    private static void ApplyTrail()
    {
        GameObject bille = GameObject.Find("Bille");
        if (bille == null)
        {
            return;
        }

        TrailRenderer trail = bille.GetComponent<TrailRenderer>();
        if (trail == null)
        {
            trail = bille.AddComponent<TrailRenderer>();
        }

        trail.sharedMaterial = EnsureTrailMaterial();
        trail.Clear();
        trail.time = 0.35f;
        trail.minVertexDistance = 0.03f;
        trail.widthMultiplier = 0.95f;
        trail.numCapVertices = 2;
        trail.numCornerVertices = 3;
        trail.textureMode = LineTextureMode.Stretch;
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.generateLightingData = false;
        trail.emitting = true;
        trail.autodestruct = false;

        AnimationCurve widthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.6f, 0.9f),
            new Keyframe(1f, 0f));
        trail.widthCurve = widthCurve;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.92f, 0.55f), 0f),
                new GradientColorKey(new Color(1f, 0.72f, 0.22f), 0.3f),
                new GradientColorKey(new Color(1f, 0.50f, 0.06f), 0.7f),
                new GradientColorKey(new Color(0.8f, 0.22f, 0f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.95f, 0.3f),
                new GradientAlphaKey(0.7f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = gradient;

        BallTrailController controller = bille.GetComponent<BallTrailController>();
        if (controller == null)
        {
            controller = bille.AddComponent<BallTrailController>();
        }

        controller.trailLength = 7f;
        controller.minimumTime = 0.06f;
        controller.maximumTime = 0.35f;
        controller.minimumVertexDistance = 0.03f;

        EditorUtility.SetDirty(trail);
        EditorUtility.SetDirty(controller);
    }

    private static void EnsureGameManager()
    {
        PinballGameManager manager = Object.FindAnyObjectByType<PinballGameManager>();
        if (manager == null)
        {
            GameObject host = new GameObject("Flipper Game");
            manager = host.AddComponent<PinballGameManager>();
            Undo.RegisterCreatedObjectUndo(host, "Créer le gestionnaire de partie");
        }

        if (manager.GetComponent<ScoreboardUI>() == null)
        {
            manager.gameObject.AddComponent<ScoreboardUI>();
        }

        if (manager.GetComponent<SnowStormEvent>() == null)
        {
            manager.gameObject.AddComponent<SnowStormEvent>();
        }
    }

    private static void FixGlassClearance()
    {
        // La vitre forme un plafond physique : avec une bille de 1,6 de diamètre,
        // elle était trop basse et coinçait la bille contre les bumpers.
        GameObject glass = GameObject.Find("vitre-flipper");
        if (glass == null)
        {
            return;
        }

        Vector3 position = glass.transform.position;
        const float minimumY = 3.05f;
        if (position.y < minimumY)
        {
            position.y = minimumY;
            glass.transform.position = position;
            EditorUtility.SetDirty(glass);
        }
    }

    private static void FixIutColliders()
    {
        GameObject iut = GameObject.Find("IUT");
        if (iut == null)
        {
            return;
        }

        Transform meshTarget = iut.transform;

        foreach (MeshFilter filter in iut.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
            {
                continue;
            }

            MeshCollider meshCollider = filter.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = filter.gameObject.AddComponent<MeshCollider>();
            }

            meshCollider.sharedMesh = filter.sharedMesh;
            meshCollider.convex = false;
            meshCollider.isTrigger = false;
            meshTarget = filter.transform;
        }

        IUTBumper rootBumper = iut.GetComponent<IUTBumper>();
        if (rootBumper != null && meshTarget != iut.transform)
        {
            // Le script de propulsion doit vivre sur l'objet qui porte le MeshCollider,
            // sinon il ne reçoit plus les collisions une fois la boîte englobante retirée.
            UnityEditorInternal.ComponentUtility.CopyComponent(rootBumper);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(meshTarget.gameObject);
            Object.DestroyImmediate(rootBumper);
        }

        foreach (Collider collider in iut.GetComponentsInChildren<Collider>(true))
        {
            if (!(collider is MeshCollider))
            {
                Object.DestroyImmediate(collider);
            }
        }
    }

    private static void SetMaterial(GameObject root, Material material)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.gameObject.name.StartsWith("BrassCap"))
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
        }
    }

    private static Material EnsureWoodMaterial()
    {
        Texture2D woodTexture = EnsureWoodTexture();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(WoodMaterialPath);

        if (material == null)
        {
            material = new Material(FindLitShader());
            material.name = "WoodRail";
            AssetDatabase.CreateAsset(material, WoodMaterialPath);
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", woodTexture);
            material.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
        }
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.35f);
        }
        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material EnsureBrassMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(BrassMaterialPath);

        if (material == null)
        {
            material = new Material(FindLitShader());
            material.name = "BrassRail";
            AssetDatabase.CreateAsset(material, BrassMaterialPath);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", new Color(0.82f, 0.60f, 0.24f, 1f));
        }
        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0.9f);
        }
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.75f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material EnsureMetalMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MetalMaterialPath);

        if (material == null)
        {
            material = new Material(FindLitShader());
            material.name = "StarterMetal";
            AssetDatabase.CreateAsset(material, MetalMaterialPath);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", new Color(0.34f, 0.36f, 0.40f, 1f));
        }
        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0.85f);
        }
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.55f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D EnsureWoodTexture()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(WoodTexturePath);
        if (existing != null)
        {
            return existing;
        }

        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
        texture.name = "WoodRailTexture";
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.anisoLevel = 4;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float grain = Mathf.PerlinNoise(x * 0.045f + 3.1f, y * 0.012f + 8.4f);
                float fine = Mathf.PerlinNoise(x * 0.18f + 21.7f, y * 0.18f + 12.3f);
                float value = Mathf.Clamp01(grain * 0.75f + fine * 0.25f);

                Color dark = new Color(0.16f, 0.085f, 0.04f, 1f);
                Color light = new Color(0.42f, 0.24f, 0.11f, 1f);
                texture.SetPixel(x, y, Color.Lerp(dark, light, Mathf.SmoothStep(0f, 1f, value)));
            }
        }

        texture.Apply();
        AssetDatabase.CreateAsset(texture, WoodTexturePath);
        AssetDatabase.SaveAssets();
        return texture;
    }

    private static Material EnsureTrailMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath);

        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = FindLitShader();
            }

            material = new Material(shader);
            material.name = "BallGlowTrail";
            AssetDatabase.CreateAsset(material, TrailMaterialPath);
        }

        Texture2D texture = EnsureTrailTexture();
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }
        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", 0f);
        }
        if (material.HasProperty("_SrcBlend"))
        {
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }
        if (material.HasProperty("_DstBlend"))
        {
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }
        if (material.HasProperty("_ZWrite"))
        {
            material.SetFloat("_ZWrite", 0f);
        }
        if (material.HasProperty("_Cull"))
        {
            material.SetFloat("_Cull", (float)CullMode.Off);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D EnsureTrailTexture()
    {
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TrailTexturePath);
        if (existing != null)
        {
            return existing;
        }

        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "TrailSoft";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            float v = y / (size - 1f);
            float distance = Mathf.Abs(v - 0.5f) * 2f;
            float alpha = Mathf.Clamp01(1f - distance);
            alpha = alpha * alpha * (3f - 2f * alpha);

            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        AssetDatabase.CreateAsset(texture, TrailTexturePath);
        AssetDatabase.SaveAssets();
        return texture;
    }

    private static Mesh EnsureConeMesh()
    {
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(ConeAssetPath);
        if (existing != null)
        {
            return existing;
        }

        Mesh mesh = BuildConeMesh(12, 1f, 1f);
        mesh.name = "FirCone";
        AssetDatabase.CreateAsset(mesh, ConeAssetPath);
        AssetDatabase.SaveAssets();
        return mesh;
    }

    private static Mesh BuildConeMesh(int sides, float radius, float height)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();

        for (int i = 0; i < sides; i++)
        {
            float angle = i / (float)sides * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            uv.Add(new Vector2(i / (float)sides, 0f));
        }

        int apex = vertices.Count;
        vertices.Add(new Vector3(0f, height, 0f));
        uv.Add(new Vector2(0.5f, 1f));

        for (int i = 0; i < sides; i++)
        {
            triangles.Add(i);
            triangles.Add(apex);
            triangles.Add((i + 1) % sides);
        }

        int capStart = vertices.Count;
        for (int i = 0; i < sides; i++)
        {
            float angle = i / (float)sides * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            uv.Add(new Vector2(0.5f, 0.5f));
        }

        int capCenter = vertices.Count;
        vertices.Add(Vector3.zero);
        uv.Add(new Vector2(0.5f, 0.5f));

        for (int i = 0; i < sides; i++)
        {
            triangles.Add(capCenter);
            triangles.Add(capStart + i);
            triangles.Add(capStart + (i + 1) % sides);
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material EnsureLitMaterial(string path, string name, Color color, float smoothness)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            material = new Material(FindLitShader());
            material.name = name;
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness);
        }
        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Shader FindLitShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return shader;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(GeneratedFolder))
        {
            AssetDatabase.CreateFolder("Assets", "Generated");
        }
    }
}
#endif
