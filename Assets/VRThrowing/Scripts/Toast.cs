using UnityEngine;
using UnityEngine.UI;

namespace VRThrowing
{
    /// <summary>
    /// Krótki komunikat przed graczem (np. „Winieta: silna”). Panel podąża za wzrokiem z opóźnieniem
    /// (lazy follow) - nie jest sztywno przyklejony do głowy, co byłoby niekomfortowe.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class Toast : MonoBehaviour
    {
        static Toast instance;

        public Text text;
        public CanvasGroup group;
        public float distance = 1.4f;
        public float below = 0.35f;
        [Tooltip("Szybkość podążania za wzrokiem.")]
        public float follow = 3f;
        public float defaultDuration = 2.2f;
        public float fadeTime = 0.3f;

        float until;
        bool snap = true;

        void Awake()
        {
            instance = this;
            if (group != null) group.alpha = 0f;
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Show(string message, float duration = -1f)
        {
            if (instance == null) return;
            if (instance.text != null) instance.text.text = message;
            if (instance.group != null && instance.group.alpha < 0.05f) instance.snap = true;
            instance.until = Time.unscaledTime + (duration > 0f ? duration : instance.defaultDuration);
        }

        void LateUpdate()
        {
            Transform head = PlayerRig.Head;
            if (head == null) return;

            float remaining = until - Time.unscaledTime;
            float alpha = Mathf.Clamp01(remaining / Mathf.Max(0.01f, fadeTime));
            if (group != null) group.alpha = alpha;
            if (alpha <= 0f) return;

            Vector3 fwd = PlayerRig.Flat(head.forward);
            Vector3 targetPos = head.position + fwd * distance + Vector3.down * below;
            Quaternion targetRot = Quaternion.LookRotation(fwd, Vector3.up);
            if (snap)
            {
                transform.SetPositionAndRotation(targetPos, targetRot);
                snap = false;
            }
            else
            {
                float k = 1f - Mathf.Exp(-follow * Time.unscaledDeltaTime);
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, targetPos, k),
                    Quaternion.Slerp(transform.rotation, targetRot, k));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;
    }
}
