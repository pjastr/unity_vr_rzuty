using UnityEngine;

namespace VRThrowing
{
    /// <summary>
    /// Krótkie impulsy wibracji: OVRInput.SetControllerVibration(częstotliwość, amplituda, kontroler).
    /// Wibracja w OVRInput trwa do wyłączenia (albo ok. 2 s), więc ten komponent sam ją wyłącza po czasie impulsu.
    /// </summary>
    public class Haptics : MonoBehaviour
    {
        static Haptics instance;

        [Range(0f, 1f)] public float frequency = 0.6f;
        [Tooltip("Mnożnik wszystkich wibracji (0 = wyłączone).")]
        [Range(0f, 1f)] public float masterAmplitude = 1f;

        readonly float[] endTime = { -1f, -1f };
        readonly bool[] active = new bool[2];

        void Awake() => instance = this;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void OnDisable()
        {
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        }

        public static void Pulse(HandSide side, float amplitude = 0.5f, float duration = 0.06f)
        {
            if (instance == null || instance.masterAmplitude <= 0f) return;
            int i = (int)side;
            OVRInput.SetControllerVibration(instance.frequency, Mathf.Clamp01(amplitude * instance.masterAmplitude), PlayerRig.Mask(side));
            instance.endTime[i] = Time.unscaledTime + Mathf.Max(0.02f, duration);
            instance.active[i] = true;
        }

        public static void PulseBoth(float amplitude = 0.5f, float duration = 0.08f)
        {
            Pulse(HandSide.Left, amplitude, duration);
            Pulse(HandSide.Right, amplitude, duration);
        }

        void LateUpdate()
        {
            for (int i = 0; i < 2; i++)
            {
                if (!active[i] || Time.unscaledTime < endTime[i]) continue;
                OVRInput.SetControllerVibration(0f, 0f, PlayerRig.Mask((HandSide)i));
                active[i] = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;
    }
}
