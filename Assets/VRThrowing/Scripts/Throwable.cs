using System;
using UnityEngine;

namespace VRThrowing
{
    public enum BallKind { Basketball, Bowling }

    public enum BallState { Docked, Held, Free }

    /// <summary>
    /// Piłka, którą można złapać (<see cref="Grabber"/>) i rzucić.
    ///  - Docked: leży na stojaku (Rigidbody kinematyczne - nie spada, nie toczy się),
    ///  - Held: w ręce (kinematyczne, przypięte do kontrolera),
    ///  - Free: po rzucie - fizyka. Po zatrzymaniu się, wypadnięciu poza obszar albo upadku pod podłogę
    ///    piłka sama wraca na stojak.
    /// Zapamiętuje też informacje o rzucie (odległość od celu, dotknięcie obręczy itp.) dla logiki gier.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Throwable : MonoBehaviour
    {
        public BallKind kind = BallKind.Basketball;
        [Tooltip("Miejsce na stojaku, na które piłka wraca.")]
        public Transform home;
        public float radius = 0.12f;

        [Header("Automatyczny powrót")]
        [Tooltip("Po ilu sekundach bezruchu (po rzucie) piłka wraca na stojak.")]
        public float returnWhenRestingFor = 1.5f;
        [Tooltip("Po ilu sekundach od rzutu piłka wraca w każdym przypadku.")]
        public float returnAfter = 12f;
        [Tooltip("Obszar (XZ), poza którym piłka od razu wraca. Puste = bez ograniczenia.")]
        public Rect allowedArea;
        public float minY = -2f;

        public Rigidbody Body { get; private set; }
        public BallState State { get; private set; }
        public float ReleaseTime { get; private set; }
        public Vector3 ReleasePosition { get; private set; }
        public Vector3 ReleaseHeadPosition { get; private set; }
        public Vector3 ReleaseVelocity { get; private set; }

        /// <summary>Od ostatniego rzutu piłka dotknęła obręczy / tablicy (ustawiane przez kolizje).</summary>
        public bool TouchedRim { get; set; }
        public bool TouchedBoard { get; set; }

        /// <summary>Gra może wstrzymać automatyczny powrót (np. kula w trakcie toczenia po torze).</summary>
        public bool HoldAutoReturn { get; set; }

        public event Action<Throwable> Grabbed;
        public event Action<Throwable> Thrown;
        public event Action<Throwable> Returned;

        Transform originalParent;
        float restTime;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            originalParent = transform.parent;
        }

        void Start()
        {
            if (home != null) Dock();
        }

        // ------------------------------------------------------------------ stany

        /// <summary>Przypięcie do ręki (wywołuje Grabber).</summary>
        public void AttachTo(Transform hand)
        {
            State = BallState.Held;
            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None;
            transform.SetParent(hand, true);
            if (Grabbed != null) Grabbed(this);
        }

        /// <summary>Puszczenie z nadaną prędkością (wywołuje Grabber).</summary>
        public void Release(Vector3 velocity, Vector3 angularVelocity, Vector3 headPosition)
        {
            transform.SetParent(originalParent, true);
            State = BallState.Free;
            Body.isKinematic = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.linearVelocity = velocity;
            Body.angularVelocity = angularVelocity;
            Body.WakeUp();

            ReleaseTime = Time.time;
            ReleasePosition = transform.position;
            ReleaseHeadPosition = headPosition;
            ReleaseVelocity = velocity;
            TouchedRim = false;
            TouchedBoard = false;
            restTime = 0f;
            if (Thrown != null) Thrown(this);
        }

        /// <summary>Natychmiastowy powrót na stojak.</summary>
        public void ReturnHome()
        {
            if (State == BallState.Held) return;
            Dock();
            Sfx.PlayAt(Sfx.Pop, transform.position, 0.4f);
            if (Returned != null) Returned(this);
        }

        /// <summary>Piłka leżąca na stojaku przesuwa się razem z nim (np. po zmianie odległości od kosza).</summary>
        public void Redock()
        {
            if (State == BallState.Docked) Dock();
        }

        void Dock()
        {
            if (home == null) return;
            transform.SetParent(originalParent, true);
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None;
            transform.SetPositionAndRotation(home.position, home.rotation);
            State = BallState.Docked;
            HoldAutoReturn = false;
        }

        // ------------------------------------------------------------------ automatyczny powrót

        void Update()
        {
            if (State != BallState.Free || home == null) return;
            Vector3 p = transform.position;

            bool outside = p.y < minY ||
                           (allowedArea.width > 0f && !allowedArea.Contains(new Vector2(p.x, p.z)));
            if (outside)
            {
                ReturnHome();
                return;
            }
            if (HoldAutoReturn) return;

            float since = Time.time - ReleaseTime;
            if (Body.linearVelocity.sqrMagnitude < 0.2f * 0.2f) restTime += Time.deltaTime;
            else restTime = 0f;

            if ((since > 1f && restTime > returnWhenRestingFor) || since > returnAfter) ReturnHome();
        }

        void OnCollisionEnter(Collision c)
        {
            if (State != BallState.Free) return;
            if (c.collider.GetComponentInParent<RimPart>() != null) TouchedRim = true;
            if (c.collider.GetComponentInParent<Backboard>() != null) TouchedBoard = true;
        }
    }
}
