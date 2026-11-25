using UnityEngine;
using UnityEngine.UI;

namespace VRThrowing
{
    /// <summary>
    /// Menu ustawień w goglach (OVRInput):
    ///  - Menu (lewy kontroler) - otwiera / zamyka menu przed graczem; w czasie otwarcia ruch jest zablokowany,
    ///  - promień z prawego kontrolera + trigger - przyciski „&lt;” „&gt;” przy każdej opcji i presety.
    /// Skróty działające bez menu:
    ///  - A - przywołaj piłki na stojak,   B - kosz: wyzwanie 60 s / kręgle: nowa gra,
    ///  - X - podgląd toru lotu wł./wył.,  Y - przejdź do drugiego stanowiska.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class ThrowMenu : MonoBehaviour
    {
        public static ThrowMenu Instance { get; private set; }

        [Header("Panel menu (ustawia generator sceny)")]
        public GameObject panel;
        public Text[] labelTexts;
        public Text[] valueTexts;
        public Text hintText;

        [Header("Położenie po otwarciu")]
        public float distance = 0.7f;
        public float heightOffset = -0.12f;

        [Header("Kolory")]
        public Color labelColor = Color.white;
        public Color inactiveColor = new Color(1f, 1f, 1f, 0.35f);

        public bool IsOpen { get; private set; }

        const string DefaultHint = "Celuj prawym kontrolerem, trigger = wybór. Przycisk Menu na lewym kontrolerze zamyka menu.";

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (ThrowSettings.Instance != null) ThrowSettings.Instance.Changed -= Refresh;
        }

        void Start()
        {
            if (ThrowSettings.Instance != null) ThrowSettings.Instance.Changed += Refresh;
            Sfx.Warmup();
            SetOpen(false, true);
            Refresh();
        }

        void Update()
        {
            if (OVRInput.GetDown(OVRInput.Button.Start, OVRInput.Controller.LTouch))
            {
                SetOpen(!IsOpen, false);
                return;
            }
            if (!IsOpen) QuickKeys();
        }

        void QuickKeys()
        {
            var s = ThrowSettings.Instance;
            var mover = PlayerMover.Instance;
            if (s == null) return;
            Station station = mover != null ? mover.Current : s.station;

            if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch)) // A
            {
                if (station == Station.Basketball && BasketballGame.Instance != null) BasketballGame.Instance.ReturnAll();
                else if (BowlingLane.Instance != null)
                    foreach (var b in BowlingLane.Instance.balls)
                        if (b != null && b.State == BallState.Free && !b.HoldAutoReturn) b.ReturnHome();
                Toast.Show("Piłki wracają na stojak.", 1.2f);
            }
            else if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch)) // B
            {
                if (station == Station.Basketball && BasketballGame.Instance != null) BasketballGame.Instance.ToggleChallenge();
                else if (station == Station.Bowling && BowlingLane.Instance != null) BowlingLane.Instance.NewGame();
            }
            else if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch)) // X
            {
                QuickStep(s, ThrowSettings.OptPreview);
            }
            else if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch)) // Y
            {
                if (mover != null && !mover.Busy)
                {
                    mover.SwitchStation();
                    Sfx.Play(Sfx.Toggle, 0.5f);
                    Toast.Show("Stanowisko: <b>" + ThrowSettings.Name(mover.Current) + "</b>", 1.5f);
                }
            }
        }

        void QuickStep(ThrowSettings s, int option)
        {
            s.Step(option, 1);
            Sfx.Play(Sfx.Toggle, 0.5f);
            Toast.Show(ThrowSettings.OptionName(option) + ": <b>" + s.OptionValue(option) + "</b>");
        }

        public void SetOpen(bool open, bool silent)
        {
            IsOpen = open;
            var mover = PlayerMover.Instance;
            if (mover != null) mover.InputLocked = open;
            if (panel == null) return;

            if (open)
            {
                Transform head = PlayerRig.Head;
                if (head != null)
                {
                    Vector3 fwd = PlayerRig.Flat(head.forward);
                    panel.transform.SetPositionAndRotation(head.position + fwd * distance + Vector3.up * heightOffset,
                        Quaternion.LookRotation(fwd, Vector3.up));
                    var rig = PlayerRig.Rig;
                    if (rig != null) panel.transform.SetParent(rig.transform, true); // menu jedzie razem z graczem
                }
                SetHint(DefaultHint);
                Refresh();
            }
            panel.SetActive(open);
            if (!silent) Sfx.Play(open ? Sfx.Toggle : Sfx.Click, 0.5f);
        }

        public void OnButton(MenuButton b)
        {
            var s = ThrowSettings.Instance;
            if (s == null) return;
            Sfx.Play(Sfx.Click, 0.6f);

            if (b.closesMenu)
            {
                SetOpen(false, true);
                return;
            }
            if (b.preset >= 0)
            {
                s.ApplyPreset(b.preset);
                SetHint("<b>" + ThrowSettings.PresetNames[Mathf.Clamp(b.preset, 0, ThrowSettings.PresetNames.Length - 1)] +
                        "</b>\n" + ThrowSettings.PresetHint(b.preset));
                return;
            }
            if (b.option >= 0)
            {
                s.Step(b.option, b.delta >= 0 ? 1 : -1);
                SetHint("<b>" + ThrowSettings.OptionName(b.option) + "</b>\n" + ThrowSettings.OptionHint(b.option));
            }
        }

        void SetHint(string text)
        {
            if (hintText != null) hintText.text = text;
        }

        public void Refresh()
        {
            var s = ThrowSettings.Instance;
            if (s == null) return;
            for (int i = 0; i < ThrowSettings.OptionCount; i++)
            {
                bool relevant = s.OptionRelevant(i);
                if (valueTexts != null && i < valueTexts.Length && valueTexts[i] != null)
                {
                    valueTexts[i].text = s.OptionValue(i);
                    valueTexts[i].color = relevant ? labelColor : inactiveColor;
                }
                if (labelTexts != null && i < labelTexts.Length && labelTexts[i] != null)
                    labelTexts[i].color = relevant ? labelColor : inactiveColor;
            }
        }
    }
}
