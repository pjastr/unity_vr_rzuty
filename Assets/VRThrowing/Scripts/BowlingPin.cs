using UnityEngine;

namespace VRThrowing
{
    /// <summary>
    /// Kręgiel: zapamiętuje pozycję wyjściową (lokalnie względem rodzica - końca toru) i umie wrócić na miejsce.
    /// Przewrócony = pochylony o więcej niż <see cref="downAngle"/> albo zrzucony z płyty (spadł niżej).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BowlingPin : MonoBehaviour
    {
        [Tooltip("Pochylenie (stopnie), od którego kręgiel liczy się jako przewrócony.")]
        public float downAngle = 25f;
        [Tooltip("Spadek wysokości (m), od którego kręgiel liczy się jako przewrócony (wpadł do dołu).")]
        public float dropThreshold = 0.08f;

        Rigidbody body;
        Vector3 homeLocalPos;
        Quaternion homeLocalRot;
        bool hasHome;

        public Rigidbody Body => body != null ? body : body = GetComponent<Rigidbody>();

        void Awake() => RememberHome();

        public void RememberHome()
        {
            homeLocalPos = transform.localPosition;
            homeLocalRot = transform.localRotation;
            hasHome = true;
        }

        public bool IsStanding
        {
            get
            {
                if (!gameObject.activeSelf || !hasHome) return false;
                if (Vector3.Angle(transform.up, Vector3.up) > downAngle) return false;
                Vector3 home = transform.parent != null ? transform.parent.TransformPoint(homeLocalPos) : homeLocalPos;
                return transform.position.y > home.y - dropThreshold;
            }
        }

        /// <summary>Ustawia kręgiel na miejscu (aktywny, bez prędkości).</summary>
        public void ResetPose()
        {
            if (!hasHome) RememberHome();
            gameObject.SetActive(true);
            var b = Body;
            if (!b.isKinematic)
            {
                b.linearVelocity = Vector3.zero;
                b.angularVelocity = Vector3.zero;
            }
            b.isKinematic = true;
            transform.localPosition = homeLocalPos;
            transform.localRotation = homeLocalRot;
            b.isKinematic = false;
            b.linearVelocity = Vector3.zero;
            b.angularVelocity = Vector3.zero;
            b.Sleep();
        }

        /// <summary>Zdejmuje przewrócony kręgiel z toru (jak maszyna ustawiająca).</summary>
        public void Remove() => gameObject.SetActive(false);
    }
}
