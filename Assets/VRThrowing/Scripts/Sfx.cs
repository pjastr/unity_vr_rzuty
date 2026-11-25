using System;
using UnityEngine;

namespace VRThrowing
{
    /// <summary>
    /// Dźwięki generowane w kodzie (synteza) - projekt nie potrzebuje plików audio.
    ///  - Play: dźwięk 2D (interfejs, komunikaty),
    ///  - PlayAt: dźwięk 3D w punkcie (odbicia, kręgle) - mała pula źródeł, bez tworzenia obiektów co uderzenie.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;
        const float TwoPi = Mathf.PI * 2f;
        const int PoolSize = 10;

        static AudioSource ui;
        static AudioSource[] pool;
        static int next;
        static AudioClip click, toggle, grab, whoosh, pop, bounce, rim, swish, score, buzzer, pinHit, thud, strike, rollLoop;

        public static AudioClip Click => click != null ? click : click = Make("click", 0.05f, (t, n) => Mathf.Sin(TwoPi * 1800f * t) * Mathf.Exp(-t * 120f) * 0.6f);
        public static AudioClip Toggle => toggle != null ? toggle : toggle = Make("toggle", 0.16f, (t, n) => Arp(t, 0.07f, 30f, 880f, 1320f));
        public static AudioClip Grab => grab != null ? grab : grab = Make("grab", 0.08f, (t, n) => (n * 0.3f + Mathf.Sin(TwoPi * 220f * t) * 0.5f) * Mathf.Exp(-t * 50f));
        public static AudioClip Whoosh => whoosh != null ? whoosh : whoosh = Make("whoosh", 0.35f, WhooshWave);
        public static AudioClip Pop => pop != null ? pop : pop = Make("pop", 0.08f, (t, n) => Mathf.Sin(SweepPhase(t, 900f, 400f, 0.08f)) * Mathf.Exp(-t * 40f) * 0.5f);
        public static AudioClip Bounce => bounce != null ? bounce : bounce = Make("bounce", 0.25f, BounceWave);
        public static AudioClip Rim => rim != null ? rim : rim = Make("rim", 0.6f, RimWave);
        public static AudioClip Swish => swish != null ? swish : swish = Make("swish", 0.4f, SwishWave);
        public static AudioClip Score => score != null ? score : score = Make("score", 0.45f, (t, n) => Arp(t, 0.1f, 9f, 659f, 784f, 1047f, 1319f));
        public static AudioClip Buzzer => buzzer != null ? buzzer : buzzer = Make("buzzer", 0.8f, (t, n) => (Square(TwoPi * 196f * t) * 0.18f + Square(TwoPi * 247f * t) * 0.12f) * Mathf.Clamp01((0.8f - t) * 10f));
        public static AudioClip PinHit => pinHit != null ? pinHit : pinHit = Make("pin", 0.22f, PinWave);
        public static AudioClip Thud => thud != null ? thud : thud = Make("thud", 0.3f, (t, n) => (Mathf.Sin(TwoPi * 70f * t) * 0.7f + n * 0.2f) * Mathf.Exp(-t * 18f));
        public static AudioClip Strike => strike != null ? strike : strike = Make("strike", 1.0f, (t, n) => Arp(t, 0.11f, 5f, 523f, 659f, 784f, 1047f, 1319f, 1568f, 2093f));
        public static AudioClip RollLoop => rollLoop != null ? rollLoop : rollLoop = MakeLoop("roll", 1f);

        public static void Warmup()
        {
            _ = Click; _ = Toggle; _ = Grab; _ = Whoosh; _ = Pop; _ = Bounce; _ = Rim; _ = Swish; _ = Score;
            _ = Buzzer; _ = PinHit; _ = Thud; _ = Strike; _ = RollLoop;
        }

        public static void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            if (ui == null)
            {
                var go = new GameObject("Sfx_UI");
                ui = go.AddComponent<AudioSource>();
                ui.playOnAwake = false;
                ui.spatialBlend = 0f;
            }
            ui.PlayOneShot(clip, volume);
        }

        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || volume <= 0.01f) return;
            if (pool == null)
            {
                var root = new GameObject("Sfx_3D");
                pool = new AudioSource[PoolSize];
                for (int i = 0; i < PoolSize; i++)
                {
                    var go = new GameObject("Source_" + i);
                    go.transform.SetParent(root.transform, false);
                    var src = go.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    src.spatialBlend = 1f;
                    src.minDistance = 1.5f;
                    src.maxDistance = 30f;
                    src.rolloffMode = AudioRolloffMode.Linear;
                    pool[i] = src;
                }
            }
            var s = pool[next];
            next = (next + 1) % PoolSize;
            s.transform.position = position;
            s.pitch = pitch;
            s.clip = clip;
            s.volume = Mathf.Clamp01(volume);
            s.Play();
        }

        // ------------------------------------------------------------------ fale

        static float WhooshWave(float t, float n)
        {
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.35f));
            return n * env * 0.35f * (0.5f + 0.5f * Mathf.Sin(SweepPhase(t, 200f, 900f, 0.35f)));
        }

        // Piłka do kosza: niski „bum” z krótkim atakiem.
        static float BounceWave(float t, float n)
        {
            float env = Mathf.Exp(-t * 22f);
            return (Mathf.Sin(SweepPhase(t, 160f, 90f, 0.25f)) * 0.8f + n * 0.15f * Mathf.Exp(-t * 80f)) * env;
        }

        // Obręcz: metal - nieharmoniczne alikwoty o długim wybrzmieniu.
        static float RimWave(float t, float n)
        {
            float a = Mathf.Sin(TwoPi * 820f * t) * Mathf.Exp(-t * 7f);
            float b = Mathf.Sin(TwoPi * 1370f * t) * Mathf.Exp(-t * 9f) * 0.6f;
            float c = Mathf.Sin(TwoPi * 2210f * t) * Mathf.Exp(-t * 12f) * 0.4f;
            return (a + b + c) * 0.35f + n * Mathf.Exp(-t * 150f) * 0.3f;
        }

        // Siatka: krótki szum.
        static float SwishWave(float t, float n) => n * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.4f)) * 0.45f;

        // Kręgiel: drewniany stuk.
        static float PinWave(float t, float n)
        {
            float tone = Mathf.Sin(TwoPi * 1250f * t) * Mathf.Exp(-t * 35f) + Mathf.Sin(TwoPi * 1890f * t) * Mathf.Exp(-t * 45f) * 0.5f;
            return (tone * 0.45f + n * Mathf.Exp(-t * 60f) * 0.5f) * 0.8f;
        }

        // ------------------------------------------------------------------ narzędzia

        static AudioClip Make(string name, float seconds, Func<float, float, float> wave)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var rng = new System.Random(name.Length * 7919 + name[0]);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float fade = Mathf.Clamp01((n - i) / (Rate * 0.005f));
                data[i] = Mathf.Clamp(wave(t, noise) * fade, -1f, 1f) * 0.8f;
            }
            var clip = AudioClip.Create("sfx_" + name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Pętla szumu „toczenia” (filtrowany szum, początek i koniec się łączą).</summary>
        static AudioClip MakeLoop(string name, float seconds)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var rng = new System.Random(4242);
            float y = 0f;
            for (int pass = 0; pass < 2; pass++) // dwa przebiegi - stan filtra na początku = stan na końcu
            {
                for (int i = 0; i < n; i++)
                {
                    float noise = (float)rng.NextDouble() * 2f - 1f;
                    y += (noise - y) * 0.02f; // prosty filtr dolnoprzepustowy
                    if (pass == 1) data[i] = Mathf.Clamp(y * 6f, -1f, 1f) * 0.6f;
                }
            }
            var clip = AudioClip.Create("sfx_" + name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float SweepPhase(float t, float f0, float f1, float T) => TwoPi * (f0 * t + (f1 - f0) * t * t / (2f * T));

        static float Square(float phase) => Mathf.Sin(phase) >= 0f ? 1f : -1f;

        static float Arp(float t, float step, float decay, params float[] notes)
        {
            int idx = Mathf.Min((int)(t / step), notes.Length - 1);
            float local = t - idx * step;
            float f = notes[idx];
            return (Mathf.Sin(TwoPi * f * t) * 0.6f + Mathf.Sin(TwoPi * f * 2f * t) * 0.15f) * Mathf.Exp(-local * decay);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ui = null;
            pool = null;
            next = 0;
            click = toggle = grab = whoosh = pop = bounce = rim = swish = score = buzzer = pinHit = thud = strike = rollLoop = null;
        }
    }
}
