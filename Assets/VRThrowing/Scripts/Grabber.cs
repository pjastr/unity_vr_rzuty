using UnityEngine;

namespace VRThrowing
{
    /// <summary>Pomiary z ostatniego rzutu - pokazywane na wyświetlaczu nad lewym kontrolerem.</summary>
    public struct ThrowInfo
    {
        public bool valid;
        public BallKind kind;
        public VelocitySource source;
        public float speedOvr, speedHistory, speedLastFrame;
        public float finalSpeed;
        public float spinRpm;
        public float elevationDeg;
        public bool usedFallback;
        public bool assisted;
    }

    /// <summary>
    /// Chwytanie i rzucanie obiema rękami (OVRInput, grip).
    ///
    /// Prędkość w chwili puszczenia można liczyć na trzy sposoby (ustawienie „Źródło prędkości”):
    ///  1) OVRInput.GetLocalControllerVelocity / GetLocalControllerAngularVelocityCw - z systemu śledzenia
    ///     (przeliczone z TrackingSpace na świat, plus składnik ω × r, bo piłka nie leży w środku kontrolera),
    ///  2) historia - nachylenie prostej dopasowanej metodą najmniejszych kwadratów do pozycji piłki z ostatnich 0,1 s,
    ///  3) ostatnia klatka - różnica pozycji z dwóch ostatnich klatek (pokazuje, dlaczego tak się nie robi).
    /// Potem: mnożnik siły, opcjonalna pomoc w celowaniu (<see cref="AimAssist"/>) i spin.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class Grabber : MonoBehaviour
    {
        public static Grabber Instance { get; private set; }

        [Header("Chwytanie")]
        [Tooltip("Punkt chwytu względem kontrolera (m).")]
        public Vector3 grabOffset = new Vector3(0f, -0.01f, 0.04f);
        [Tooltip("Maksymalna odległość od punktu chwytu do powierzchni piłki.")]
        public float grabReach = 0.07f;
        [Range(0.1f, 0.9f)] public float gripPress = 0.6f;
        [Range(0.05f, 0.8f)] public float gripRelease = 0.35f;
        public LayerMask mask = ~(1 << 2);

        [Header("Szacowanie prędkości")]
        [Tooltip("Okno czasowe metody „historia” (s).")]
        public float historyWindow = 0.1f;
        [Tooltip("Gdy OVRInput zwraca prawie zero, a ręka wyraźnie się rusza - użyj historii.")]
        public bool fallbackToHistory = true;
        [Tooltip("Rzut z prędkością poniżej tej wartości to „odłożenie”, nie rzut.")]
        public float minThrowSpeed = 0.6f;

        [Header("Podgląd toru (ustawia generator sceny)")]
        public LineRenderer previewLeft;
        public LineRenderer previewRight;
        public int previewPoints = 70;
        public float previewStep = 0.025f;

        public ThrowInfo LastThrow { get; private set; }

        class Sample
        {
            public float t;
            public Vector3 p;
            public Quaternion r;
        }

        class Hand
        {
            public HandSide side;
            public Throwable held;
            public bool gripDown;
            public Throwable inReach;
            public readonly Sample[] samples = new Sample[48];
            public int count, head;
            public LineRenderer preview;

            public Hand()
            {
                for (int i = 0; i < samples.Length; i++) samples[i] = new Sample();
            }

            public void Clear() => count = 0;

            public void Add(float t, Vector3 p, Quaternion r)
            {
                head = (head + 1) % samples.Length;
                samples[head].t = t;
                samples[head].p = p;
                samples[head].r = r;
                if (count < samples.Length) count++;
            }

            /// <summary>i = 0 to najnowsza próbka, 1 - poprzednia itd.</summary>
            public Sample Get(int i) => samples[(head - i + samples.Length * 4) % samples.Length];
        }

        readonly Hand[] hands = { new Hand { side = HandSide.Left }, new Hand { side = HandSide.Right } };
        Vector3[] previewBuffer;
        readonly Collider[] overlap = new Collider[32];

        public bool IsHolding(HandSide side) => hands[(int)side].held != null;
        public Throwable Held(HandSide side) => hands[(int)side].held;

        void Awake()
        {
            Instance = this;
            hands[0].preview = previewLeft;
            hands[1].preview = previewRight;
            previewBuffer = new Vector3[Mathf.Max(2, previewPoints + 1)];
            foreach (var h in hands)
                if (h.preview != null) h.preview.enabled = false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnEnable()
        {
            PlayerMover.Discontinuity += ClearHistories;
        }

        void OnDisable()
        {
            PlayerMover.Discontinuity -= ClearHistories;
        }

        void ClearHistories()
        {
            foreach (var h in hands) h.Clear();
        }

        // LateUpdate: kotwice kontrolerów są już zaktualizowane w tej klatce.
        void LateUpdate()
        {
            var s = ThrowSettings.Instance;
            foreach (var h in hands) UpdateHand(h, s);
        }

        void UpdateHand(Hand h, ThrowSettings s)
        {
            Transform anchor = PlayerRig.Controller(h.side);
            if (anchor == null) return;

            if (h.held != null && h.held.State != BallState.Held) h.held = null; // ktoś „zabrał” piłkę

            if (h.held != null) h.Add(Time.time, h.held.transform.position, h.held.transform.rotation);

            var maskCtl = PlayerRig.Mask(h.side);
            float grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, maskCtl);
            bool wasDown = h.gripDown;
            h.gripDown = wasDown ? grip > gripRelease : grip > gripPress;
            bool pressed = h.gripDown && !wasDown;
            bool released = !h.gripDown && wasDown;
            bool toggle = s != null && s.toggleGrip;

            if (h.held == null)
            {
                Vector3 hp = anchor.TransformPoint(grabOffset);
                Throwable near = FindNearest(hp);
                if (near != h.inReach)
                {
                    h.inReach = near;
                    if (near != null) Haptics.Pulse(h.side, 0.12f, 0.02f);
                }
                if (pressed && near != null) Grab(h, near, anchor);
            }
            else if ((toggle && pressed) || (!toggle && released) || !PlayerRig.IsConnected(h.side))
            {
                ReleaseHeld(h, anchor, s);
            }

            UpdatePreview(h, anchor, s);
        }

        Throwable FindNearest(Vector3 point)
        {
            int n = Physics.OverlapSphereNonAlloc(point, grabReach + 0.35f, overlap, mask, QueryTriggerInteraction.Ignore);
            Throwable best = null;
            float bestDist = grabReach;
            for (int i = 0; i < n; i++)
            {
                var c = overlap[i];
                var t = c.GetComponentInParent<Throwable>();
                if (t == null || t.State == BallState.Held) continue;
                float d = Vector3.Distance(point, c.ClosestPoint(point));
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = t;
                }
            }
            return best;
        }

        void Grab(Hand h, Throwable t, Transform anchor)
        {
            h.held = t;
            h.inReach = null;
            h.Clear();
            t.AttachTo(anchor);
            h.Add(Time.time, t.transform.position, t.transform.rotation);
            Haptics.Pulse(h.side, 0.35f, 0.05f);
            Sfx.PlayAt(Sfx.Grab, t.transform.position, 0.5f);
        }

        void ReleaseHeld(Hand h, Transform anchor, ThrowSettings s)
        {
            Throwable t = h.held;
            h.held = null;
            if (t == null) return;

            ThrowInfo info;
            Vector3 w;
            Vector3 v = Estimate(h, anchor, t, s, out w, out info);

            Transform head = PlayerRig.Head;
            t.Release(v, w, head != null ? head.position : t.transform.position);
            LastThrow = info;
            h.Clear();
            if (h.preview != null) h.preview.enabled = false;

            float speed = v.magnitude;
            Haptics.Pulse(h.side, Mathf.Clamp(speed / 10f, 0.1f, 0.8f), 0.06f);
            if (speed > 2f) Sfx.PlayAt(Sfx.Whoosh, t.transform.position, Mathf.Clamp01(speed / 12f));
        }

        // ------------------------------------------------------------------ szacowanie prędkości

        /// <summary>Prędkość, jaką dostałaby piłka, gdyby puścić ją teraz (z mnożnikiem i pomocą w celowaniu).</summary>
        Vector3 Estimate(Hand h, Transform anchor, Throwable t, ThrowSettings s, out Vector3 angular, out ThrowInfo info)
        {
            info = new ThrowInfo { valid = true, kind = t.kind };
            VelocitySource source = s != null ? s.velocitySource : VelocitySource.OvrInput;
            info.source = source;

            Vector3 wOvr;
            Vector3 vOvr = OvrVelocity(h.side, anchor, t.transform.position, out wOvr);
            Vector3 wHist;
            Vector3 vHist = HistoryVelocity(h, out wHist);
            Vector3 vLast = LastFrameVelocity(h);
            info.speedOvr = vOvr.magnitude;
            info.speedHistory = vHist.magnitude;
            info.speedLastFrame = vLast.magnitude;

            Vector3 v;
            switch (source)
            {
                case VelocitySource.History:
                    v = vHist;
                    angular = wHist;
                    break;
                case VelocitySource.LastFrame:
                    v = vLast;
                    angular = wHist;
                    break;
                default:
                    v = vOvr;
                    angular = wOvr;
                    if (fallbackToHistory && vOvr.magnitude < 0.05f && vHist.magnitude > 0.5f)
                    {
                        v = vHist;
                        angular = wHist;
                        info.usedFallback = true;
                    }
                    break;
            }

            if (s != null)
            {
                v *= s.Multiplier;
                if (!s.spin) angular = Vector3.zero;
                if (s.Assist > 0f && v.magnitude > minThrowSpeed)
                {
                    Vector3 before = v;
                    v = AimAssist.Apply(t, t.transform.position, v, s.Assist);
                    info.assisted = (v - before).sqrMagnitude > 1e-4f;
                }
            }

            info.finalSpeed = v.magnitude;
            info.spinRpm = angular.magnitude * 60f / (2f * Mathf.PI);
            Vector3 flat = new Vector3(v.x, 0f, v.z);
            info.elevationDeg = Mathf.Atan2(v.y, Mathf.Max(1e-4f, flat.magnitude)) * Mathf.Rad2Deg;
            return v;
        }

        static Vector3 OvrVelocity(HandSide side, Transform anchor, Vector3 ballCenter, out Vector3 angular)
        {
            var ctl = PlayerRig.Mask(side);
            Vector3 vLocal = OVRInput.GetLocalControllerVelocity(ctl);
            Vector3 wLocal = OVRInput.GetLocalControllerAngularVelocityCw(ctl); // konwencja Unity (wersja bez „Cw” jest przestarzała)
            Transform ts = PlayerRig.TrackingSpace;
            Quaternion r = ts != null ? ts.rotation : Quaternion.identity;

            Vector3 v = r * vLocal;           // z przestrzeni śledzenia do świata
            angular = r * wLocal;             // rad/s
            v += Vector3.Cross(angular, ballCenter - anchor.position); // punkt piłki, nie środek kontrolera
            if (PlayerMover.Instance != null) v += PlayerMover.Instance.Velocity; // ruch gałką
            return v;
        }

        Vector3 HistoryVelocity(Hand h, out Vector3 angular)
        {
            angular = Vector3.zero;
            if (h.count < 2) return Vector3.zero;
            float now = h.Get(0).t;

            // Metoda najmniejszych kwadratów: p(t) = a + v·t, liczymy v.
            int n = 0;
            float tm = 0f;
            Vector3 pm = Vector3.zero;
            for (int i = 0; i < h.count; i++)
            {
                var smp = h.Get(i);
                if (now - smp.t > historyWindow && n >= 3) break;
                tm += smp.t;
                pm += smp.p;
                n++;
            }
            if (n < 2) return Vector3.zero;
            tm /= n;
            pm /= n;
            float stt = 0f;
            Vector3 stp = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                var smp = h.Get(i);
                float dt = smp.t - tm;
                stt += dt * dt;
                stp += dt * (smp.p - pm);
            }
            if (stt < 1e-8f) return Vector3.zero;

            // Prędkość kątowa: obrót między najstarszą a najnowszą próbką okna.
            var newest = h.Get(0);
            var oldest = h.Get(n - 1);
            float span = newest.t - oldest.t;
            if (span > 1e-4f)
            {
                Quaternion dq = newest.r * Quaternion.Inverse(oldest.r);
                float angle;
                Vector3 axis;
                dq.ToAngleAxis(out angle, out axis);
                if (angle > 180f) angle -= 360f;
                if (!float.IsNaN(axis.x)) angular = axis * (angle * Mathf.Deg2Rad / span);
            }
            return stp / stt;
        }

        static Vector3 LastFrameVelocity(Hand h)
        {
            if (h.count < 2) return Vector3.zero;
            var a = h.Get(0);
            var b = h.Get(1);
            float dt = a.t - b.t;
            return dt > 1e-5f ? (a.p - b.p) / dt : Vector3.zero;
        }

        // ------------------------------------------------------------------ podgląd toru lotu

        void UpdatePreview(Hand h, Transform anchor, ThrowSettings s)
        {
            if (h.preview == null) return;
            bool show = h.held != null && s != null && s.trajectoryPreview;
            if (!show)
            {
                if (h.preview.enabled) h.preview.enabled = false;
                return;
            }

            Vector3 w;
            ThrowInfo dummy;
            Vector3 v = Estimate(h, anchor, h.held, s, out w, out dummy);
            if (v.magnitude < minThrowSpeed)
            {
                h.preview.enabled = false;
                return;
            }

            Vector3 p = h.held.transform.position;
            Vector3 g = Physics.gravity;
            int count = 0;
            previewBuffer[count++] = p;
            for (int i = 0; i < previewPoints && count < previewBuffer.Length; i++)
            {
                Vector3 next = p + v * previewStep + 0.5f * g * previewStep * previewStep;
                v += g * previewStep;
                Vector3 seg = next - p;
                float len = seg.magnitude;
                RaycastHit hit;
                if (len > 1e-5f && Physics.SphereCast(p, h.held.radius * 0.5f, seg / len, out hit, len, mask, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<Throwable>() != h.held)
                {
                    previewBuffer[count++] = hit.point;
                    break;
                }
                previewBuffer[count++] = next;
                p = next;
            }
            h.preview.enabled = true;
            h.preview.positionCount = count;
            for (int i = 0; i < count; i++) h.preview.SetPosition(i, previewBuffer[i]);
        }
    }
}
