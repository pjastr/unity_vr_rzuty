using UnityEngine;

namespace VRThrowing
{
    /// <summary>
    /// Pomoc w celowaniu - częściowa korekta prędkości wyrzutu w stronę celu.
    ///  - Kosz: rozwiązanie rzutu ukośnego. Przy zachowanej wartości prędkości szukamy kąta wyrzutu,
    ///    przy którym piłka przejdzie przez środek obręczy (z dwóch rozwiązań - płaskiego i wysokiego -
    ///    wybieramy bliższe kątowi gracza), i mieszamy z prędkością gracza w proporcji „siła pomocy”.
    ///  - Kręgle: obrót składowej poziomej w stronę pierwszego kręgla.
    /// Pomoc nie działa, gdy rzut jest wyraźnie „obok” celu (więcej niż maxYawError stopni).
    /// </summary>
    public static class AimAssist
    {
        /// <summary>Środek obręczy (ustawia BasketballHoop).</summary>
        public static Transform BasketTarget;

        /// <summary>Pierwszy kręgiel (ustawia BowlingLane).</summary>
        public static Transform BowlingTarget;

        public static float maxYawError = 25f;

        public static Vector3 Apply(Throwable ball, Vector3 position, Vector3 velocity, float strength)
        {
            if (ball == null || strength <= 0f) return velocity;
            if (ball.kind == BallKind.Basketball && BasketTarget != null)
                return Basket(position, velocity, BasketTarget.position + Vector3.up * 0.03f, strength, -Physics.gravity.y);
            if (ball.kind == BallKind.Bowling && BowlingTarget != null)
                return Bowling(position, velocity, BowlingTarget.position, strength);
            return velocity;
        }

        public static Vector3 Basket(Vector3 p, Vector3 v, Vector3 target, float strength, float g)
        {
            Vector3 to = target - p;
            var toFlat = new Vector3(to.x, 0f, to.z);
            var vFlat = new Vector3(v.x, 0f, v.z);
            float d = toFlat.magnitude;
            float h = to.y;
            if (d < 0.5f || vFlat.magnitude < 0.3f || g <= 0f) return v;
            if (Vector3.Angle(vFlat, toFlat) > maxYawError) return v;

            float speed = v.magnitude;
            float s2 = speed * speed;
            float disc = s2 * s2 - g * (g * d * d + 2f * h * s2);
            if (disc < 0f)
            {
                // Za słaby rzut: minimalna prędkość, przy której da się trafić. Pomagamy tylko, gdy brakuje niewiele.
                float needed = Mathf.Sqrt(g * (h + Mathf.Sqrt(h * h + d * d)));
                if (speed < needed * 0.8f) return v;
                speed = needed;
                s2 = speed * speed;
                disc = 0f;
            }
            float root = Mathf.Sqrt(Mathf.Max(0f, disc));
            float low = Mathf.Atan((s2 - root) / (g * d));
            float high = Mathf.Atan((s2 + root) / (g * d));
            float user = Mathf.Atan2(v.y, vFlat.magnitude);
            float angle = Mathf.Abs(low - user) < Mathf.Abs(high - user) ? low : high;

            Vector3 dir = toFlat / d * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
            return Vector3.Lerp(v, dir * speed, Mathf.Clamp01(strength));
        }

        public static Vector3 Bowling(Vector3 p, Vector3 v, Vector3 target, float strength)
        {
            var vFlat = new Vector3(v.x, 0f, v.z);
            var toFlat = new Vector3(target.x - p.x, 0f, target.z - p.z);
            if (vFlat.magnitude < 0.3f || toFlat.magnitude < 1f) return v;
            float yaw = Vector3.SignedAngle(vFlat, toFlat, Vector3.up);
            if (Mathf.Abs(yaw) > maxYawError) return v;
            Vector3 rotated = Quaternion.AngleAxis(yaw * Mathf.Clamp01(strength), Vector3.up) * vFlat;
            return new Vector3(rotated.x, v.y, rotated.z);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            BasketTarget = null;
            BowlingTarget = null;
        }
    }
}
