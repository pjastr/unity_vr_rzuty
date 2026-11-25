using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace VRThrowing.EditorTools
{
    public enum BuildProfileKind { Dev, Release }

    /// <summary>
    /// Profile ustawień buildu i uruchamianie na goglach przez USB.
    ///  DEV     - najkrótszy czas Build &amp; Run (IL2CPP Debug, "Faster (smaller) builds", LZ4, bez symbols.zip),
    ///            Development Build + Script Debugging do debugowania przez USB.
    ///  RELEASE - szybkie ładowanie i wydajność (IL2CPP Release, Optimize Speed, LZ4HC, bez Development).
    /// Wspólne dla obu: bez splash screena Unity, ASTC, ARM64, incremental GC, strip unused mesh components.
    /// </summary>
    public static class VRThrowingBuild
    {
        public const string ApkPath = "Builds/VRRzuty.apk";
        public const string ReleaseApkPath = "Builds/VRRzuty_release.apk";
        const string P = "VRRzuty.Build.";

        enum BuildMode { Full, Patch, PatchScriptsOnly }

        // ---------------------------------------------------------------- opcje (EditorPrefs)

        public static bool ScriptDebugging
        {
            get => EditorPrefs.GetBool(P + "ScriptDebugging", true);
            set => EditorPrefs.SetBool(P + "ScriptDebugging", value);
        }

        public static bool AutoconnectProfiler
        {
            get => EditorPrefs.GetBool(P + "Profiler", false);
            set => EditorPrefs.SetBool(P + "Profiler", value);
        }

        public static bool ClearLogcatBeforeRun
        {
            get => EditorPrefs.GetBool(P + "ClearLogcat", true);
            set => EditorPrefs.SetBool(P + "ClearLogcat", value);
        }

        static bool UseLz4HC
        {
            get => EditorPrefs.GetBool(P + "Lz4HC", false);
            set => EditorPrefs.SetBool(P + "Lz4HC", value);
        }

        public static string LastBuildInfo => EditorPrefs.GetString(P + "LastInfo", "");

        // ---------------------------------------------------------------- tabela ustawień

        class Setting
        {
            public string name;
            public string hint;
            public Func<string> get;
            public Func<BuildProfileKind, string> want;
            public Action<BuildProfileKind> apply;
        }

        public class SettingRow
        {
            public string name, hint, current, dev, release;
            public bool MatchesDev => current == dev;
            public bool MatchesRelease => current == release;
        }

        const string Yes = "tak", No = "nie";
        static string YN(bool b) => b ? Yes : No;
        static NamedBuildTarget Android => NamedBuildTarget.Android;
        static bool IsDev(BuildProfileKind k) => k == BuildProfileKind.Dev;

        static readonly Setting[] Settings =
        {
            new Setting
            {
                name = "Development Build", hint = "Logi, profiler, debugger – tylko do testów",
                get = () => YN(EditorUserBuildSettings.development),
                want = k => YN(IsDev(k)),
                apply = k => EditorUserBuildSettings.development = IsDev(k)
            },
            new Setting
            {
                name = "Script Debugging", hint = "Podłączanie debuggera C# (Rider/VS) przez USB",
                get = () => YN(EditorUserBuildSettings.allowDebugging),
                want = k => YN(IsDev(k) && ScriptDebugging),
                apply = k => EditorUserBuildSettings.allowDebugging = IsDev(k) && ScriptDebugging
            },
            new Setting
            {
                name = "Autoconnect Profiler", hint = "Profiler łączy się sam po starcie (wolniejszy start)",
                get = () => YN(EditorUserBuildSettings.connectProfiler),
                want = k => YN(IsDev(k) && AutoconnectProfiler),
                apply = k => EditorUserBuildSettings.connectProfiler = IsDev(k) && AutoconnectProfiler
            },
            new Setting
            {
                name = "IL2CPP Compiler Configuration", hint = "Debug = najszybsza kompilacja C++, Release = szybszy kod",
                get = () => PlayerSettings.GetIl2CppCompilerConfiguration(Android).ToString(),
                want = k => IsDev(k) ? "Debug" : "Release",
                apply = k => PlayerSettings.SetIl2CppCompilerConfiguration(Android,
                    IsDev(k) ? Il2CppCompilerConfiguration.Debug : Il2CppCompilerConfiguration.Release)
            },
            new Setting
            {
                name = "IL2CPP Code Generation", hint = "OptimizeSize = „Faster (smaller) builds”",
                get = () => PlayerSettings.GetIl2CppCodeGeneration(Android).ToString(),
                want = k => IsDev(k) ? "OptimizeSize" : "OptimizeSpeed",
                apply = k => PlayerSettings.SetIl2CppCodeGeneration(Android,
                    IsDev(k) ? Il2CppCodeGeneration.OptimizeSize : Il2CppCodeGeneration.OptimizeSpeed)
            },
            new Setting
            {
                name = "Managed Stripping Level", hint = "Low – bezpieczny dla Meta SDK (refleksja)",
                get = () => PlayerSettings.GetManagedStrippingLevel(Android).ToString(),
                want = k => "Low",
                apply = k => PlayerSettings.SetManagedStrippingLevel(Android, ManagedStrippingLevel.Low)
            },
            new Setting
            {
                name = "Kompresja APK", hint = "LZ4 = szybszy build, LZ4HC = mniejszy APK (ładuje się równie szybko)",
                get = () => UseLz4HC ? "LZ4HC" : "LZ4",
                want = k => IsDev(k) ? "LZ4" : "LZ4HC",
                apply = k => UseLz4HC = !IsDev(k)
            },
            new Setting
            {
                name = "Symbole (symbols.zip)", hint = "Pakowanie symboli wydłuża build",
                get = GetDebugSymbols,
                want = k => "brak",
                apply = k => SetDebugSymbolsNone()
            },
            new Setting
            {
                name = "Minify (R8/Proguard)", hint = "Wydłuża krok Gradle, w tym projekcie zbędne",
                get = () => YN(GetStaticBool(typeof(PlayerSettings.Android), "minifyDebug") || GetStaticBool(typeof(PlayerSettings.Android), "minifyRelease")),
                want = k => No,
                apply = k =>
                {
                    SetStatic(typeof(PlayerSettings.Android), "minifyDebug", false);
                    SetStatic(typeof(PlayerSettings.Android), "minifyRelease", false);
                }
            },
            new Setting
            {
                name = "Splash screen Unity", hint = "Wyłączony = aplikacja startuje szybciej",
                get = () => YN(PlayerSettings.SplashScreen.show),
                want = k => No,
                apply = k => PlayerSettings.SplashScreen.show = false
            },
            new Setting
            {
                name = "Incremental GC", hint = "Mniej przycięć klatek od Garbage Collectora",
                get = () => YN(PlayerSettings.gcIncremental),
                want = k => Yes,
                apply = k => PlayerSettings.gcIncremental = true
            },
            new Setting
            {
                name = "Strip Unused Mesh Components", hint = "Mniejsze dane siatek = szybsze ładowanie",
                get = () => YN(PlayerSettings.stripUnusedMeshComponents),
                want = k => Yes,
                apply = k => PlayerSettings.stripUnusedMeshComponents = true
            },
            new Setting
            {
                name = "Kompresja tekstur", hint = "ASTC – natywny format GPU Questa",
                get = () => GetStatic(typeof(EditorUserBuildSettings), "androidBuildSubtarget"),
                want = k => "ASTC",
                apply = k => SetStatic(typeof(EditorUserBuildSettings), "androidBuildSubtarget", "ASTC")
            },
            new Setting
            {
                name = "Optimized Frame Pacing", hint = "W VR tempo klatek ustala kompozytor Meta",
                get = () => YN(GetStaticBool(typeof(PlayerSettings.Android), "optimizedFramePacing")),
                want = k => No,
                apply = k => SetStatic(typeof(PlayerSettings.Android), "optimizedFramePacing", false)
            },
            new Setting
            {
                name = "Build App Bundle (AAB)", hint = "Do instalacji przez USB potrzebny APK",
                get = () => YN(EditorUserBuildSettings.buildAppBundle),
                want = k => No,
                apply = k => EditorUserBuildSettings.buildAppBundle = false
            },
        };

        public static List<SettingRow> DescribeSettings()
        {
            var rows = new List<SettingRow>();
            foreach (var s in Settings)
            {
                string current;
                try { current = s.get(); } catch { current = "n/d"; }
                rows.Add(new SettingRow
                {
                    name = s.name, hint = s.hint, current = current,
                    dev = s.want(BuildProfileKind.Dev), release = s.want(BuildProfileKind.Release)
                });
            }
            return rows;
        }

        public static void ApplyProfile(BuildProfileKind kind)
        {
            foreach (var s in Settings)
            {
                try { s.apply(kind); }
                catch (Exception e) { Debug.LogWarning($"[VRRzuty] Nie ustawiono „{s.name}”: {e.Message}"); }
            }
            // Wspólne wymagania Quest (gdyby ktoś je zmienił).
            PlayerSettings.SetScriptingBackend(Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            AssetDatabase.SaveAssets();
            Debug.Log($"[VRRzuty] Zastosowano profil buildu {kind}.");
        }

        public static int MismatchCount(BuildProfileKind kind) =>
            DescribeSettings().Count(r => r.current != (kind == BuildProfileKind.Dev ? r.dev : r.release));

        // ---------------------------------------------------------------- build

        public static void BuildAndRun() => Build(BuildProfileKind.Dev, BuildMode.Full, true);
        public static void PatchAndRun(bool scriptsOnly) => Build(BuildProfileKind.Dev, scriptsOnly ? BuildMode.PatchScriptsOnly : BuildMode.Patch, true);
        public static void BuildRelease(bool run) => Build(BuildProfileKind.Release, BuildMode.Full, run);

        static bool Build(BuildProfileKind kind, BuildMode mode, bool run)
        {
            const string title = "VR Rzuty – build";
            if (!File.Exists(VRThrowingSetup.ScenePath))
            {
                EditorUtility.DisplayDialog(title, "Brak sceny. Użyj VR Rzuty › 2. Generuj scenę (kosz i kręgle).", "OK");
                return false;
            }
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                if (EditorUtility.DisplayDialog(title, "Aktywna platforma to nie Android. Przełączyć teraz (kilka minut)?", "Przełącz", "Anuluj"))
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                return false;
            }
            if (mode == BuildMode.PatchScriptsOnly && !File.Exists(ApkPath))
            {
                EditorUtility.DisplayDialog(title, "„Tylko skrypty” wymaga wcześniejszego pełnego buildu DEV.\nUżyj najpierw Build And Run albo Patch And Run.", "OK");
                return false;
            }

            int mismatch = MismatchCount(kind);
            if (mismatch > 0)
            {
                int choice = EditorUtility.DisplayDialogComplex(title,
                    $"Bieżące ustawienia różnią się od profilu {kind} w {mismatch} pozycjach (Panel kontrolny › Build i optymalizacja).\n\n" +
                    "Zastosować profil przed buildem? (Pierwszy build po zmianie profilu jest dłuższy.)",
                    "Zastosuj i buduj", "Anuluj", "Buduj bez zmian");
                if (choice == 1) return false;
                if (choice == 0) ApplyProfile(kind);
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            string path = kind == BuildProfileKind.Dev ? ApkPath : ReleaseApkPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? "Builds");
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

            var options = BuildOptions.None;
            if (kind == BuildProfileKind.Dev)
            {
                options |= BuildOptions.Development;
                if (ScriptDebugging) options |= BuildOptions.AllowDebugging;
                if (AutoconnectProfiler) options |= BuildOptions.ConnectWithProfiler;
                if (mode != BuildMode.Full) options |= BuildOptions.PatchPackage;
                if (mode == BuildMode.PatchScriptsOnly) options |= BuildOptions.BuildScriptsOnly;
            }
            options |= UseLz4HC ? BuildOptions.CompressWithLz4HC : BuildOptions.CompressWithLz4;
            if (run) options |= BuildOptions.AutoRunPlayer;

            if (run && ClearLogcatBeforeRun && Adb.Available) Adb.Run("logcat -c", out _, 4000);

            string label = mode == BuildMode.Full ? (run ? "Build And Run" : "Build") : mode == BuildMode.Patch ? "Patch And Run" : "Patch And Run (skrypty)";
            label += " [" + kind + "]";

            var sw = Stopwatch.StartNew();
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { VRThrowingSetup.ScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = options
            });
            sw.Stop();

            var summary = report.summary;
            bool ok = summary.result == BuildResult.Succeeded;
            string info = $"{DateTime.Now:HH:mm:ss}  {label}: {(ok ? "OK" : summary.result.ToString())} w {sw.Elapsed.TotalSeconds:0} s" +
                          (ok && File.Exists(path) ? $", APK {new FileInfo(path).Length / (1024f * 1024f):0.0} MB" : "");
            EditorPrefs.SetString(P + "LastInfo", info);

            if (ok)
                Debug.Log($"[VRRzuty] {info} -> {path}" + (run ? " (uruchamianie na goglach)" : ""));
            else
                Debug.LogError($"[VRRzuty] {info}. Sprawdź Console. Częste przyczyny: gogle niepodłączone/nieautoryzowane " +
                               "(Panel kontrolny › Gogle), brak Android Build Support, błędy w Project Setup Tool.");
            return ok;
        }

        // ---------------------------------------------------------------- refleksja (API, które zmieniają się między wersjami)

        static PropertyInfo StaticProp(Type t, string name) =>
            t?.GetProperty(name, BindingFlags.Public | BindingFlags.Static);

        static string GetStatic(Type t, string name)
        {
            try
            {
                var p = StaticProp(t, name);
                return p != null ? p.GetValue(null)?.ToString() ?? "null" : "n/d";
            }
            catch
            {
                return "n/d";
            }
        }

        static bool GetStaticBool(Type t, string name)
        {
            try { return StaticProp(t, name)?.GetValue(null) is bool b && b; }
            catch { return false; }
        }

        static bool SetStatic(Type t, string name, object value)
        {
            var p = StaticProp(t, name);
            if (p == null || !p.CanWrite) return false;
            object v = value;
            if (p.PropertyType.IsEnum && value is string s)
            {
                if (!Enum.GetNames(p.PropertyType).Contains(s)) return false;
                v = Enum.Parse(p.PropertyType, s);
            }
            p.SetValue(null, v);
            return true;
        }

        static Type DebugSymbolsType => VRThrowingSetup.FindType("UnityEditor.Android.UserBuildSettings+DebugSymbols");

        static string GetDebugSymbols()
        {
            var t = DebugSymbolsType;
            string v = t != null ? GetStatic(t, "level") : GetStatic(typeof(EditorUserBuildSettings), "androidCreateSymbols");
            return v == "None" || v == "Disabled" ? "brak" : v;
        }

        static void SetDebugSymbolsNone()
        {
            var t = DebugSymbolsType;
            if (t == null || !SetStatic(t, "level", "None"))
                SetStatic(typeof(EditorUserBuildSettings), "androidCreateSymbols", "Disabled");
        }
    }

    /// <summary>Narzędzia adb (z Android SDK dołączonego do Unity): status gogli, instalacja, uruchamianie, logcat.</summary>
    public static class Adb
    {
        public static string SdkRoot()
        {
            string p = null;
            try
            {
                var t = VRThrowingSetup.FindType("UnityEditor.Android.AndroidExternalToolsSettings");
                p = t?.GetProperty("sdkRootPath", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
            }
            catch { /* brak modułu Android */ }
            if (string.IsNullOrEmpty(p)) p = EditorPrefs.GetString("AndroidSdkRoot", "");
            if (string.IsNullOrEmpty(p)) p = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "SDK");
            return p;
        }

        public static string Exe =>
            Path.Combine(SdkRoot(), "platform-tools", Application.platform == RuntimePlatform.WindowsEditor ? "adb.exe" : "adb");

        public static bool Available => File.Exists(Exe);

        public static string PackageName => PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);

        public static int Run(string args, out string output, int timeoutMs = 15000)
        {
            output = "";
            if (!Available)
            {
                output = "Nie znaleziono adb: " + Exe;
                return -1;
            }
            try
            {
                var psi = new ProcessStartInfo(Exe, args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null) { output = "Nie udało się uruchomić adb."; return -1; }
                    var stdout = p.StandardOutput.ReadToEndAsync();
                    var stderr = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(timeoutMs))
                    {
                        try { p.Kill(); } catch { /* ignore */ }
                        output = "adb: przekroczono czas (" + timeoutMs / 1000 + " s)";
                        return -1;
                    }
                    output = (stdout.Result + stderr.Result).Trim();
                    return p.ExitCode;
                }
            }
            catch (Exception e)
            {
                output = "adb: " + e.Message;
                return -1;
            }
        }

        /// <summary>Opis stanu połączenia z goglami (adb devices -l).</summary>
        public static string DeviceStatus(out bool ready)
        {
            ready = false;
            if (!Available) return "Nie znaleziono adb (" + Exe + ").\nZainstaluj moduł Android Build Support (z Android SDK & NDK Tools) w Unity Hub.";
            if (Run("devices -l", out string output, 8000) != 0) return output;

            var lines = output.Split('\n').Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("List of devices") && !l.StartsWith("*")).ToList();
            if (lines.Count == 0)
                return "Brak urządzeń. Sprawdź kabel USB-C (z transmisją danych), tryb dewelopera i założ gogle – może czekać pytanie o debugowanie USB.";

            var sb = new StringBuilder();
            foreach (var l in lines)
            {
                var parts = l.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                string serial = parts.Length > 0 ? parts[0] : "?";
                string state = parts.Length > 1 ? parts[1] : "?";
                string model = parts.FirstOrDefault(x => x.StartsWith("model:"))?.Substring(6).Replace('_', ' ') ?? "";
                if (state == "device")
                {
                    ready = true;
                    sb.AppendLine($"<color=#5BD45B>●</color> Połączono: <b>{(model.Length > 0 ? model : "urządzenie")}</b> ({serial})");
                }
                else if (state == "unauthorized")
                    sb.AppendLine($"<color=#FFB347>●</color> {serial}: brak autoryzacji – załóż gogle i potwierdź „Zezwól na debugowanie USB” (zaznacz „Zawsze”).");
                else
                    sb.AppendLine($"<color=#FF6B6B>●</color> {serial}: {state} – odłącz i podłącz kabel.");
            }
            return sb.ToString().TrimEnd();
        }

        public static void InstallLastApk()
        {
            string apk = Path.GetFullPath(VRThrowingBuild.ApkPath);
            if (!File.Exists(apk))
            {
                EditorUtility.DisplayDialog("VR Rzuty", "Brak " + VRThrowingBuild.ApkPath + " – zbuduj najpierw aplikację.", "OK");
                return;
            }
            EditorUtility.DisplayProgressBar("VR Rzuty", "Instaluję APK na goglach…", 0.5f);
            int code;
            string output;
            try { code = Run($"install -r \"{apk}\"", out output, 180000); }
            finally { EditorUtility.ClearProgressBar(); }
            Report("Instalacja", code, output);
            if (code == 0) LaunchApp();
        }

        public static void LaunchApp()
        {
            string pkg = PackageName;
            string activity = null;
            if (Run($"shell cmd package resolve-activity --brief -c android.intent.category.LAUNCHER {pkg}", out string resolved, 8000) == 0)
                activity = resolved.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.Contains("/"));

            int code;
            string output;
            if (!string.IsNullOrEmpty(activity)) code = Run($"shell am start -n {activity}", out output, 10000);
            else code = Run($"shell monkey -p {pkg} 1", out output, 10000);
            Report("Uruchomienie " + pkg, code, output);
        }

        public static void StopApp() => Report("Zatrzymanie", Run($"shell am force-stop {PackageName}", out string o, 8000), o);

        public static void Uninstall()
        {
            if (!EditorUtility.DisplayDialog("VR Rzuty", $"Odinstalować {PackageName} z gogli? (usuwa też zapisane ustawienia i wyniki)", "Odinstaluj", "Anuluj")) return;
            Report("Odinstalowanie", Run($"uninstall {PackageName}", out string o, 30000), o);
        }

        public static void ClearLogcat() => Report("Czyszczenie logcat", Run("logcat -c", out string o, 5000), o);

        /// <summary>Zapisuje logi Unity z gogli do Logs/quest_logcat.txt i otwiera plik.</summary>
        public static void DumpLogcat()
        {
            int code = Run("logcat -d -v time Unity:V AndroidRuntime:E DEBUG:V libc:F CRASH:V *:S", out string output, 15000);
            if (code != 0)
            {
                Report("Logcat", code, output);
                return;
            }
            Directory.CreateDirectory("Logs");
            string file = Path.GetFullPath("Logs/quest_logcat.txt");
            File.WriteAllText(file, output, Encoding.UTF8);
            EditorUtility.OpenWithDefaultApp(file);
            Debug.Log("[VRRzuty] Logcat zapisany: " + file);
        }

        public static void OpenAndroidLogcatWindow()
        {
            if (!EditorApplication.ExecuteMenuItem("Window/Analysis/Android Logcat"))
                EditorUtility.DisplayDialog("VR Rzuty",
                    "Pakiet Android Logcat nie jest zainstalowany.\n\nWindow › Package Manager › Unity Registry › „Android Logcat” › Install.\n" +
                    "Albo użyj przycisku „Pobierz log Unity z gogli”.", "OK");
        }

        static void Report(string what, int code, string output)
        {
            if (code == 0) Debug.Log($"[VRRzuty] {what}: OK\n{output}");
            else
            {
                Debug.LogWarning($"[VRRzuty] {what}: błąd ({code})\n{output}");
                EditorUtility.DisplayDialog("VR Rzuty", $"{what}: błąd\n\n{output}", "OK");
            }
        }
    }
}
