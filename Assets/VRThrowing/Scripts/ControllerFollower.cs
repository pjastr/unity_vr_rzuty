using UnityEngine;

namespace VRThrowing
{
    /// <summary>
    /// Przypina obiekt (model kontrolera z prymitywów, HUD na nadgarstku) do LeftControllerAnchor / RightControllerAnchor.
    /// W edytorze obiekt stoi w scenie (łatwo go oglądać i edytować), a po starcie sam przenosi się pod kontroler.
    /// Gdy kontroler jest odłożony / niepodłączony, model jest ukrywany.
    /// </summary>
    public class ControllerFollower : MonoBehaviour
    {
        public HandSide side = HandSide.Left;
        public Vector3 localPosition = Vector3.zero;
        public Vector3 localEuler = Vector3.zero;
        [Tooltip("Obiekt ukrywany, gdy kontroler nie jest podłączony.")]
        public GameObject visual;

        Transform anchor;

        void Update()
        {
            if (anchor == null)
            {
                anchor = PlayerRig.Controller(side);
                if (anchor == null) return;
                transform.SetParent(anchor, false);
                transform.localPosition = localPosition;
                transform.localRotation = Quaternion.Euler(localEuler);
            }

            if (visual != null)
            {
                bool connected = PlayerRig.IsConnected(side);
                if (visual.activeSelf != connected) visual.SetActive(connected);
            }
        }
    }
}
