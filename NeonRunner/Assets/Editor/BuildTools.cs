using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Vytvoří scénu (kamera se září, světlo, herní objekt, materiály) a sestaví macOS aplikaci.
/// Scéna Main.unity se generuje kódem – při každém sestavení se vytvoří znovu.
/// Z příkazové řádky:  Unity -batchmode -quit -projectPath . -executeMethod BuildTools.SetupAndBuild
/// </summary>
[InitializeOnLoad]
public static class BuildTools
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string BuildPath = "Builds/NeonRunner.app";

    static BuildTools()
    {
        if (Application.isBatchMode) return;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(ScenePath))
            {
                SetupScene();
                EditorSceneManager.OpenScene(ScenePath);
            }
        };
    }

    [MenuItem("Neon Runner/1. Vytvořit scénu znovu")]
    public static void SetupScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Materials");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // kamera se září (bloom)
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.008f, 0.008f, 0.025f);
        cam.fieldOfView = 62f;
        cam.farClipPlane = 220f;
        cam.allowHDR = true;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0f, 4.2f, -7.5f);
        var bloom = camGo.AddComponent<NeonBloom>();
        bloom.shader = Shader.Find("Hidden/NeonBloom");
        bloom.intensity = 0.35f;
        bloom.threshold = 1.1f;
        bloom.softKnee = 0.4f;
        bloom.iterations = 4;

        // měsíční světlo
        var sun = new GameObject("Moonlight").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.3f;
        sun.color = new Color(0.9f, 0.92f, 1f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.85f;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var g = new GameObject("Game").AddComponent<RunnerGame>();

        // trať a okolí
        g.groundMat = Mat("Ground", new Color(0.035f, 0.035f, 0.05f), 0f, 0.45f);
        g.stripeMat = Mat("Stripe", new Color(0.2f, 1f, 0.95f), 0.7f);
        g.railMat = Mat("Rail", new Color(0.6f, 0.62f, 0.7f), 0f, 0.85f, 0.8f);
        g.sleeperMat = Mat("Sleeper", new Color(0.16f, 0.12f, 0.1f), 0f, 0.1f);
        g.wallMat = Mat("Wall", new Color(0.1f, 0.1f, 0.13f), 0f, 0.4f);
        g.buildingMat = Mat("Building", new Color(0.025f, 0.022f, 0.045f), 0f, 0.5f);
        g.windowMat = Mat("Window", new Color(1f, 0.25f, 0.8f), 0.8f);
        g.windowMat2 = Mat("Window2", new Color(0.2f, 0.85f, 1f), 0.8f);
        g.windowMat3 = Mat("Window3", new Color(1f, 0.8f, 0.2f), 0.7f);
        g.lampMat = Mat("Lamp", new Color(1f, 0.9f, 0.7f), 2f);

        // překážky
        g.lowMat = Mat("LowBarrier", new Color(0.04f, 0.04f, 0.05f), 0f, 0.5f);
        g.lowStripeMat = Mat("LowBarrierStripe", new Color(1f, 0.82f, 0.05f), 1f);
        g.highMat = Mat("HighGate", new Color(0.06f, 0.05f, 0.07f), 0f, 0.5f);
        g.highLightMat = Mat("HighGateLight", new Color(1f, 0.08f, 0.15f), 1.3f);
        g.trainMat = Mat("Train", new Color(0.82f, 0.84f, 0.9f), 0f, 0.75f, 0.5f);
        g.trainWindowMat = Mat("TrainWindow", new Color(0.4f, 0.9f, 1f), 0.8f);
        g.trainLightMat = Mat("TrainLight", new Color(1f, 0.95f, 0.6f), 2f);
        g.trainStripeMat = Mat("TrainStripe", new Color(1f, 0.15f, 0.1f), 1.1f);

        // předměty
        g.coinMat = Mat("Coin", new Color(1f, 0.8f, 0.15f), 0.7f, 0.9f, 1f);
        g.magnetMat = Mat("Magnet", new Color(0.95f, 0.1f, 0.15f), 0.5f, 0.7f);
        g.magnetTipMat = Mat("MagnetTip", new Color(0.85f, 0.88f, 0.95f), 0.3f, 0.9f, 1f);
        g.jetpackMat = Mat("JetpackTank", new Color(1f, 0.45f, 0.08f), 0.5f, 0.7f, 0.5f);
        g.pickupRingMat = Mat("PickupRing", new Color(0.3f, 1f, 0.9f), 1.2f);

        // hráč
        g.suitMat = Mat("PlayerSuit", new Color(0.35f, 1f, 0.15f), 0.15f, 0.5f);
        g.helmetMat = Mat("PlayerHelmet", new Color(0.92f, 0.93f, 1f), 0f, 0.85f);
        g.visorMat = Mat("PlayerVisor", new Color(1f, 0.55f, 0.1f), 1.4f);
        g.packMat = Mat("PlayerPack", new Color(0.08f, 0.08f, 0.1f), 0f, 0.6f, 0.4f);

        // částice a stopa
        g.particleMat = ParticleMaterial();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("NeonRunner: scéna vytvořena -> " + ScenePath);
    }

    static Material Mat(string name, Color color, float emission = 0f, float smoothness = 0.35f, float metallic = 0f)
    {
        string path = $"Assets/Materials/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        m.SetFloat("_Glossiness", smoothness);
        m.SetFloat("_Metallic", metallic);
        if (emission > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", color * emission);
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material ParticleMaterial()
    {
        const string texPath = "Assets/Materials/SoftDot.png";
        if (!File.Exists(texPath))
        {
            const int size = 64;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f)) / (size * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
            File.WriteAllBytes(texPath, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(texPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        const string matPath = "Assets/Materials/Particle.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (m == null)
        {
            Shader sh = Shader.Find("Legacy Shaders/Particles/Additive");
            if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, matPath);
        }
        m.mainTexture = tex;
        if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(m);
        return m;
    }

    [MenuItem("Neon Runner/2. Sestavit macOS aplikaci")]
    public static void BuildMac()
    {
        SetupScene(); // scéna se vždy vygeneruje podle aktuálního kódu

        PlayerSettings.productName = "Neon Runner";
        PlayerSettings.companyName = "Vojta";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = BuildPath,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"NeonRunner BUILD RESULT: {report.summary.result}, errors: {report.summary.totalErrors}, path: {BuildPath}");

        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    public static void SetupAndBuild()
    {
        BuildMac();
    }
}
