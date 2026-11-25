using System;
using System.Collections;
using UnityEngine;

namespace VRThrowing
{
    /// <summary>
    /// Proste poruszanie się po stanowisku (OVRInput):
    ///  - lewa gałka: powolny ruch płynny (wg kierunku głowy), ograniczony do obszaru stanowiska,
    ///  - prawa gałka ← →: obrót skokowy 45°,
    ///  - przejście do drugiego stanowiska (kosz ↔ kręgle) z mrugnięciem.
    /// Zdarzenie <see cref="Discontinuity"/> informuje Grabber o skokach pozycji (obrót, teleport) -
    /// historia ruchu ręki jest wtedy czyszczona, żeby nie zafałszować prędkości rzutu.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public class PlayerMover : MonoBehaviour
    {
        public static PlayerMover Instance { get; private set; }
        public static event Action Discontinuity;

        [Header("Punkty stanowisk (ustawia generator sceny)")]
        public Transform basketballStand;
        public Transform bowlingStand;
        [Tooltip("Obszar ruchu na stanowisku kosza (XZ, świat).")]
        public Rect basketballArea = new Rect(-4.5f, -2.8f, 9f, 9.4f);
        [Tooltip("Obszar ruchu na stanowisku kręgli (XZ, świat).")]
        public Rect bowlingArea = new Rect(10.5f, -4f, 3f, 4.7f);
        public ScreenFader fader;

        [Header("Ruch")]
        public float moveSpeed = 1.4f;
        [Range(0.05f, 0.5f)] public float deadZone = 0.15f;
        public float snapAngle = 45f;

        public bool InputLocked { get; set; }
        public bool Busy { get; private set; }

        /// <summary>Prędkość sztucznego ruchu rigu (m/s) - dodawana do prędkości rzutu z OVRInput.</summary>
        public Vector3 Velocity { get; private set; }

        public Station Current { get; private set; }

        bool turnArmed = true;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            var s = ThrowSettings.Instance;
            Current = s != null ? s.station : Station.Basketball;
            StartCoroutine(PlaceWhenReady());
        }

        IEnumerator PlaceWhenReady()
        {
            while (PlayerRig.Head == null) yield return null;
            yield return null; // pierwsza klatka śledzenia
            Place(StandFor(Current), false);
            Toast.Show("<b>Grip</b> = złap piłkę ze stojaka, puść grip = rzut.\n<b>Y</b> = kręgle / kosz,  <b>Menu</b> = ustawienia.", 5f);
        }

        void Update()
        {
            Velocity = Vector3.zero;
            var rig = PlayerRig.Rig;
            Transform head = PlayerRig.Head;
            if (rig == null || head == null || Busy || InputLocked) return;

            // ---- ruch płynny w obrębie stanowiska
            Vector2 l = PlayerRig.Stick(HandSide.Left);
            float mag = l.magnitude;
            if (mag > deadZone)
            {
                float amount = Mathf.InverseLerp(deadZone, 1f, Mathf.Min(1f, mag));
                Vector3 dir = PlayerRig.Flat(head.forward) * l.y + PlayerRig.Flat(head.right) * l.x;
                Vector3 delta = dir / Mathf.Max(1f, dir.magnitude) * (amount * moveSpeed * Time.deltaTime);

                Rect area = Current == Station.Basketball ? basketballArea : bowlingArea;
                Vector3 h = head.position + delta;
                h.x = Mathf.Clamp(h.x, area.xMin, area.xMax);
                h.z = Mathf.Clamp(h.z, area.yMin, area.yMax);
                delta = new Vector3(h.x - head.position.x, 0f, h.z - head.position.z);
                rig.transform.position += delta;
                if (Time.deltaTime > 0f) Velocity = delta / Time.deltaTime;
            }

            // ---- obrót skokowy
            Vector2 r = PlayerRig.Stick(HandSide.Right);
            if (turnArmed && Mathf.Abs(r.x) > 0.7f && Mathf.Abs(r.x) > Mathf.Abs(r.y))
            {
                turnArmed = false;
                RotateAroundHead(Mathf.Sign(r.x) * snapAngle);
                Sfx.Play(Sfx.Click, 0.25f);
            }
            else if (r.magnitude < 0.3f)
            {
                turnArmed = true;
            }
        }

        public Transform StandFor(Station s) => s == Station.Basketball ? basketballStand : bowlingStand;

        /// <summary>Przejście do drugiego stanowiska.</summary>
        public void SwitchStation()
        {
            GoTo(Current == Station.Basketball ? Station.Bowling : Station.Basketball);
        }

        public void GoTo(Station s)
        {
            if (Busy) return;
            Current = s;
            var settings = ThrowSettings.Instance;
            if (settings != null && settings.station != s)
            {
                settings.station = s;
                settings.NotifyChanged();
            }
            Place(StandFor(s), true);
        }

        /// <summary>Ustawia gracza (głowę) nad punktem i obraca w kierunku jego osi Z.</summary>
        public void Place(Transform point, bool blink)
        {
            if (point == null || PlayerRig.Rig == null) return;
            StartCoroutine(PlaceRoutine(point, blink && fader != null));
        }

        IEnumerator PlaceRoutine(Transform point, bool blink)
        {
            Busy = true;
            if (blink) yield return fader.Fade(1f, 0.12f);

            var rig = PlayerRig.Rig;
            Transform head = PlayerRig.Head;
            float yaw = Vector3.SignedAngle(PlayerRig.Flat(head.forward), PlayerRig.Flat(point.forward), Vector3.up);
            RotateAroundHead(yaw);
            Vector3 p = rig.transform.position;
            rig.transform.position = new Vector3(p.x + point.position.x - head.position.x, point.position.y,
                p.z + point.position.z - head.position.z);
            if (Discontinuity != null) Discontinuity();

            if (blink)
            {
                yield return null;
                yield return fader.Fade(0f, 0.2f);
            }
            Busy = false;
        }

        void RotateAroundHead(float angle)
        {
            var rig = PlayerRig.Rig;
            Transform head = PlayerRig.Head;
            if (rig == null || head == null) return;
            rig.transform.RotateAround(head.position, Vector3.up, angle);
            if (Discontinuity != null) Discontinuity();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Discontinuity = null;
    }
}
