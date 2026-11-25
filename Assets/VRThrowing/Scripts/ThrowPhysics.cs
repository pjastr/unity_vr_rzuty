using UnityEngine;

namespace VRThrowing
{
    /// <summary>
    /// Ustawienia fizyki dla rzutów:
    ///  - krok fizyki 1/90 s (domyślne 1/50 s to za mało dla szybkiej piłki i cienkiej obręczy),
    ///  - więcej iteracji solvera (stabilne stosy kręgli),
    ///  - grawitacja z ustawień (Ziemia / Mars / Księżyc).
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class ThrowPhysics : MonoBehaviour
    {
        [Tooltip("Częstotliwość kroku fizyki (Hz).")]
        public float physicsRate = 90f;
        public int solverIterations = 10;
        public int solverVelocityIterations = 4;

        void Awake()
        {
            Time.fixedDeltaTime = 1f / Mathf.Max(30f, physicsRate);
            Physics.defaultSolverIterations = solverIterations;
            Physics.defaultSolverVelocityIterations = solverVelocityIterations;
        }

        void Start()
        {
            if (ThrowSettings.Instance != null) ThrowSettings.Instance.Changed += Apply;
            Apply();
        }

        void OnDestroy()
        {
            if (ThrowSettings.Instance != null) ThrowSettings.Instance.Changed -= Apply;
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
        }

        void Apply()
        {
            var s = ThrowSettings.Instance;
            float g = s != null ? s.Gravity : 9.81f;
            if (Mathf.Abs(Physics.gravity.y + g) > 0.001f) Physics.gravity = new Vector3(0f, -g, 0f);
        }
    }
}
