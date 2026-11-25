using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VRThrowing
{
    /// <summary>
    /// Tor do kręgli: przebieg rzutu, liczenie kręgli, maszyna ustawiająca i tablica 10 ramek.
    /// Układ lokalny toru: oś Z wzdłuż toru, z = 0 to linia spalonego, pierwszy kręgiel w z = długość toru.
    ///
    /// Przebieg rzutu:
    ///   Ready → (kula puszczona) Released → (kula za linią spalonego) Rolling →
    ///   (kula w dole / zatrzymana / 10 s) Settling (kręgle się uspokajają) → liczenie →
    ///   zdjęcie przewróconych kręgli albo ustawienie wszystkich (nowa ramka) → Ready.
    /// Spalony: głowa gracza za linią w chwili puszczenia kuli - rzut liczy się za 0, kręgle wracają na miejsca.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class BowlingLane : MonoBehaviour
    {
        public static BowlingLane Instance { get; private set; }

        [Header("Tor (ustawia generator sceny)")]
        [Tooltip("Koniec toru: płyta z kręglami, dół, zasłona. Przesuwany przy zmianie długości toru.")]
        public Transform farEnd;
        public LaneStretch[] stretch;
        public BowlingPin[] pins;
        public Throwable[] balls;
        [Tooltip("Cel pomocy w celowaniu (pierwszy kręgiel, wysokość środka kuli).")]
        public Transform aimTarget;
        public GameObject bumpers;
        public float laneTop = 0.15f;

        [Header("Tablica (ustawia generator sceny)")]
        public Text[] frameMarks;
        public Text[] frameTotals;
        public Text totalText;
        public Text infoText;

        [Header("Czasy")]
        public float maxRollTime = 10f;
        public float settleTime = 2.5f;
        public float rackDelay = 1f;

        enum LaneState { Ready, Released, Rolling, Settling, Racking, GameOver }

        readonly BowlingScore score = new BowlingScore();
        readonly List<BowlingPin> standingBefore = new List<BowlingPin>();
        LaneState state;
        Throwable activeBall;
        bool foul;
        float stateTime, stillTime;
        float appliedLength = -1f;
        int best;
        const string BestKey = "rzuty.v1.bowling.best";

        public float LaneLength => appliedLength > 0f ? appliedLength : 18.29f;

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
            AimAssist.BowlingTarget = aimTarget;
            foreach (var b in balls)
            {
                if (b == null) continue;
                b.Thrown += OnThrown;
            }
            if (ThrowSettings.Instance != null) ThrowSettings.Instance.Changed += ApplySettings;
            ApplySettings();
            ResetAllPins();
            SetState(LaneState.Ready);
            RefreshBoard();
        }

        // ------------------------------------------------------------------ ustawienia: długość toru, bandy

        void ApplySettings()
        {
            var s = ThrowSettings.Instance;
            if (s == null) return;

            if (bumpers != null && bumpers.activeSelf != s.bumpers) bumpers.SetActive(s.bumpers);

            if (!Mathf.Approximately(appliedLength, s.LaneLength))
            {
                if (state == LaneState.Rolling || state == LaneState.Settling)
                {
                    Toast.Show("Długość toru zmieni się po tym rzucie.");
                    return;
                }
                appliedLength = s.LaneLength;
                if (farEnd != null)
                {
                    Vector3 p = farEnd.localPosition;
                    farEnd.localPosition = new Vector3(p.x, p.y, appliedLength);
                }
                if (stretch != null)
                    foreach (var st in stretch)
                        if (st != null) st.Apply(appliedLength);
                Physics.SyncTransforms();
                ResetAllPins();
                if (state != LaneState.Ready && state != LaneState.GameOver) SetState(LaneState.Ready);
            }
        }

        // ------------------------------------------------------------------ gra

        public void NewGame()
        {
            score.Clear();
            foreach (var b in balls)
                if (b != null && b.State == BallState.Free) b.ReturnHome();
            activeBall = null;
            ResetAllPins();
            SetState(LaneState.Ready);
            RefreshBoard();
            Sfx.Play(Sfx.Toggle, 0.5f);
            Toast.Show("<b>Nowa gra</b> – 10 ramek. Rzucaj zza czerwonej linii!");
        }

        public string StatusLine
        {
            get
            {
                if (state == LaneState.GameOver || score.GameOver) return "Kręgle: koniec gry, " + score.Total + " pkt (B = nowa)";
                return "Kręgle: ramka " + (score.CurrentFrame + 1) + ", rzut " + (score.RollInFrame + 1) + ", wynik " + score.Total;
            }
        }

        void OnThrown(Throwable b)
        {
            if (state != LaneState.Ready && state != LaneState.Released) return;
            Transform head = PlayerRig.Head;
            Vector3 local = transform.InverseTransformPoint(head != null ? b.ReleaseHeadPosition : b.ReleasePosition);
            foul = local.z > 0.05f;
            activeBall = b;
            standingBefore.Clear();
            foreach (var p in pins)
                if (p != null && p.IsStanding) standingBefore.Add(p);
            SetState(LaneState.Released);
        }

        void SetState(LaneState s)
        {
            state = s;
            stateTime = Time.time;
            stillTime = 0f;
        }

        void Update()
        {
            float t = Time.time - stateTime;
            switch (state)
            {
                case LaneState.Released:
                    if (activeBall == null || activeBall.State != BallState.Free)
                    {
                        SetState(LaneState.Ready); // złapana ponownie albo wróciła na stojak przed linią
                        break;
                    }
                    if (Local(activeBall).z > 0.6f)
                    {
                        activeBall.HoldAutoReturn = true;
                        SetState(LaneState.Rolling);
                        if (foul)
                        {
                            Sfx.Play(Sfx.Buzzer, 0.5f);
                            Toast.Show("<color=#FF6B6B><b>SPALONY!</b></color>  Stopa za linią – rzut za 0.", 2.5f);
                        }
                    }
                    break;

                case LaneState.Rolling:
                {
                    bool finished = activeBall == null || activeBall.State != BallState.Free;
                    if (!finished)
                    {
                        Vector3 lp = Local(activeBall);
                        if (activeBall.Body.linearVelocity.magnitude < 0.08f) stillTime += Time.deltaTime;
                        else stillTime = 0f;
                        finished = lp.z > LaneLength + 1.2f || lp.y < laneTop - 0.2f || stillTime > 1f || t > maxRollTime;
                    }
                    if (finished)
                    {
                        if (activeBall != null)
                        {
                            activeBall.HoldAutoReturn = false;
                            if (activeBall.State == BallState.Free) activeBall.ReturnHome();
                        }
                        SetState(LaneState.Settling);
                    }
                    break;
                }

                case LaneState.Settling:
                    if (t > settleTime) Evaluate();
                    break;

                case LaneState.Racking:
                    if (t > rackDelay)
                    {
                        ResetAllPins();
                        SetState(LaneState.Ready);
                        Sfx.PlayAt(Sfx.PinHit, aimTarget != null ? aimTarget.position : transform.position, 0.4f);
                    }
                    break;
            }
        }

        Vector3 Local(Throwable b) => transform.InverseTransformPoint(b.transform.position);

        void Evaluate()
        {
            int knocked = 0;
            foreach (var p in standingBefore)
                if (p != null && !p.IsStanding) knocked++;

            if (foul)
            {
                knocked = 0;
                foreach (var p in standingBefore)
                    if (p != null) p.ResetPose();
            }

            int frameBefore = score.CurrentFrame;
            int rollBefore = score.RollInFrame;
            int standingCount = standingBefore.Count;
            score.Roll(knocked);
            RefreshBoard();

            // komunikat
            string msg;
            if (!foul && knocked == standingCount && knocked == 10 && (rollBefore == 0 || frameBefore == 9))
            {
                msg = "<size=60><b>STRIKE!</b></size>";
                Sfx.Play(Sfx.Strike, 0.8f);
                Haptics.PulseBoth(0.7f, 0.25f);
            }
            else if (!foul && knocked == standingCount && knocked > 0)
            {
                msg = "<size=56><b>SPARE!</b></size>";
                Sfx.Play(Sfx.Score, 0.7f);
                Haptics.PulseBoth(0.5f, 0.15f);
            }
            else if (knocked == 0)
            {
                msg = foul ? "Spalony – 0" : "Pudło – 0 kręgli";
            }
            else
            {
                msg = knocked + (knocked == 1 ? " kręgiel" : knocked < 5 ? " kręgle" : " kręgli");
            }
            Toast.Show(msg + "\n<size=34>Ramka " + (frameBefore + 1) + " · wynik " + score.Total + "</size>", 2.2f);

            if (score.GameOver)
            {
                bool record = score.Total > best;
                if (record)
                {
                    best = score.Total;
                    PlayerPrefs.SetInt(BestKey, best);
                    PlayerPrefs.Save();
                }
                SetState(LaneState.GameOver);
                RefreshBoard();
                Toast.Show("<b>KONIEC GRY: " + score.Total + " pkt</b>" + (record ? "  <color=#FFD54A>REKORD!</color>" : "") +
                           "\nB = nowa gra", 6f);
                return;
            }

            if (score.NeedsFullRack)
            {
                SetState(LaneState.Racking);
            }
            else
            {
                foreach (var p in pins)
                    if (p != null && p.gameObject.activeSelf && !p.IsStanding) p.Remove();
                SetState(LaneState.Ready);
            }
        }

        void ResetAllPins()
        {
            foreach (var p in pins)
                if (p != null) p.ResetPose();
        }

        // ------------------------------------------------------------------ tablica

        void RefreshBoard()
        {
            var totals = score.Totals();
            for (int f = 0; f < BowlingScore.Frames; f++)
            {
                if (frameMarks != null && f < frameMarks.Length && frameMarks[f] != null) frameMarks[f].text = score.Marks(f);
                if (frameTotals != null && f < frameTotals.Length && frameTotals[f] != null)
                    frameTotals[f].text = totals[f].HasValue ? totals[f].Value.ToString() : "";
            }
            if (totalText != null) totalText.text = "<b>" + score.Total + "</b>";
            if (infoText != null)
            {
                string status = score.GameOver
                    ? "Koniec gry. B = nowa gra"
                    : "Ramka <b>" + (score.CurrentFrame + 1) + "</b>, rzut " + (score.RollInFrame + 1) + "   ·   B = nowa gra";
                infoText.text = status + "   ·   <color=#FFD54A>Rekord: " + best + "</color>";
            }
        }
    }
}
