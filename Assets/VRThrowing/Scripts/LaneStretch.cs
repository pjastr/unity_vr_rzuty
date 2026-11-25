using UnityEngine;

namespace VRThrowing
{
    /// <summary>
    /// Element toru rozciągany przy zmianie długości (deska toru, rynny, bandy, ściany).
    /// Prostopadłościan biegnie wzdłuż lokalnej osi Z od z0 do (długość toru + endOffset).
    /// </summary>
    public class LaneStretch : MonoBehaviour
    {
        public float z0;
        public float endOffset;

        public void Apply(float laneLength)
        {
            float z1 = laneLength + endOffset;
            float len = Mathf.Max(0.01f, z1 - z0);
            Vector3 p = transform.localPosition;
            transform.localPosition = new Vector3(p.x, p.y, z0 + len * 0.5f);
            Vector3 s = transform.localScale;
            transform.localScale = new Vector3(s.x, s.y, len);
        }
    }
}
