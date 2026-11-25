using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace VRThrowing
{
    /// <summary>
    /// Stanowisko koszykówki: wykrywanie trafień, tryby Trening i Wyzwanie 60 s, tablica wyników,
    /// zmiana wysokości kosza i odległości rzutu z ustawień.
    ///
    /// Trafienie jest wykrywane geometrycznie w dwóch etapach (bez triggerów):
    ///  1) środek piłki przecina w dół płaszczyznę obręczy w odległości mniejszej niż promień obręczy,
    ///  2) w ciągu 1 s przecina płaszczyznę 35 cm niżej, nadal pod obręczą (piłka nie odbiła się w górę).
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class BasketballGame : MonoBehaviour
    {
        public static BasketballGame Instance { get; private set; }

        [Header("Kosz (ustawia generator sceny)")]
        [Tooltip("Obiekt przesuwany w pionie przy zmianie wysokości kosza.")]
        public Transform hoopRoot;
        [Tooltip("Środek obręczy.")]
        public Transform rimCenter;
        public float rimRadius = 0.2286f;
        [Tooltip("Wysokość obręczy, dla której zbudowano scenę.")]
        public float builtHeight = 3.05f;

        [Header("Stanowisko")]
        [Tooltip("Obiekt (stojak + znacznik) przesuwany przy zmianie odległości.")]
        public Transform standRoot;
        public Transform standPoint;
        public Throwable[] balls;

        [Header("Tablica")]
        public Text scoreText;
        public float challengeSeconds = 60f;
        public float threePointDistance = 6.6f;

        public bool ChallengeRunning { get; private set; }

        class Track
        {
            public Vector3 prev;
            public bool has;
            public float candidateTime = -1f;
            public bool shotPending;
            public bool scored;
        }

        readonly Dictionary<Throwable, Track> tracks = new Dictionary<Throwable, Track>();
        int score, shots, makes, streak, bestStreak, swishes;
        float timeLeft;
        int best;
        const string BestKey = "rzuty.v1.basket.best60";

        public Vector3 AimPoint => rimCenter != null ? rimCenter.position : Vector3.zero;

        void Awake()
        {
            Instance = this;
            best = PlayerPrefs.GetInt(BestKey, 0);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (ThrowSettings.Instance != null) ThrowSettings.Instance.Changed -= ApplySettings;
        }

        void Start()
        {
            AimAssist.BasketTarget = rimCenter;
            foreach (var b in balls)
            {
                if (b == null) continue;
                tracks[b] = new Track();
                b.Thrown += OnThrown;
                b.Returned += OnBallBack;
                b.Grabbed += OnBallBack;
            }
            if (ThrowSettings.Instance != null) ThrowSettings.Instance.Changed += ApplySettings;
            ApplySettings();
            Refresh();
        }

        float appliedHeight = -1f, appliedDistance = -1f;

        void ApplySettings()
        {
            var s = ThrowSettings.Instance;
            if (s == null) return;

            bool changed = false;
            if (!Mathf.Approximately(appliedHeight, s.HoopHeight) && hoopRoot != null)
            {
                appliedHeight = s.HoopHeight;
                Vector3 p = hoopRoot.localPosition;
                hoopRoot.localPosition = new Vector3(p.x, s.HoopHeight - builtHeight, p.z);
                changed = true;
            }
            if (!Mathf.Approximately(appliedDistance, s.ShotDistance) && standRoot != null && rimCenter != null)
            {
                appliedDistance = s.ShotDistance;
                Vector3 p = standRoot.position;
                standRoot.position = new Vector3(p.x, p.y, rimCenter.position.z - s.ShotDistance);
                changed = true;
            }
            if (!changed) return;

            ReturnAll();
            var mover = PlayerMover.Instance;
            if (mover != null && mover.Current == Station.Basketball && Time.timeSinceLevelLoad > 0.5f) mover.Place(standPoint, true);
        }

        public void ReturnAll()
        {
            foreach (var b in balls)
            {
                if (b == null) continue;
                if (b.State == BallState.Free) b.ReturnHome();
                else b.Redock();
            }
        }

        // ------------------------------------------------------------------ tryby

        public void ToggleChallenge()
        {
            if (ChallengeRunning)
            {
                ChallengeRunning = false;
                Toast.Show("Wyzwanie przerwane. Tryb treningowy.");
            }
            else
            {
                ChallengeRunning = true;
                timeLeft = challengeSeconds;
                ResetStats();
                ReturnAll();
                Sfx.Play(Sfx.Buzzer, 0.5f);
                Toast.Show("<b>WYZWANIE " + Mathf.RoundToInt(challengeSeconds) + " s!</b>\nIle punktów zdobędziesz?", 2.5f);
            }
            Refresh();
        }

        public void ResetStats()
        {
            score = shots = makes = streak = bestStreak = swishes = 0;
            Refresh();
        }

        void Update()
        {
            if (!ChallengeRunning) return;
            float before = timeLeft;
            timeLeft -= Time.deltaTime;
            if (Mathf.CeilToInt(before) != Mathf.CeilToInt(timeLeft) && timeLeft <= 5f && timeLeft > 0f) Sfx.Play(Sfx.Click, 0.6f);
            if (timeLeft <= 0f)
            {
                timeLeft = 0f;
                ChallengeRunning = false;
                Sfx.Play(Sfx.Buzzer, 0.8f);
                bool record = score > best;
                if (record)
                {
                    best = score;
                    PlayerPrefs.SetInt(BestKey, best);
                    PlayerPrefs.Save();
                }
                Toast.Show("<b>KONIEC CZASU!</b>  " + score + " pkt" + (record ? "  – <color=#FFD54A>REKORD!</color>" : "") +
                           "\nTrafienia: " + makes + "/" + shots + "   B = jeszcze raz", 5f);
            }
            Refresh();
        }

        // ------------------------------------------------------------------ trafienia

        void OnThrown(Throwable b)
        {
            if (b.ReleaseVelocity.magnitude < 1f) return; // odłożenie, nie rzut
            shots++;
            Track t;
            if (tracks.TryGetValue(b, out t))
            {
                t.shotPending = true;
                t.scored = false;
            }
            Refresh();
        }

        /// <summary>Piłka wróciła na stojak albo została złapana: rzut bez trafienia przerywa serię.</summary>
        void OnBallBack(Throwable b)
        {
            Track t;
            if (!tracks.TryGetValue(b, out t) || !t.shotPending) return;
            if (!t.scored) streak = 0;
            t.shotPending = false;
            Refresh();
        }

        void FixedUpdate()
        {
            if (rimCenter == null) return;
            Vector3 c = rimCenter.position;
            foreach (var kv in tracks)
            {
                Throwable b = kv.Key;
                Track t = kv.Value;
                Vector3 p = b.transform.position;
                if (b.State != BallState.Free)
                {
                    t.has = false;
                    t.candidateTime = -1f;
                    continue;
                }
                if (t.has)
                {
                    // etap 1: przejście w dół przez płaszczyznę obręczy wewnątrz obręczy
                    if (t.prev.y >= c.y && p.y < c.y && HorizontalDistance(Lerp(t.prev, p, c.y), c) < rimRadius)
                        t.candidateTime = Time.time;

                    // etap 2: 35 cm niżej, nadal pod obręczą
                    float y2 = c.y - 0.35f;
                    if (t.candidateTime > 0f && t.prev.y >= y2 && p.y < y2)
                    {
                        if (Time.time - t.candidateTime < 1f && HorizontalDistance(Lerp(t.prev, p, y2), c) < rimRadius + 0.05f) Scored(b);
                        t.candidateTime = -1f;
                    }
                    if (t.candidateTime > 0f && Time.time - t.candidateTime > 1f) t.candidateTime = -1f;
                }
                t.prev = p;
                t.has = true;
            }
        }

        static Vector3 Lerp(Vector3 a, Vector3 b, float y)
        {
            float k = Mathf.Abs(a.y - b.y) < 1e-5f ? 0f : (a.y - y) / (a.y - b.y);
            return Vector3.Lerp(a, b, k);
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        void Scored(Throwable b)
        {
            Vector3 c = rimCenter.position;
            Vector3 from = b.ReleaseHeadPosition;
            float dist = HorizontalDistance(from, c);
            int points = dist >= threePointDistance ? 3 : 2;
            bool swish = !b.TouchedRim && !b.TouchedBoard;

            Track tr;
            if (tracks.TryGetValue(b, out tr)) tr.scored = true;
            makes++;
            streak++;
            bestStreak = Mathf.Max(bestStreak, streak);
            if (swish) swishes++;
            score += points;

            string kind = swish ? "Czysto!" : b.TouchedBoard ? "Z tablicy!" : "Kosz!";
            Toast.Show("<b>" + kind + "</b>  +" + points + "   (" + dist.ToString("0.0").Replace('.', ',') + " m)" +
                       (streak >= 3 ? "\nSeria: " + streak : ""), 1.8f);
            Sfx.PlayAt(swish ? Sfx.Swish : Sfx.Score, c, 0.9f);
            if (!swish) Sfx.Play(Sfx.Score, 0.5f);
            Haptics.PulseBoth(0.5f, 0.12f);
            Refresh();
        }

        // ------------------------------------------------------------------ tablica

        public string StatusLine
        {
            get
            {
                if (ChallengeRunning) return "Kosz: <b>" + score + " pkt</b>  czas " + Mathf.CeilToInt(timeLeft) + " s";
                return "Kosz: " + makes + " trafień, " + score + " pkt (B = wyzwanie 60 s)";
            }
        }

        void Refresh()
        {
            if (scoreText == null) return;
            var sb = new StringBuilder(256);
            if (ChallengeRunning)
                sb.Append("<b>WYZWANIE</b>   <size=96><b>").Append(Mathf.CeilToInt(timeLeft)).Append(" s</b></size>\n");
            else
                sb.Append("<b>TRENING</b>   <color=#9AB>B = wyzwanie ").Append(Mathf.RoundToInt(challengeSeconds)).Append(" s</color>\n");
            sb.Append("Punkty: <b>").Append(score).Append("</b>    Trafienia: ").Append(makes).Append('/').Append(shots);
            if (shots > 0) sb.Append(" (").Append(Mathf.RoundToInt(100f * makes / shots)).Append("%)");
            sb.Append('\n');
            sb.Append("Seria: ").Append(streak).Append(" (najdłuższa ").Append(bestStreak).Append(")    Czyste: ").Append(swishes).Append('\n');
            sb.Append("<color=#FFD54A>Rekord wyzwania: ").Append(best).Append(" pkt</color>");
            scoreText.text = sb.ToString();
        }
    }
}
