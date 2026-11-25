using System;
using UnityEngine;

namespace VRThrowing
{
    /// <summary>Skąd brana jest prędkość piłki w chwili puszczenia.</summary>
    public enum VelocitySource { OvrInput, History, LastFrame }

    /// <summary>Stanowisko, przy którym stoi gracz.</summary>
    public enum Station { Basketball, Bowling }

    /// <summary>
    /// Wszystkie ustawienia rzutów w jednym miejscu. Menu w goglach (ThrowMenu) zmienia je przez <see cref="Step"/>,
    /// a pozostałe skrypty tylko je odczytują (i reagują na zdarzenie <see cref="Changed"/>).
    /// Ustawienia są zapamiętywane w goglach (PlayerPrefs).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ThrowSettings : MonoBehaviour
    {
        public static ThrowSettings Instance { get; private set; }

        public static readonly float[] Multipliers = { 1f, 1.25f, 1.5f, 2f };
        public static readonly float[] AssistLevels = { 0f, 0.3f, 0.7f };
        public static readonly float[] Gravities = { 9.81f, 3.71f, 1.62f };
        public static readonly string[] GravityNames = { "Ziemia 9,81", "Mars 3,71", "Księżyc 1,62" };
        public static readonly float[] HoopHeights = { 3.05f, 2.6f, 2.0f };
        public static readonly float[] ShotDistances = { 2f, 4.2f, 6.75f };
        public static readonly string[] ShotDistanceNames = { "2,0 m (blisko)", "4,2 m (wolny)", "6,75 m (za 3)" };
        public static readonly float[] LaneLengths = { 18.29f, 12f, 8f };

        [Header("Rzut")]
        public VelocitySource velocitySource = VelocitySource.OvrInput;
        [Range(0, 3)] public int multiplierIndex;
        [Range(0, 2)] public int assistIndex;
        public bool trajectoryPreview;
        [Tooltip("Przekazuj piłce obrót kontrolera (spin).")]
        public bool spin = true;
        [Tooltip("Chwytanie: false = trzymaj grip, true = grip przełącza (kliknij, żeby złapać, kliknij, żeby puścić).")]
        public bool toggleGrip;
        [Range(0, 2)] public int gravityIndex;

        [Header("Kosz")]
        [Range(0, 2)] public int hoopHeightIndex;
        [Range(0, 2)] public int distanceIndex = 1;

        [Header("Kręgle")]
        [Range(0, 2)] public int laneLengthIndex;
        public bool bumpers;

        [Header("Stan")]
        public Station station = Station.Basketball;
        public bool persist = true;

        public event Action Changed;

        public float Multiplier => Multipliers[Mathf.Clamp(multiplierIndex, 0, Multipliers.Length - 1)];
        public float Assist => AssistLevels[Mathf.Clamp(assistIndex, 0, AssistLevels.Length - 1)];
        public float Gravity => Gravities[Mathf.Clamp(gravityIndex, 0, Gravities.Length - 1)];
        public float HoopHeight => HoopHeights[Mathf.Clamp(hoopHeightIndex, 0, HoopHeights.Length - 1)];
        public float ShotDistance => ShotDistances[Mathf.Clamp(distanceIndex, 0, ShotDistances.Length - 1)];
        public float LaneLength => LaneLengths[Mathf.Clamp(laneLengthIndex, 0, LaneLengths.Length - 1)];

        // ------------------------------------------------------------------ opcje menu

        public const int OptSource = 0, OptMultiplier = 1, OptAssist = 2, OptPreview = 3, OptSpin = 4, OptGrip = 5,
            OptGravity = 6, OptHoopHeight = 7, OptDistance = 8, OptLaneLength = 9, OptBumpers = 10;

        public const int OptionCount = 11;

        static readonly string[] OptionNames =
        {
            "Źródło prędkości", "Siła rzutu", "Pomoc w celowaniu", "Podgląd toru lotu", "Rotacja (spin)", "Chwytanie",
            "Grawitacja", "Wysokość kosza", "Odległość od kosza", "Długość toru", "Bandy na torze"
        };

        static readonly string[] OptionHints =
        {
            "OVRInput: prędkość liczona przez system śledzenia gogli (najdokładniejsza). Historia: średnia z ruchu piłki w ostatnich 0,1 s. Ostatnia klatka: różnica pozycji z dwóch klatek - zwykle za mała albo „szarpana”. Porównaj na wyświetlaczu nad lewym kontrolerem.",
            "Mnożnik prędkości wyrzutu. W VR rzuty często „wydają się” słabsze, bo piłka nic nie waży - wiele gier lekko je wzmacnia.",
            "Delikatnie poprawia kierunek lotu w stronę obręczy (lub środka toru). Słaba: 30%, silna: 70% korekty. Działa tylko, gdy rzucasz mniej więcej w cel.",
            "Pokazuje przewidywany tor lotu piłki, zanim ją puścisz - liczony z bieżącej prędkości ręki.",
            "Obrót kontrolera w chwili puszczenia nadaje piłce rotację (podkręcenie). Bez spinu piłka leci „martwa”.",
            "Przytrzymaj: trzymasz grip, puszczasz grip = rzut. Przełącz: jedno kliknięcie łapie, drugie puszcza (wygodniejsze przy długim trzymaniu).",
            "Zmienia przyspieszenie ziemskie w całej scenie. Na Księżycu piłka leci wolniej i wyżej - zobacz, jak zmienia się tor lotu.",
            "Regulaminowa wysokość obręczy to 3,05 m. Niżej łatwiej trafić.",
            "Odległość od środka obręczy: 4,2 m - linia rzutów wolnych, 6,75 m - linia za 3 punkty.",
            "Regulaminowy tor ma 18,29 m od linii spalonego do pierwszego kręgla. Krótszy tor ułatwia celowanie.",
            "Bandy zamykają rynny - kula nie wypadnie z toru (jak na torach dla dzieci)."
        };

        public static readonly string[] PresetNames = { "Realistyczny", "Trening", "Łatwy" };

        static readonly string[] PresetHints =
        {
            "Realistyczny: prędkość z OVRInput, siła ×1, bez pomocy i podglądu, spin, Ziemia, regulaminowy kosz i tor.",
            "Trening: podgląd toru lotu i słaba pomoc w celowaniu, siła ×1,25 - dobre do nauki rzutu.",
            "Łatwy: silna pomoc, siła ×1,5, niższy kosz z bliska, krótki tor z bandami."
        };

        public static string OptionName(int i) => i >= 0 && i < OptionCount ? OptionNames[i] : "?";
        public static string OptionHint(int i) => i >= 0 && i < OptionCount ? OptionHints[i] : "";
        public static string PresetHint(int i) => i >= 0 && i < PresetHints.Length ? PresetHints[i] : "";

        public string OptionValue(int i)
        {
            switch (i)
            {
                case OptSource: return Name(velocitySource);
                case OptMultiplier: return "×" + Multiplier.ToString("0.##").Replace('.', ',');
                case OptAssist: return assistIndex == 0 ? "wył." : assistIndex == 1 ? "słaba" : "silna";
                case OptPreview: return trajectoryPreview ? "wł." : "wył.";
                case OptSpin: return spin ? "wł." : "wył.";
                case OptGrip: return toggleGrip ? "przełącz" : "przytrzymaj";
                case OptGravity: return GravityNames[Mathf.Clamp(gravityIndex, 0, GravityNames.Length - 1)];
                case OptHoopHeight: return HoopHeight.ToString("0.00").Replace('.', ',') + " m";
                case OptDistance: return ShotDistanceNames[Mathf.Clamp(distanceIndex, 0, ShotDistanceNames.Length - 1)];
                case OptLaneLength: return LaneLength.ToString("0.##").Replace('.', ',') + " m";
                case OptBumpers: return bumpers ? "wł." : "wył.";
                default: return "?";
            }
        }

        /// <summary>Opcje kosza są wyszarzone przy kręglach i odwrotnie.</summary>
        public bool OptionRelevant(int i)
        {
            switch (i)
            {
                case OptHoopHeight:
                case OptDistance: return station == Station.Basketball;
                case OptLaneLength:
                case OptBumpers: return station == Station.Bowling;
                default: return true;
            }
        }

        public void Step(int i, int delta)
        {
            switch (i)
            {
                case OptSource: velocitySource = (VelocitySource)Wrap((int)velocitySource + delta, 3); break;
                case OptMultiplier: multiplierIndex = Wrap(multiplierIndex + delta, Multipliers.Length); break;
                case OptAssist: assistIndex = Wrap(assistIndex + delta, AssistLevels.Length); break;
                case OptPreview: trajectoryPreview = !trajectoryPreview; break;
                case OptSpin: spin = !spin; break;
                case OptGrip: toggleGrip = !toggleGrip; break;
                case OptGravity: gravityIndex = Wrap(gravityIndex + delta, Gravities.Length); break;
                case OptHoopHeight: hoopHeightIndex = Wrap(hoopHeightIndex + delta, HoopHeights.Length); break;
                case OptDistance: distanceIndex = Wrap(distanceIndex + delta, ShotDistances.Length); break;
                case OptLaneLength: laneLengthIndex = Wrap(laneLengthIndex + delta, LaneLengths.Length); break;
                case OptBumpers: bumpers = !bumpers; break;
                default: return;
            }
            NotifyChanged();
        }

        public void ApplyPreset(int preset)
        {
            switch (preset)
            {
                case 0: // realistyczny
                    velocitySource = VelocitySource.OvrInput;
                    multiplierIndex = 0;
                    assistIndex = 0;
                    trajectoryPreview = false;
                    spin = true;
                    gravityIndex = 0;
                    hoopHeightIndex = 0;
                    distanceIndex = 1;
                    laneLengthIndex = 0;
                    bumpers = false;
                    break;
                case 1: // trening
                    velocitySource = VelocitySource.OvrInput;
                    multiplierIndex = 1;
                    assistIndex = 1;
                    trajectoryPreview = true;
                    spin = true;
                    gravityIndex = 0;
                    break;
                default: // łatwy
                    velocitySource = VelocitySource.OvrInput;
                    multiplierIndex = 2;
                    assistIndex = 2;
                    trajectoryPreview = true;
                    gravityIndex = 0;
                    hoopHeightIndex = 1;
                    distanceIndex = 0;
                    laneLengthIndex = 2;
                    bumpers = true;
                    break;
            }
            NotifyChanged();
        }

        /// <summary>Krótki opis konfiguracji rzutu (HUD, tablice wyników).</summary>
        public string ShortSummary()
        {
            string s = Name(velocitySource) + " " + OptionValue(OptMultiplier);
            if (assistIndex > 0) s += ", pomoc " + OptionValue(OptAssist);
            if (gravityIndex > 0) s += ", " + GravityNames[gravityIndex].Split(' ')[0];
            return s;
        }

        public static string Name(VelocitySource v) =>
            v == VelocitySource.OvrInput ? "OVRInput" : v == VelocitySource.History ? "historia 0,1 s" : "ostatnia klatka";

        public static string Name(Station s) => s == Station.Basketball ? "Kosz" : "Kręgle";

        // ------------------------------------------------------------------ cykl życia i zapis

        void Awake()
        {
            Instance = this;
            if (persist) Load();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void NotifyChanged()
        {
            if (persist) Save();
            if (Changed != null) Changed();
        }

        const string P = "rzuty.v1.";

        void Load()
        {
            if (!PlayerPrefs.HasKey(P + "source")) return;
            velocitySource = (VelocitySource)Wrap(PlayerPrefs.GetInt(P + "source", 0), 3);
            multiplierIndex = Wrap(PlayerPrefs.GetInt(P + "mult", multiplierIndex), Multipliers.Length);
            assistIndex = Wrap(PlayerPrefs.GetInt(P + "assist", assistIndex), AssistLevels.Length);
            trajectoryPreview = PlayerPrefs.GetInt(P + "preview", trajectoryPreview ? 1 : 0) == 1;
            spin = PlayerPrefs.GetInt(P + "spin", spin ? 1 : 0) == 1;
            toggleGrip = PlayerPrefs.GetInt(P + "toggleGrip", toggleGrip ? 1 : 0) == 1;
            gravityIndex = Wrap(PlayerPrefs.GetInt(P + "gravity", gravityIndex), Gravities.Length);
            hoopHeightIndex = Wrap(PlayerPrefs.GetInt(P + "hoop", hoopHeightIndex), HoopHeights.Length);
            distanceIndex = Wrap(PlayerPrefs.GetInt(P + "distance", distanceIndex), ShotDistances.Length);
            laneLengthIndex = Wrap(PlayerPrefs.GetInt(P + "lane", laneLengthIndex), LaneLengths.Length);
            bumpers = PlayerPrefs.GetInt(P + "bumpers", bumpers ? 1 : 0) == 1;
        }

        void Save()
        {
            PlayerPrefs.SetInt(P + "source", (int)velocitySource);
            PlayerPrefs.SetInt(P + "mult", multiplierIndex);
            PlayerPrefs.SetInt(P + "assist", assistIndex);
            PlayerPrefs.SetInt(P + "preview", trajectoryPreview ? 1 : 0);
            PlayerPrefs.SetInt(P + "spin", spin ? 1 : 0);
            PlayerPrefs.SetInt(P + "toggleGrip", toggleGrip ? 1 : 0);
            PlayerPrefs.SetInt(P + "gravity", gravityIndex);
            PlayerPrefs.SetInt(P + "hoop", hoopHeightIndex);
            PlayerPrefs.SetInt(P + "distance", distanceIndex);
            PlayerPrefs.SetInt(P + "lane", laneLengthIndex);
            PlayerPrefs.SetInt(P + "bumpers", bumpers ? 1 : 0);
            PlayerPrefs.Save();
        }

        static int Wrap(int v, int n) => ((v % n) + n) % n;
    }
}
