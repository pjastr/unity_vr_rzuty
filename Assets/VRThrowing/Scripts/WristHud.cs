using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace VRThrowing
{
    /// <summary>
    /// Mały wyświetlacz nad lewym kontrolerem: ustawienia rzutu i pomiary ostatniego rzutu -
    /// prędkość według wszystkich trzech metod (OVRInput / historia / ostatnia klatka), kąt wyrzutu, spin.
    /// Dzięki temu widać, jak bardzo różnią się metody szacowania prędkości.
    /// </summary>
    public class WristHud : MonoBehaviour
    {
        public Text text;
        public float refreshInterval = 0.15f;

        float next;
        readonly StringBuilder sb = new StringBuilder(320);

        void Update()
        {
            if (text == null || Time.unscaledTime < next) return;
            next = Time.unscaledTime + refreshInterval;

            var s = ThrowSettings.Instance;
            var g = Grabber.Instance;
            var mover = PlayerMover.Instance;
            sb.Length = 0;

            if (s != null)
            {
                sb.Append("<b>").Append(ThrowSettings.Name(mover != null ? mover.Current : s.station)).Append("</b>  ");
                sb.Append(ThrowSettings.Name(s.velocitySource)).Append(' ').Append(s.OptionValue(ThrowSettings.OptMultiplier));
                if (s.assistIndex > 0) sb.Append("  pomoc: ").Append(s.OptionValue(ThrowSettings.OptAssist));
                if (s.gravityIndex > 0) sb.Append("  <color=#9CF>g=").Append(s.Gravity.ToString("0.00").Replace('.', ',')).Append("</color>");
                sb.Append('\n');
            }

            if (g != null && g.LastThrow.valid)
            {
                var t = g.LastThrow;
                sb.Append("Rzut: <b>").Append(F(t.finalSpeed)).Append(" m/s</b>  kąt ").Append(Mathf.RoundToInt(t.elevationDeg)).Append("°");
                if (t.spinRpm > 1f) sb.Append("  spin ").Append(Mathf.RoundToInt(t.spinRpm)).Append(" obr/min");
                if (t.assisted) sb.Append("  <color=#FC6>(pomoc)</color>");
                sb.Append('\n');
                sb.Append("<size=26>OVR ").Append(F(t.speedOvr)).Append("  ·  hist. ").Append(F(t.speedHistory))
                  .Append("  ·  klatka ").Append(F(t.speedLastFrame)).Append(" m/s");
                if (t.usedFallback) sb.Append("  <color=#FC6>(OVR=0 → historia)</color>");
                sb.Append("</size>\n");
            }
            else
            {
                sb.Append("Złap piłkę gripem i rzuć.\n");
            }

            if (mover != null && mover.Current == Station.Bowling)
            {
                if (BowlingLane.Instance != null) sb.Append(BowlingLane.Instance.StatusLine);
            }
            else if (BasketballGame.Instance != null)
            {
                sb.Append(BasketballGame.Instance.StatusLine);
            }

            text.text = sb.ToString();
        }

        static string F(float v) => v.ToString("0.0").Replace('.', ',');
    }
}
