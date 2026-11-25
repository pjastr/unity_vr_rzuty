using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using Object = UnityEngine.Object;

namespace VRThrowing.EditorTools
{
    /// <summary>
    /// Menu "VR Rzuty" w górnym pasku edytora + konfiguracja projektu pod Meta Quest 3S.
    ///   0. Szybki start            - konfiguracja + generowanie sceny jednym kliknięciem,
    ///   1. Konfiguruj projekt      - Android/IL2CPP/ARM64/Vulkan/Linear, URP, OpenXR + Meta XR, tylko kontrolery,
    ///   2. Generuj scenę           - cała scena z prymitywów (VRThrowingSceneBuilder),
    ///   3. Build And Run (USB)     - pełny build + instalacja + uruchomienie,
    ///   4. Patch And Run (USB)     - szybki build: na gogle trafiają tylko zmienione pliki,
    ///   Panel kontrolny...         - okno z zakładkami: kontrola projektu, build i optymalizacja, gogle (ADB).
    /// </summary>
    public static class VRThrowingSetup
    {
        public const string Root = "Assets/VRThrowing";
        public const string ScenePath = Root + "/Scenes/VRRzuty.unity";
        public const string SettingsFolder = Root + "/Settings";
        public const string MaterialsFolder = Root + "/Materials";
        public const string AppId = "com.vrrzuty.quest";
        public const string DocsFile = "INSTRUKCJA.md";
        const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";
        const string M = "VR Rzuty/";

        /// <summary>Funkcje OpenXR włączane automatycznie (nazwy typów).</summary>
        static readonly string[] WantedOpenXRFeatures =
            { "MetaXRFeature", "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile" };

        // =====================================================================
        // MENU
        // =====================================================================

        [MenuItem(M + "0. Szybki start (konfiguracja + scena)", false, 1)]
        public static void QuickStart()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string config = ConfigureProject();
            VRThrowingSceneBuilder.GenerateScene(false, out string sceneInfo);
            VRThrowingWindow.Open();

            string text = "Konfiguracja:\n" + config + "\nScena:\n" + sceneInfo + "\n\n" + ChecklistSummary();
            OfferPlatformSwitch(text);
        }

        [MenuItem(M + "1. Konfiguruj projekt dla Quest 3S", false, 2)]
        public static void ConfigureProjectMenu()
        {
            OfferPlatformSwitch(ConfigureProject());
        }

        [MenuItem(M + "2. Generuj scenę (kosz i kręgle)", false, 3)]
        public static void GenerateSceneMenu() => VRThrowingSceneBuilder.GenerateScene(true, out _);

        [MenuItem(M + "3. Build And Run (USB)", false, 20)]
        public static void BuildAndRunMenu() => VRThrowingBuild.BuildAndRun();

        [MenuItem(M + "4. Patch And Run (USB, szybki)", false, 21)]
        public static void PatchAndRunMenu() => VRThrowingBuild.PatchAndRun(false);

        [MenuItem(M + "Panel kontrolny (kontrola, build, gogle)", false, 40)]
        public static void OpenWindowMenu() => VRThrowingWindow.Open();

        [MenuItem(M + "Otwórz scenę", false, 60)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("VR Rzuty", "Scena jeszcze nie istnieje – użyj „2. Generuj scenę (kosz i kręgle)”.", "OK");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem(M + "Otwórz instrukcję (INSTRUKCJA.md)", false, 61)]
        public static void OpenDocs()
        {
            string path = Path.GetFullPath(DocsFile);
            if (File.Exists(path)) EditorUtility.OpenWithDefaultApp(path);
            else Debug.LogWarning("[VRRzuty] Nie znaleziono " + path);
        }

        static void OfferPlatformSwitch(string report)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (EditorUtility.DisplayDialog("VR Rzuty", report +
                        "\n\nAktywna platforma to nie Android. Przełączyć teraz? (kilka minut, jednorazowo)",
                        "Przełącz na Android", "Później"))
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                }
            }
            else
            {
                EditorUtility.DisplayDialog("VR Rzuty", report, "OK");
            }
        }

        // =====================================================================
        // KONFIGURACJA PROJEKTU
        // =====================================================================

        /// <summary>Ustawia wszystko, co da się ustawić automatycznie. Zwraca raport.</summary>
        public static string ConfigureProject()
        {
            var sb = new StringBuilder();

            ApplyPlayerSettings();
            sb.AppendLine("• Player: IL2CPP, ARM64, min. API 32, Vulkan, Linear, id " + AppId);

            EnsureUrp();
            sb.AppendLine("• URP: przypięty (MSAA 4x, bez HDR, bez cieni i świateł dodatkowych)");

            sb.AppendLine(SetHandTrackingSupport("ControllersOnly")
                ? "• Meta: Hand Tracking Support = Controllers Only"
                : "• [!] Hand Tracking Support – ustaw ręcznie Controllers Only (OVRManager › Quest Features)");

            sb.Append(ConfigureXR());

            VRThrowingBuild.ApplyProfile(BuildProfileKind.Dev);
            sb.AppendLine("• Build: profil DEV (szybki Build & Run, Development + Script Debugging)");

            if (File.Exists(ScenePath)) AddSceneToBuild();
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        public static void ApplyPlayerSettings()
        {
            var android = NamedBuildTarget.Android;
            PlayerSettings.companyName = "VRRzuty";
            PlayerSettings.productName = "VR Rzuty";
            PlayerSettings.SetApplicationIdentifier(android, AppId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            if ((int)PlayerSettings.Android.minSdkVersion < 32)
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32; // Android 12L - minimum Horizon OS
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            EditorUserBuildSettings.buildAppBundle = false;
        }

        public static bool PlayerSettingsOk(out string detail)
        {
            var android = NamedBuildTarget.Android;
            var problems = new List<string>();
            if (PlayerSettings.GetScriptingBackend(android) != ScriptingImplementation.IL2CPP) problems.Add("backend ≠ IL2CPP");
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64) problems.Add("architektura ≠ tylko ARM64");
            if ((int)PlayerSettings.Android.minSdkVersion < 32) problems.Add("min. API < 32");
            if (PlayerSettings.colorSpace != ColorSpace.Linear) problems.Add("Color Space ≠ Linear");
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (apis.Length == 0 || apis[0] != GraphicsDeviceType.Vulkan) problems.Add("Graphics API ≠ Vulkan");
            detail = problems.Count == 0 ? "IL2CPP, ARM64, API 32+, Vulkan, Linear" : string.Join(", ", problems);
            return problems.Count == 0;
        }

        // ---------------------------------------------------------------- URP

        public static UniversalRenderPipelineAsset EnsureUrp()
        {
            var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                EnsureFolder(SettingsFolder);
                string assetPath = SettingsFolder + "/VRRzuty_URP.asset";
                urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
                if (urp == null)
                {
                    var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rendererData, SettingsFolder + "/VRRzuty_URP_Renderer.asset");
                    urp = UniversalRenderPipelineAsset.Create(rendererData);
                    AssetDatabase.CreateAsset(urp, assetPath);
                }
                GraphicsSettings.defaultRenderPipeline = urp;
            }

            // Ustawienia pod mobilne GPU Questa: mniej wariantów shaderów = szybszy build i szybsze ładowanie.
            urp.msaaSampleCount = 4;
            urp.supportsHDR = false;
            urp.supportsCameraDepthTexture = false;
            urp.supportsCameraOpaqueTexture = false;
            urp.renderScale = 1f;
            urp.shadowDistance = 0f;

            var so = new SerializedObject(urp);
            SetSerialized(so, "m_MainLightShadowsSupported", false);
            SetSerialized(so, "m_AdditionalLightShadowsSupported", false);
            SetSerialized(so, "m_SoftShadowsSupported", false);
            SetSerialized(so, "m_AdditionalLightsRenderingMode", 0); // Disabled
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);

            // Poziomy jakości z innym potokiem -> używają domyślnego (naszego URP).
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                if (QualitySettings.renderPipeline != null && QualitySettings.renderPipeline != urp)
                    QualitySettings.renderPipeline = null;
            }
            QualitySettings.SetQualityLevel(current, false);

            AssetDatabase.SaveAssets();
            return urp;
        }

        public static bool UrpOk() => GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset;

        static void SetSerialized(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name);
            if (p == null) return;
            if (value is bool && p.propertyType == SerializedPropertyType.Boolean) p.boolValue = (bool)value;
            else if (value is int && (p.propertyType == SerializedPropertyType.Enum || p.propertyType == SerializedPropertyType.Integer)) p.intValue = (int)value;
            else if (value is float && p.propertyType == SerializedPropertyType.Float) p.floatValue = (float)value;
        }

        // ---------------------------------------------------------------- XR (OpenXR + Meta XR)

        /// <summary>Włącza loader OpenXR dla Androida i funkcję "Meta XR Feature" + profile kontrolerów Touch.</summary>
        public static string ConfigureXR()
        {
            var sb = new StringBuilder();
            try
            {
                var manager = AndroidXRManager(true);
                if (manager == null)
                    sb.AppendLine("• [!] XR Plug-in Management: nie udało się utworzyć ustawień – otwórz Project Settings › XR Plug-in Management i zaznacz OpenXR (Android).");
                else if (IsOpenXRActive())
                    sb.AppendLine("• XR: OpenXR włączony dla Androida");
                else if (XRPackageMetadataStore.AssignLoader(manager, OpenXRLoaderType, BuildTargetGroup.Android))
                    sb.AppendLine("• XR: włączono OpenXR dla Androida");
                else
                    sb.AppendLine("• [!] XR: nie udało się włączyć OpenXR – zaznacz go ręcznie (Project Settings › XR Plug-in Management › Android).");
            }
            catch (Exception e)
            {
                sb.AppendLine("• [!] XR: " + e.Message);
            }

            sb.AppendLine(EnableOpenXRFeatures());
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        public static XRManagerSettings AndroidXRManager(bool create)
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget);
            if (perTarget == null)
            {
                if (!create) return null;
                EnsureFolder("Assets/XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))
            {
                if (!create) return null;
                perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            }
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
            {
                if (!create) return null;
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            }
            return perTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        }

        public static bool IsOpenXRActive()
        {
            try
            {
                var manager = AndroidXRManager(false);
                return manager != null && manager.activeLoaders.Any(l => l != null && l.GetType().FullName == OpenXRLoaderType);
            }
            catch
            {
                return false;
            }
        }

        static string EnableOpenXRFeatures()
        {
            OpenXRSettings settings;
            try { settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android); }
            catch { settings = null; }
            if (settings == null)
                return "• [!] OpenXR: brak ustawień dla Androida – uruchom konfigurację ponownie po przeładowaniu edytora.";

            TryEnableMetaFeatureSet();

            foreach (var f in settings.GetFeatures())
            {
                if (f == null || !WantedOpenXRFeatures.Contains(f.GetType().Name) || f.enabled) continue;
                f.enabled = true;
                EditorUtility.SetDirty(f);
            }
            EditorUtility.SetDirty(settings);

            return IsMetaFeatureEnabled()
                ? "• OpenXR: Meta XR Feature + profile kontrolerów Touch włączone"
                : "• [!] OpenXR: nie znaleziono „Meta XR Feature” – zaznacz grupę Meta XR w XR Plug-in Management › OpenXR (Android).";
        }

        public static bool IsMetaFeatureEnabled()
        {
            try
            {
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                return settings != null && settings.GetFeatures().Any(f => f != null && f.enabled && f.GetType().Name == "MetaXRFeature");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Próbuje zaznaczyć grupę funkcji "Meta XR" (API wewnętrzne OpenXR – przez refleksję, bez ryzyka błędów kompilacji).</summary>
        static void TryEnableMetaFeatureSet()
        {
            try
            {
                Type mgr = FindType("UnityEditor.XR.OpenXR.Features.OpenXRFeatureSetManager");
                if (mgr == null) return;
                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                var list = mgr.GetMethod("FeatureSetsForBuildTarget", flags, null, new[] { typeof(BuildTargetGroup) }, null);
                if (!(list?.Invoke(null, new object[] { BuildTargetGroup.Android }) is IEnumerable sets)) return;

                bool changed = false;
                foreach (var fs in sets)
                {
                    if (fs == null) continue;
                    var t = fs.GetType();
                    string id = t.GetField("featureSetId")?.GetValue(fs) as string;
                    var enabledField = t.GetField("isEnabled");
                    if (id == null || enabledField == null) continue;
                    if (id.Replace(".", "").IndexOf("metaxr", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!(bool)enabledField.GetValue(fs))
                    {
                        enabledField.SetValue(fs, true);
                        changed = true;
                    }
                }
                if (changed)
                    mgr.GetMethod("SetFeaturesFromEnabledFeatureSets", flags, null, new[] { typeof(BuildTargetGroup) }, null)
                        ?.Invoke(null, new object[] { BuildTargetGroup.Android });
            }
            catch (Exception e)
            {
                Debug.Log("[VRRzuty] Grupa Meta XR nie została zaznaczona automatycznie: " + e.Message);
            }
        }

        public static void OpenXRSettingsWindow() => SettingsService.OpenProjectSettings("Project/XR Plug-in Management");

        // ---------------------------------------------------------------- Meta: tylko kontrolery

        /// <summary>OVRProjectConfig.handTrackingSupport (przez refleksję - odporne na zmiany API SDK).</summary>
        public static bool SetHandTrackingSupport(string value)
        {
            try
            {
                object config = GetProjectConfig(out Type t);
                if (config == null) return false;
                var field = t.GetField("handTrackingSupport", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null || !field.FieldType.IsEnum || !Enum.GetNames(field.FieldType).Contains(value)) return false;
                field.SetValue(config, Enum.Parse(field.FieldType, value));

                var commit = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "CommitProjectConfig" && m.GetParameters().Length == 1);
                if (commit != null) commit.Invoke(null, new[] { config });
                else if (config is Object o) { EditorUtility.SetDirty(o); AssetDatabase.SaveAssets(); }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VRRzuty] Hand Tracking Support: " + e.Message);
                return false;
            }
        }

        public static string GetHandTrackingSupport()
        {
            try
            {
                object config = GetProjectConfig(out Type t);
                var field = t?.GetField("handTrackingSupport", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return config != null && field != null ? field.GetValue(config).ToString() : "?";
            }
            catch
            {
                return "?";
            }
        }

        static object GetProjectConfig(out Type t)
        {
            t = FindType("OVRProjectConfig");
            if (t == null) return null;
            object config = null;
            var getter = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "GetProjectConfig");
            if (getter != null) config = getter.Invoke(null, DefaultArgs(getter));
            if (config == null)
            {
                var prop = t.GetProperty("CachedProjectConfig", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (prop != null) config = prop.GetValue(null);
            }
            return config;
        }

        // =====================================================================
        // LISTA KONTROLNA (używana przez Panel kontrolny i raporty)
        // =====================================================================

        public class CheckItem
        {
            public string title;
            public string detail;
            public bool ok;
            public bool manual;        // krok ręczny - nie da się sprawdzić automatycznie
            public string fixLabel;
            public Action fix;
            public string extraLabel;
            public Action extra;
        }

        public static List<CheckItem> GetChecklist()
        {
            var list = new List<CheckItem>();

            bool android = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
            list.Add(new CheckItem
            {
                title = "Platforma Android aktywna",
                ok = android,
                detail = android ? "OK" : "Aktywna: " + EditorUserBuildSettings.activeBuildTarget + " (przełączenie trwa kilka minut, jednorazowo)",
                fixLabel = "Przełącz",
                fix = () => EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android)
            });

            bool ps = PlayerSettingsOk(out string psDetail);
            list.Add(new CheckItem { title = "Player Settings (Quest)", ok = ps, detail = psDetail, fixLabel = "Ustaw", fix = ApplyPlayerSettings });

            bool urp = UrpOk();
            list.Add(new CheckItem
            {
                title = "Universal Render Pipeline",
                ok = urp,
                detail = urp ? GraphicsSettings.defaultRenderPipeline.name : "brak przypiętego URP",
                fixLabel = "Ustaw",
                fix = () => EnsureUrp()
            });

            bool oxr = IsOpenXRActive();
            list.Add(new CheckItem
            {
                title = "XR Plug-in Management: OpenXR (Android)",
                ok = oxr,
                detail = oxr ? "OK" : "OpenXR nie jest zaznaczony dla Androida",
                fixLabel = "Włącz",
                fix = () => Debug.Log("[VRRzuty]\n" + ConfigureXR()),
                extraLabel = "Otwórz ustawienia",
                extra = OpenXRSettingsWindow
            });

            bool meta = IsMetaFeatureEnabled();
            list.Add(new CheckItem
            {
                title = "OpenXR: Meta XR Feature",
                ok = meta,
                detail = meta ? "OK" : "wymagany przez Meta XR Core SDK (OVRInput, OVRCameraRig)",
                fixLabel = "Włącz",
                fix = () => Debug.Log("[VRRzuty]\n" + ConfigureXR()),
                extraLabel = "Otwórz ustawienia",
                extra = OpenXRSettingsWindow
            });

            string hts = GetHandTrackingSupport();
            list.Add(new CheckItem
            {
                title = "Tylko kontrolery (Hand Tracking Support)",
                ok = hts == "ControllersOnly",
                detail = "obecnie: " + hts,
                fixLabel = "Ustaw",
                fix = () => SetHandTrackingSupport("ControllersOnly")
            });

            var scene = GetSceneStatus();
            list.Add(new CheckItem
            {
                title = "Scena istnieje i jest w buildzie",
                ok = scene.exists && scene.inBuild,
                detail = !scene.exists ? "brak " + ScenePath : !scene.inBuild ? "nie ma jej na liście scen buildu" : ScenePath,
                fixLabel = scene.exists ? "Dodaj do buildu" : "Generuj",
                fix = () => { if (File.Exists(ScenePath)) AddSceneToBuild(); else VRThrowingSceneBuilder.GenerateScene(false, out _); }
            });

            if (scene.isOpen)
            {
                bool rigOk = scene.rigs == 1 && scene.strayCameras == 0;
                list.Add(new CheckItem
                {
                    title = "Scena: jeden OVRCameraRig, brak innych kamer",
                    ok = rigOk,
                    detail = $"rigi: {scene.rigs}, kamery spoza rigu: {scene.strayCameras}",
                    fixLabel = "Generuj ponownie",
                    fix = () => VRThrowingSceneBuilder.GenerateScene(false, out _)
                });
                list.Add(new CheckItem
                {
                    title = "OVRManager: Tracking Origin = Floor Level",
                    ok = scene.floorLevel,
                    detail = scene.floorLevel ? "OK" : "wysokość sceny = podłoga pokoju",
                    fixLabel = "Ustaw",
                    fix = () =>
                    {
                        var mgr = Object.FindAnyObjectByType<OVRManager>(FindObjectsInactive.Include);
                        if (mgr != null && SetEnum(mgr, "FloorLevel", "_trackingOriginType", "m_TrackingOriginType", "trackingOriginType"))
                        {
                            EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
                            EditorSceneManager.SaveScene(mgr.gameObject.scene);
                        }
                    }
                });
            }
            else if (scene.exists)
            {
                list.Add(new CheckItem
                {
                    title = "Scena otwarta",
                    ok = false,
                    detail = "otwórz scenę, aby sprawdzić rig i OVRManager",
                    fixLabel = "Otwórz",
                    fix = OpenScene
                });
            }

            list.Add(new CheckItem
            {
                title = "Meta Project Setup Tool (ręcznie, jednorazowo)",
                manual = true,
                detail = "Zakładka Android: „Fix All”, potem „Apply All” – aż znikną błędy „Required”.",
                fixLabel = "Otwórz",
                fix = OpenProjectSetupTool
            });

            return list;
        }

        public static string ChecklistSummary()
        {
            var sb = new StringBuilder("Lista kontrolna:\n");
            foreach (var c in GetChecklist())
                sb.Append(c.manual ? "[RĘCZNIE] " : c.ok ? "[OK]   " : "[!!]   ").Append(c.title)
                  .AppendLine(c.ok ? "" : " – " + c.detail);
            return sb.ToString();
        }

        public struct SceneStatus
        {
            public bool exists, inBuild, isOpen, floorLevel;
            public int rigs, strayCameras;
        }

        public static SceneStatus GetSceneStatus()
        {
            var s = new SceneStatus
            {
                exists = File.Exists(ScenePath),
                inBuild = EditorBuildSettings.scenes.Any(e => e.path == ScenePath && e.enabled),
                isOpen = EditorSceneManager.GetActiveScene().path == ScenePath
            };
            if (!s.isOpen) return s;

            var rigs = Object.FindObjectsByType<OVRCameraRig>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            s.rigs = rigs.Length;
            s.strayCameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Count(c => rigs.All(r => !c.transform.IsChildOf(r.transform)));
            var mgr = Object.FindAnyObjectByType<OVRManager>(FindObjectsInactive.Include);
            s.floorLevel = mgr != null && GetEnumName(mgr, "_trackingOriginType", "m_TrackingOriginType", "trackingOriginType") == "FloorLevel";
            return s;
        }

        // =====================================================================
        // NARZĘDZIA META
        // =====================================================================

        public static void OpenProjectSetupTool()
        {
            if (!ExecuteFirstMenu("Meta/Tools/Project Setup Tool", "Oculus/Tools/Project Setup Tool", "Meta/Project Setup Tool"))
                EditorUtility.DisplayDialog("VR Rzuty",
                    "Nie znaleziono menu Project Setup Tool. Otwórz je ręcznie: Meta › Tools › Project Setup Tool " +
                    "(albo ikona Meta w pasku stanu edytora, prawy dolny róg).", "OK");
        }

        public static void OpenBuildingBlocks()
        {
            if (!ExecuteFirstMenu("Meta/Tools/Building Blocks", "Oculus/Tools/Building Blocks", "Window/Meta/Meta XR SDK/Building Blocks"))
                EditorUtility.DisplayDialog("VR Rzuty", "Nie znaleziono menu Building Blocks (Meta › Tools › Building Blocks).", "OK");
        }

        static bool ExecuteFirstMenu(params string[] paths)
        {
            foreach (var p in paths)
                if (EditorApplication.ExecuteMenuItem(p)) return true;
            return false;
        }

        // =====================================================================
        // POMOCNICZE
        // =====================================================================

        public static void AddSceneToBuild()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static Type FindType(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(name);
                if (t != null) return t;
            }
            return null;
        }

        static object[] DefaultArgs(MethodInfo m) =>
            m.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue
                : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();

        public static bool SetEnum(Object target, string valueName, params string[] propertyNames)
        {
            if (target == null) return false;
            var so = new SerializedObject(target);
            foreach (string pn in propertyNames)
            {
                var p = so.FindProperty(pn);
                if (p == null || p.propertyType != SerializedPropertyType.Enum) continue;
                int i = Array.IndexOf(p.enumNames, valueName);
                if (i < 0) continue;
                if (p.enumValueIndex != i)
                {
                    p.enumValueIndex = i;
                    so.ApplyModifiedProperties();
                }
                return true;
            }
            Debug.LogWarning($"[VRRzuty] Nie ustawiono {valueName} na {target.GetType().Name} – ustaw ręcznie w Inspectorze.");
            return false;
        }

        static string GetEnumName(Object target, params string[] propertyNames)
        {
            if (target == null) return null;
            var so = new SerializedObject(target);
            foreach (string pn in propertyNames)
            {
                var p = so.FindProperty(pn);
                if (p == null || p.propertyType != SerializedPropertyType.Enum) continue;
                int i = p.enumValueIndex;
                return i >= 0 && i < p.enumNames.Length ? p.enumNames[i] : null;
            }
            return null;
        }
    }

    /// <summary>Przy pierwszym otwarciu projektu: proponuje szybki start albo otwiera scenę.</summary>
    [InitializeOnLoad]
    static class VRThrowingFirstRun
    {
        const string Key = "VRRzuty.FirstRunChecked";

        static VRThrowingFirstRun()
        {
            if (Application.isBatchMode || SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, true);
            EditorApplication.delayCall += Check;
        }

        static void Check()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var active = EditorSceneManager.GetActiveScene();

            if (File.Exists(VRThrowingSetup.ScenePath))
            {
                if (string.IsNullOrEmpty(active.path) && !active.isDirty) EditorSceneManager.OpenScene(VRThrowingSetup.ScenePath);
                return;
            }

            if (EditorUtility.DisplayDialog("VR Rzuty",
                    "Scena z koszem i torem jeszcze nie istnieje.\n\n" +
                    "„Szybki start” skonfiguruje projekt pod Meta Quest 3S (Android, URP, OpenXR + Meta XR, tylko kontrolery, " +
                    "profil szybkiego buildu) i wygeneruje scenę.\n\nTo samo znajdziesz później w menu VR Rzuty.",
                    "Szybki start", "Później"))
            {
                VRThrowingSetup.QuickStart();
            }
        }
    }
}
