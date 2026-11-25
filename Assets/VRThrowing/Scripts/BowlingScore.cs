using System.Collections.Generic;

namespace VRThrowing
{
    /// <summary>
    /// Punktacja kręgli (10 ramek) - czysta logika, bez Unity, więc łatwo ją przetestować.
    ///  - strike (X): 10 + dwa kolejne rzuty, spare (/): 10 + jeden kolejny rzut, inaczej suma kręgli,
    ///  - w 10. ramce po strike'u lub spare'ie przysługują rzuty dodatkowe (razem maks. 3).
    /// </summary>
    public class BowlingScore
    {
        public const int Frames = 10;

        readonly List<int> rolls = new List<int>();

        public IList<int> Rolls => rolls.AsReadOnly();

        public void Clear() => rolls.Clear();

        /// <summary>Dodaje rzut (liczba przewróconych kręgli, przycinana do liczby stojących).</summary>
        public void Roll(int pins)
        {
            if (GameOver) return;
            if (pins < 0) pins = 0;
            int standing = PinsStanding;
            if (pins > standing) pins = standing;
            rolls.Add(pins);
        }

        // ------------------------------------------------------------------ podział na ramki

        /// <summary>Rzuty w ramkach i indeks pierwszego rzutu każdej ramki.</summary>
        void Split(List<int>[] frames, int[] starts)
        {
            int i = 0, n = rolls.Count;
            for (int f = 0; f < Frames; f++)
            {
                frames[f] = new List<int>(3);
                starts[f] = i;
                if (f < Frames - 1)
                {
                    if (i < n)
                    {
                        int first = rolls[i++];
                        frames[f].Add(first);
                        if (first < 10 && i < n) frames[f].Add(rolls[i++]);
                    }
                }
                else
                {
                    while (i < n && frames[f].Count < 3) frames[f].Add(rolls[i++]);
                }
            }
        }

        static bool Complete(int f, List<int> fr)
        {
            if (f < Frames - 1) return fr.Count == 2 || (fr.Count == 1 && fr[0] == 10);
            if (fr.Count == 3) return true;
            return fr.Count == 2 && fr[0] + fr[1] < 10;
        }

        List<int>[] SplitFrames(out int[] starts)
        {
            var frames = new List<int>[Frames];
            starts = new int[Frames];
            Split(frames, starts);
            return frames;
        }

        public bool GameOver
        {
            get
            {
                int[] s;
                var fr = SplitFrames(out s);
                return Complete(Frames - 1, fr[Frames - 1]);
            }
        }

        /// <summary>Bieżąca ramka 0..9 (10 = koniec gry).</summary>
        public int CurrentFrame
        {
            get
            {
                int[] s;
                var fr = SplitFrames(out s);
                for (int f = 0; f < Frames; f++)
                    if (!Complete(f, fr[f])) return f;
                return Frames;
            }
        }

        /// <summary>Numer rzutu w bieżącej ramce (0, 1, 2).</summary>
        public int RollInFrame
        {
            get
            {
                int f = CurrentFrame;
                if (f >= Frames) return 0;
                int[] s;
                return SplitFrames(out s)[f].Count;
            }
        }

        /// <summary>Ile kręgli stoi przed następnym rzutem.</summary>
        public int PinsStanding
        {
            get
            {
                int f = CurrentFrame;
                if (f >= Frames) return 0;
                int[] s;
                var fr = SplitFrames(out s)[f];
                if (fr.Count == 0) return 10;
                if (f < Frames - 1) return 10 - fr[0];
                if (fr.Count == 1) return fr[0] == 10 ? 10 : 10 - fr[0];
                // dwa rzuty w 10. ramce
                if (fr[0] == 10) return fr[1] == 10 ? 10 : 10 - fr[1];
                return fr[0] + fr[1] == 10 ? 10 : 0;
            }
        }

        /// <summary>Czy przed następnym rzutem trzeba ustawić wszystkie 10 kręgli.</summary>
        public bool NeedsFullRack => !GameOver && PinsStanding == 10;

        /// <summary>Wynik narastająco po każdej ramce (null - jeszcze nie da się policzyć).</summary>
        public int?[] Totals()
        {
            int[] starts;
            var fr = SplitFrames(out starts);
            var result = new int?[Frames];
            int sum = 0;
            for (int f = 0; f < Frames; f++)
            {
                int? frameScore = FrameScore(f, fr[f], starts[f]);
                if (frameScore == null) break;
                sum += frameScore.Value;
                result[f] = sum;
            }
            return result;
        }

        int? FrameScore(int f, List<int> fr, int start)
        {
            if (!Complete(f, fr)) return null;
            if (f == Frames - 1)
            {
                int s = 0;
                foreach (int r in fr) s += r;
                return s;
            }
            if (fr[0] == 10) // strike
            {
                if (start + 2 >= rolls.Count) return null;
                return 10 + rolls[start + 1] + rolls[start + 2];
            }
            if (fr[0] + fr[1] == 10) // spare
            {
                if (start + 2 >= rolls.Count) return null;
                return 10 + rolls[start + 2];
            }
            return fr[0] + fr[1];
        }

        public int Total
        {
            get
            {
                var t = Totals();
                int last = 0;
                foreach (var v in t)
                    if (v.HasValue) last = v.Value;
                return last;
            }
        }

        /// <summary>Oznaczenia rzutów w ramce, np. "X", "7 /", "9 -", "X X 8".</summary>
        public string Marks(int f)
        {
            int[] s;
            var fr = SplitFrames(out s)[f];
            if (fr.Count == 0) return "";
            var parts = new List<string>(3);
            if (f < Frames - 1)
            {
                if (fr[0] == 10) return "X";
                parts.Add(Digit(fr[0]));
                if (fr.Count > 1) parts.Add(fr[0] + fr[1] == 10 ? "/" : Digit(fr[1]));
                return string.Join(" ", parts.ToArray());
            }

            // 10. ramka
            parts.Add(fr[0] == 10 ? "X" : Digit(fr[0]));
            if (fr.Count > 1)
            {
                if (fr[0] == 10) parts.Add(fr[1] == 10 ? "X" : Digit(fr[1]));
                else parts.Add(fr[0] + fr[1] == 10 ? "/" : Digit(fr[1]));
            }
            if (fr.Count > 2)
            {
                bool freshRack = (fr[0] == 10 && fr[1] == 10) || (fr[0] < 10 && fr[0] + fr[1] == 10);
                if (freshRack) parts.Add(fr[2] == 10 ? "X" : Digit(fr[2]));
                else parts.Add(fr[1] + fr[2] == 10 ? "/" : Digit(fr[2]));
            }
            return string.Join(" ", parts.ToArray());
        }

        static string Digit(int pins) => pins == 0 ? "-" : pins.ToString();
    }
}
