using UnityEngine;

namespace VRThrowing
{
    public enum ImpactKind { Basketball, BowlingBall, Pin }

    /// <summary>
    /// Dźwięk zderzenia (3D) zależny od siły uderzenia: odbicie piłki, „klang” obręczy, stuk kręgli.
    /// Dla kuli do kręgli dodatkowo ciągły szum toczenia, którego głośność zależy od prędkości.
    /// </summary>
    public class ImpactSound : MonoBehaviour
    {
        public ImpactKind kind = ImpactKind.Basketball;
        [Tooltip("Prędkość zderzenia (m/s), przy której dźwięk ma pełną głośność.")]
        public float fullAt = 6f;
        public float minInterval = 0.06f;

        float lastTime = -1f;
        AudioSource rolling;
        Rigidbody body;
        bool grounded;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (kind == ImpactKind.BowlingBall)
            {
                rolling = gameObject.AddComponent<AudioSource>();
                rolling.clip = Sfx.RollLoop;
                rolling.loop = true;
                rolling.playOnAwake = false;
                rolling.spatialBlend = 1f;
                rolling.minDistance = 1f;
                rolling.maxDistance = 25f;
                rolling.rolloffMode = AudioRolloffMode.Linear;
                rolling.volume = 0f;
            }
        }

        void OnCollisionEnter(Collision c)
        {
            float speed = c.relativeVelocity.magnitude;
            if (speed < 0.4f || Time.time - lastTime < minInterval) return;
            lastTime = Time.time;
            float vol = Mathf.Clamp01(speed / fullAt);
            Vector3 p = c.contactCount > 0 ? c.GetContact(0).point : transform.position;

            if (c.collider.GetComponentInParent<RimPart>() != null)
            {
                Sfx.PlayAt(Sfx.Rim, p, vol, Random.Range(0.95f, 1.05f));
                return;
            }
            switch (kind)
            {
                case ImpactKind.Basketball:
                    Sfx.PlayAt(Sfx.Bounce, p, vol, Random.Range(0.93f, 1.07f));
                    break;
                case ImpactKind.BowlingBall:
                    if (c.collider.GetComponentInParent<BowlingPin>() != null) Sfx.PlayAt(Sfx.PinHit, p, Mathf.Max(0.5f, vol), Random.Range(0.9f, 1.1f));
                    else Sfx.PlayAt(Sfx.Thud, p, vol * 0.8f, Random.Range(0.9f, 1.05f));
                    break;
                default:
                    Sfx.PlayAt(Sfx.PinHit, p, vol * 0.8f, Random.Range(0.85f, 1.15f));
                    break;
            }
        }

        void OnCollisionStay(Collision c)
        {
            grounded = true;
        }

        void FixedUpdate()
        {
            if (rolling == null) return;
            float speed = body != null && !body.isKinematic ? body.linearVelocity.magnitude : 0f;
            float target = grounded ? Mathf.Clamp01(speed / 7f) * 0.7f : 0f;
            rolling.volume = Mathf.MoveTowards(rolling.volume, target, Time.fixedDeltaTime * 3f);
            rolling.pitch = 0.7f + Mathf.Clamp01(speed / 8f) * 0.6f;
            if (rolling.volume > 0.01f && !rolling.isPlaying) rolling.Play();
            else if (rolling.volume <= 0.01f && rolling.isPlaying) rolling.Stop();
            grounded = false;
        }
    }
}
