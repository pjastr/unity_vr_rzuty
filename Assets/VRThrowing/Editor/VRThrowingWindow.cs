using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VRThrowing.EditorTools
{
    /// <summary>
    /// Panel kontrolny (VR Rzuty › Panel kontrolny) z trzema zakładkami:
    ///  1. Kontrola projektu   - lista kontrolna z przyciskami naprawy (+ jedyne kroki ręczne),
    ///  2. Build i optymalizacja - profile DEV / RELEASE, Build/Patch And Run, ostatni czas buildu,
    ///  3. Gogle (USB/ADB)     - stan połączenia, instalacja, uruchamianie, logcat.
    /// </summary>
    public class VRThrowingWindow : EditorWindow
    {
        static readonly string[] Tabs = { "1. Kontrola projektu", "2. Build i optymalizacja", "3. Gogle (USB / ADB)" };
        const string TabKey = "VRRzuty.Window.Tab";

        int tab;
        Vector2 scroll;
        List<VRThrowingSetup.CheckItem> checks = new List<VRThrowingSetup.CheckItem>();
        string deviceStatus = "(kliknij „Sprawdź połączenie”)";
        GUIStyle rich, richBold, box, header;

        public static void Open()
        {
            var w = GetWindow<VRThrowingWindow>("VR Rzuty");
            w.minSize = new Vector2(560f, 480f);
            w.Show();
        }

        void OnEnable()
        {
            tab = EditorPrefs.GetInt(TabKey, 0);
            Refresh();
        }

        void OnFocus() => Refresh();

        void Refresh()
        {
            try { checks = VRThrowingSetup.GetChecklist(); }
            catch (Exception e) { Debug.LogWarning("[VRRzuty] Lista kontrolna: " + e.Message); }
            Repaint();
        }

        /// <summary>Akcje uruchamiamy poza OnGUI (build, przełączanie platformy), potem odświeżamy panel.</summary>
        void Later(Action action)
        {
            EditorApplication.delayCall += () =>
            {
                try { action(); }
                catch (Exception e) { Debug.LogException(e); }
                if (this != null) Refresh();
            };
        }

        void InitStyles()
        {
            if (rich != null) return;
            rich = new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true };
            richBold = new GUIStyle(rich) { fontStyle = FontStyle.Bold };
            box = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(8, 8, 6, 6) };
            header = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
        }

        void OnGUI()
        {
            InitStyles();
            EditorGUILayout.Space(4);
            int newTab = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(26));
            if (newTab != tab)
            {
                tab = newTab;
                EditorPrefs.SetInt(TabKey, tab);
                Refresh();
            }
            EditorGUILayout.Space(4);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            switch (tab)
            {
                case 0: DrawChecks(); break;
                case 1: DrawBuild(); break;
                default: DrawDevice(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        // =====================================================================
        // 1. KONTROLA PROJEKTU
        // =====================================================================

        void DrawChecks()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Szybki start (wszystko)", GUILayout.Height(28))) Later(VRThrowingSetup.QuickStart);
            if (GUILayout.Button("Konfiguruj projekt", GUILayout.Height(28))) Later(VRThrowingSetup.ConfigureProjectMenu);
            if (GUILayout.Button("Generuj scenę", GUILayout.Height(28))) Later(VRThrowingSetup.GenerateSceneMenu);
            if (GUILayout.Button("Odśwież", GUILayout.Height(28), GUILayout.Width(80))) Refresh();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(6);

            int bad = 0;
            foreach (var c in checks) if (!c.ok && !c.manual) bad++;
            EditorGUILayout.LabelField(bad == 0
                ? "<color=#5BD45B><b>Wszystkie automatyczne punkty OK.</b></color> Zostaje jednorazowo Project Setup Tool (niżej)."
                : $"<color=#FFB347><b>Do poprawy: {bad}</b></color> – kliknij przycisk przy punkcie albo „Szybki start”.", rich);
            EditorGUILayout.Space(4);

            foreach (var c in checks)
            {
                EditorGUILayout.BeginVertical(box);
                EditorGUILayout.BeginHorizontal();
                string status = c.manual ? "<color=#7FB2FF>[RĘCZNIE]</color>" : c.ok ? "<color=#5BD45B>[OK]</color>" : "<color=#FF6B6B>[!!]</color>";
                EditorGUILayout.LabelField(status + "  " + c.title, richBold);
                if ((!c.ok || c.manual) && c.fix != null && GUILayout.Button(c.fixLabel ?? "Napraw", GUILayout.Width(130)))
                    Later(c.fix);
                if (c.extra != null && GUILayout.Button(c.extraLabel ?? "Więcej", GUILayout.Width(130)))
                    Later(c.extra);
                EditorGUILayout.EndHorizontal();
                if (!string.IsNullOrEmpty(c.detail)) EditorGUILayout.LabelField(c.detail, rich);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Narzędzia", header);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Project Setup Tool")) Later(VRThrowingSetup.OpenProjectSetupTool);
            if (GUILayout.Button("XR Plug-in Management")) Later(VRThrowingSetup.OpenXRSettingsWindow);
            if (GUILayout.Button("Building Blocks")) Later(VRThrowingSetup.OpenBuildingBlocks);
            if (GUILayout.Button("Instrukcja")) Later(VRThrowingSetup.OpenDocs);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Kolejność przy pierwszym uruchomieniu:\n" +
                "1) Szybki start  →  2) przełączenie na Android  →  3) Project Setup Tool: Fix All + Apply All\n" +
                "4) zakładka 3: sprawdź połączenie z goglami  →  5) zakładka 2: Build And Run.\n\n" +
                "Building Blocks nie są wymagane – generator dodaje OVRCameraRig z Meta XR Core SDK. " +
                "Jeśli wolisz blok „Camera Rig”, usuń OVRCameraRig ze sceny, dodaj blok i wygeneruj scenę ponownie (rig zostanie zachowany). " +
                "Bloku „Controller Tracking” nie dodawaj – kontrolery mają modele z prymitywów.",
                MessageType.Info);
        }

        // =====================================================================
        // 2. BUILD I OPTYMALIZACJA
        // =====================================================================

        void DrawBuild()
        {
            EditorGUILayout.LabelField("Profile ustawień", header);
            EditorGUILayout.LabelField(
                "<b>DEV</b> – najkrótszy czas Build & Run i debugowanie przez USB.   " +
                "<b>RELEASE</b> – szybkie ładowanie i pełna wydajność (do pokazania / dłuższych testów).", rich);
            EditorGUILayout.Space(4);

            var rows = VRThrowingBuild.DescribeSettings();
            int devMiss = 0, relMiss = 0;
            foreach (var r in rows)
            {
                if (!r.MatchesDev) devMiss++;
                if (!r.MatchesRelease) relMiss++;
            }

            // Nagłówek tabeli
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Ustawienie", EditorStyles.miniBoldLabel, GUILayout.Width(200));
            GUILayout.Label("Obecnie", EditorStyles.miniBoldLabel, GUILayout.Width(110));
            GUILayout.Label("DEV", EditorStyles.miniBoldLabel, GUILayout.Width(95));
            GUILayout.Label("RELEASE", EditorStyles.miniBoldLabel, GUILayout.Width(95));
            GUILayout.Label("Po co", EditorStyles.miniBoldLabel);
            EditorGUILayout.EndHorizontal();

            foreach (var r in rows)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(r.name, GUILayout.Width(200));
                string color = r.MatchesDev && r.MatchesRelease ? "#5BD45B" : r.MatchesDev ? "#6FC3FF" : r.MatchesRelease ? "#D9A6FF" : "#FFB347";
                GUILayout.Label($"<color={color}><b>{r.current}</b></color>", rich, GUILayout.Width(110));
                GUILayout.Label(r.dev, GUILayout.Width(95));
                GUILayout.Label(r.release, GUILayout.Width(95));
                GUILayout.Label($"<size=10><color=#999>{r.hint}</color></size>", rich);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField(
                "<size=10><color=#6FC3FF>niebieski</color> = jak DEV, <color=#D9A6FF>fioletowy</color> = jak RELEASE, " +
                "<color=#5BD45B>zielony</color> = wspólne, <color=#FFB347>pomarańczowy</color> = inne niż oba profile</size>", rich);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(devMiss == 0 ? "Profil DEV aktywny ✓" : $"Zastosuj profil DEV ({devMiss} zmian)", GUILayout.Height(26)))
                Later(() => VRThrowingBuild.ApplyProfile(BuildProfileKind.Dev));
            if (GUILayout.Button(relMiss == 0 ? "Profil RELEASE aktywny ✓" : $"Zastosuj profil RELEASE ({relMiss} zmian)", GUILayout.Height(26)))
                Later(() => VRThrowingBuild.ApplyProfile(BuildProfileKind.Release));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUI.BeginChangeCheck();
            bool dbg = EditorGUILayout.ToggleLeft("Script Debugging w profilu DEV (debugger C# przez USB; nieco dłuższy build)", VRThrowingBuild.ScriptDebugging);
            bool prof = EditorGUILayout.ToggleLeft("Autoconnect Profiler w profilu DEV", VRThrowingBuild.AutoconnectProfiler);
            bool clr = EditorGUILayout.ToggleLeft("Czyść logcat przed uruchomieniem (łatwiej znaleźć logi bieżącej sesji)", VRThrowingBuild.ClearLogcatBeforeRun);
            if (EditorGUI.EndChangeCheck())
            {
                VRThrowingBuild.ScriptDebugging = dbg;
                VRThrowingBuild.AutoconnectProfiler = prof;
                VRThrowingBuild.ClearLogcatBeforeRun = clr;
                if (EditorUserBuildSettings.development)
                {
                    EditorUserBuildSettings.allowDebugging = dbg;
                    EditorUserBuildSettings.connectProfiler = prof;
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Uruchamianie na goglach (USB)", header);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Build And Run\n<size=10>DEV, pełny build</size>", RichButton(), GUILayout.Height(44)))
                Later(VRThrowingBuild.BuildAndRun);
            if (GUILayout.Button("Patch And Run\n<size=10>DEV, tylko zmienione pliki</size>", RichButton(), GUILayout.Height(44)))
                Later(() => VRThrowingBuild.PatchAndRun(false));
            if (GUILayout.Button("Patch And Run – skrypty\n<size=10>gdy zmieniasz tylko kod C#</size>", RichButton(), GUILayout.Height(44)))
                Later(() => VRThrowingBuild.PatchAndRun(true));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Build And Run – RELEASE")) Later(() => VRThrowingBuild.BuildRelease(true));
            if (GUILayout.Button("Tylko APK – RELEASE")) Later(() => VRThrowingBuild.BuildRelease(false));
            if (GUILayout.Button("Otwórz folder Builds"))
            {
                Directory.CreateDirectory("Builds");
                EditorUtility.RevealInFinder(Path.GetFullPath("Builds"));
            }
            EditorGUILayout.EndHorizontal();

            string last = VRThrowingBuild.LastBuildInfo;
            EditorGUILayout.LabelField("Ostatni build: " + (string.IsNullOrEmpty(last) ? "–" : last), rich);

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Jak skrócić pętlę zmiana → test w goglach:\n" +
                "• Pierwszy build jest zawsze najdłuższy (IL2CPP + Gradle). Kolejne korzystają z cache w Library/Bee – nie usuwaj folderu Library.\n" +
                "• Patch And Run wysyła na gogle tylko zmienione pliki zamiast całego APK. Pierwszy patch przygotowuje środowisko i trwa dłużej.\n" +
                "• „Patch And Run – skrypty” pomija przetwarzanie scen i assetów – używaj, gdy zmieniasz tylko kod.\n" +
                "• Zmiana profilu (DEV ↔ RELEASE) wymusza ponowną kompilację IL2CPP – nie przełączaj bez potrzeby.\n" +
                "• Szybkie ładowanie aplikacji: brak splash screena, scena z prymitywów bez tekstur, URP bez cieni i HDR (mniej wariantów shaderów), ASTC.",
                MessageType.None);
        }

        GUIStyle richButton;
        GUIStyle RichButton()
        {
            if (richButton == null) richButton = new GUIStyle(GUI.skin.button) { richText = true };
            return richButton;
        }

        // =====================================================================
        // 3. GOGLE (ADB)
        // =====================================================================

        void DrawDevice()
        {
            EditorGUILayout.LabelField("Połączenie z Quest 3S", header);
            EditorGUILayout.BeginVertical(box);
            EditorGUILayout.LabelField(deviceStatus, rich);
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Sprawdź połączenie", GUILayout.Height(26)))
                deviceStatus = Adb.DeviceStatus(out _);

            EditorGUILayout.LabelField($"<size=10><color=#999>adb: {Adb.Exe}\nPakiet: {Adb.PackageName}</color></size>", rich);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Aplikacja na goglach", header);
            using (new EditorGUI.DisabledScope(!Adb.Available))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Zainstaluj ostatni APK\n<size=10>bez budowania</size>", RichButton(), GUILayout.Height(40))) Later(Adb.InstallLastApk);
                if (GUILayout.Button("Uruchom", GUILayout.Height(40))) Later(Adb.LaunchApp);
                if (GUILayout.Button("Zatrzymaj", GUILayout.Height(40))) Later(Adb.StopApp);
                if (GUILayout.Button("Odinstaluj", GUILayout.Height(40))) Later(Adb.Uninstall);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("Debugowanie", header);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Pobierz log Unity z gogli")) Later(Adb.DumpLogcat);
                if (GUILayout.Button("Wyczyść logcat")) Later(Adb.ClearLogcat);
                if (GUILayout.Button("Okno Android Logcat")) Later(Adb.OpenAndroidLogcatWindow);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Przygotowanie gogli (jednorazowo):\n" +
                "1. Aplikacja Meta Horizon na telefonie › Urządzenia › gogle › Ustawienia gogli › Tryb dewelopera: włącz.\n" +
                "2. Podłącz Quest kablem USB-C (z transmisją danych) i załóż gogle – potwierdź „Zezwolić na debugowanie USB?” (zaznacz „Zawsze”).\n" +
                "3. „Sprawdź połączenie” powinno pokazać zielony punkt.\n\n" +
                "Debugowanie: profil DEV daje Development Build + Script Debugging. Logi Debug.Log widać w Console (wybierz gogle z listy " +
                "połączeń w oknie Console), w oknie Android Logcat albo w pliku z „Pobierz log Unity z gogli”. Debugger C# (Rider / Visual Studio): " +
                "„Attach to Unity Process” i wybierz urządzenie Android.",
                MessageType.Info);
        }
    }
}
