using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Rollzy.EditorTools
{
    /// <summary>One-click project setup. Menu: Rollzy 3D → ...</summary>
    public static class RollzySetup
    {
        const string Root = "Assets/_Project";
        const string ScenePath = Root + "/Scenes/Gameplay.unity";
        const string MatDir = Root + "/Art/Materials";
        const string UiDir = Root + "/UI";
        const string AdMobDefine = "ROLLZY_ADMOB";

        // ------------------------------------------------------------ 1. scene
        [MenuItem("Rollzy 3D/1. Create Gameplay Scene", priority = 1)]
        public static void CreateGameplayScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Rollzy 3D", "Gameplay.unity already exists. Rebuild it?", "Rebuild", "Cancel")) return;
            AssetDatabase.Refresh();
            EnsureFolder(MatDir);

            var lit = MakeMaterial("M_LitTemplate", "Universal Render Pipeline/Lit", m =>
            {
                m.SetFloat("_Smoothness", 0.35f);
                m.enableInstancing = true;
            });
            var unlit = MakeMaterial("M_UnlitTemplate", "Universal Render Pipeline/Unlit", null);
            var particles = MakeMaterial("M_ParticlesAdditive", "Universal Render Pipeline/Particles/Unlit", m =>
            {
                m.SetFloat("_Surface", 1);   // transparent
                m.SetFloat("_Blend", 2);     // additive
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            });
            var sky = MakeMaterial("M_SkyGradient", "Skybox/Panoramic", m =>
            {
                m.SetFloat("_Mapping", 1);
                m.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
                m.SetFloat("_Exposure", 1);
            });

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UiDir + "/Rollzy.uss");
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UiDir + "/RollzyTheme.tss");
            if (styleSheet == null || theme == null)
                Debug.LogWarning("[Rollzy 3D] UI files not found in " + UiDir + ". Make sure the _Project/UI folder was copied in.");
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiDir + "/RollzyPanel.asset");
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, UiDir + "/RollzyPanel.asset");
            }
            panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1080, 1920);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0, 6.5f, -8);
            camGo.transform.LookAt(new Vector3(0, 0, 8));

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            var uiGo = new GameObject("UI");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = panel;

            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif

            var gameGo = new GameObject("Game");
            gameGo.AddComponent<SwipeInput>();
            var game = gameGo.AddComponent<RollzyGame>();
            var so = new SerializedObject(game);
            so.FindProperty("litTemplate").objectReferenceValue = lit;
            so.FindProperty("unlitTemplate").objectReferenceValue = unlit;
            so.FindProperty("particleTemplate").objectReferenceValue = particles;
            so.FindProperty("skyboxTemplate").objectReferenceValue = sky;
            so.FindProperty("uiDocument").objectReferenceValue = doc;
            so.FindProperty("styleSheet").objectReferenceValue = styleSheet;
            so.FindProperty("mainCamera").objectReferenceValue = cam;
            so.FindProperty("sun").objectReferenceValue = sun;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureFolder(Root + "/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Rollzy 3D] Gameplay scene ready. Press Play. (Mouse drag = swipe, arrows/space also work.)");
        }

        static Material MakeMaterial(string name, string shaderName, Action<Material> setup)
        {
            string path = MatDir + "/" + name + ".mat";
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[Rollzy 3D] Shader '{shaderName}' not found. Is this a URP project?");
                shader = Shader.Find("Standard");
            }
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            var m = existing != null ? existing : new Material(shader);
            m.shader = shader;
            setup?.Invoke(m);
            if (existing == null) AssetDatabase.CreateAsset(m, path);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------ 2. android settings
        [MenuItem("Rollzy 3D/2. Apply Android Player Settings", priority = 2)]
        public static void ApplyAndroidSettings()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.productName = "Rollzy 3D";
            var company = PlayerSettings.companyName;
            if (string.IsNullOrEmpty(company) || company == "DefaultCompany" || company.StartsWith("Unity"))
                PlayerSettings.companyName = "YourStudio";
            var appId = PlayerSettings.GetApplicationIdentifier(android);
            // Replace placeholder ids from new projects and Unity templates; keep anything the developer chose.
            if (string.IsNullOrEmpty(appId) || appId.StartsWith("com.DefaultCompany") || appId.StartsWith("com.UnityTechnologies"))
                PlayerSettings.SetApplicationIdentifier(android, "com.yourstudio.rollzy3d");
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if ((int)PlayerSettings.Android.minSdkVersion < 23) PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)23;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)36; // Google Play requirement from Aug 31 2026
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/AppIcon.png");
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            if (PlayerSettings.bundleVersion == "0.1" || PlayerSettings.bundleVersion == "0.1.0" || string.IsNullOrEmpty(PlayerSettings.bundleVersion)) PlayerSettings.bundleVersion = "1.0.0";
            Debug.Log("[Rollzy 3D] Android settings applied (IL2CPP, ARM64, API 23–36, Vulkan+GLES3, portrait, linear). " +
                      "Package name: " + PlayerSettings.GetApplicationIdentifier(android) + " — change it before publishing.");
        }

        // ------------------------------------------------------------ 3. admob
        [MenuItem("Rollzy 3D/3. Enable AdMob (after importing the plugin)", priority = 3)]
        public static void EnableAdMob()
        {
            bool pluginPresent = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType("GoogleMobileAds.Api.MobileAds") != null);
            if (!pluginPresent)
            {
                EditorUtility.DisplayDialog("Rollzy 3D",
                    "Google Mobile Ads plugin not found.\n\nDownload the latest GoogleMobileAds-*.unitypackage from github.com/googleads/googleads-mobile-unity/releases, " +
                    "import it (Assets → Import Package → Custom Package), then run this menu again.", "OK");
                return;
            }
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                var defines = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(d => d.Length > 0).ToList();
                if (!defines.Contains(AdMobDefine)) defines.Add(AdMobDefine);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            }
            Debug.Log("[Rollzy 3D] AdMob enabled with Google's TEST ad units. Next: Assets → Google Mobile Ads → Settings → enter your AdMob App ID. " +
                      "Before release, replace the test unit IDs in Scripts/Ads/AdProviders.cs.");
        }

        [MenuItem("Rollzy 3D/3b. Disable AdMob (use test ads)", priority = 4)]
        public static void DisableAdMob()
        {
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                var defines = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Where(d => d.Length > 0 && d != AdMobDefine);
                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            }
            Debug.Log("[Rollzy 3D] AdMob disabled; the game shows its own TEST AD screen.");
        }

        // ------------------------------------------------------------ 4/5. builds
        [MenuItem("Rollzy 3D/4. Build APK for my phone", priority = 20)]
        public static void BuildApk() => Build(false);

        [MenuItem("Rollzy 3D/5. Build AAB for Google Play", priority = 21)]
        public static void BuildAab()
        {
            if (string.IsNullOrEmpty(PlayerSettings.Android.keystoreName))
            {
                EditorUtility.DisplayDialog("Rollzy 3D",
                    "Google Play needs a signed bundle.\n\nCreate a keystore first: Edit → Project Settings → Player → Android → Publishing Settings → Keystore Manager. " +
                    "Keep that keystore and its passwords safe: you need the same one for every update.", "OK");
                return;
            }
            Build(true);
        }

        static void Build(bool bundle)
        {
            if (!File.Exists(ScenePath)) CreateGameplayScene();
            ApplyAndroidSettings();
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = bundle;
            Directory.CreateDirectory("Builds");
            string path = "Builds/Rollzy3D." + (bundle ? "aab" : "apk");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[Rollzy 3D] Built {path} ({new FileInfo(path).Length / (1024 * 1024)} MB).");
                EditorUtility.RevealInFinder(path);
            }
            else Debug.LogError("[Rollzy 3D] Build failed: " + report.summary.result + ". See the Console for the first red error.");
        }

        [MenuItem("Rollzy 3D/Reset Save Data", priority = 50)]
        public static void ResetSaveData()
        {
            SaveData.Delete();
            Debug.Log("[Rollzy 3D] Save data deleted.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
