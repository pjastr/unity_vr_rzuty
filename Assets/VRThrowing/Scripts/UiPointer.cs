using UnityEngine;

namespace VRThrowing
{
    /// <summary>
    /// Promień z prawego kontrolera do obsługi menu ustawień (tylko gdy menu jest otwarte).
    /// Trafia w kolidery-triggery przycisków (<see cref="MenuButton"/>); trigger prawego kontrolera = kliknięcie.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public class UiPointer : MonoBehaviour
    {
        public LineRenderer line;
        public Transform dot;
        public float maxDistance = 3f;
        [Tooltip("Korekta kierunku promienia względem kontrolera (stopnie).")]
        public Vector3 aimEuler = Vector3.zero;
        public LayerMask mask = ~(1 << 2);

        MenuButton hovered;

        void Update()
        {
            var menu = ThrowMenu.Instance;
            Transform c = PlayerRig.Controller(HandSide.Right);
            bool wanted = menu != null && menu.IsOpen;
            bool active = wanted && c != null && PlayerRig.IsConnected(HandSide.Right);
            if (!active)
            {
                SetVisible(false);
                SetHovered(null);
                return;
            }

            Vector3 origin = c.position;
            Vector3 dir = c.rotation * Quaternion.Euler(aimEuler) * Vector3.forward;
            Vector3 end = origin + dir * maxDistance;
            MenuButton button = null;
            bool hit = false;

            RaycastHit info;
            if (Physics.Raycast(origin, dir, out info, maxDistance, mask, QueryTriggerInteraction.Collide))
            {
                end = info.point;
                hit = true;
                button = info.collider.GetComponent<MenuButton>();
            }

            SetHovered(button);
            SetVisible(true);
            if (line != null)
            {
                line.positionCount = 2;
                line.SetPosition(0, origin);
                line.SetPosition(1, end);
            }
            if (dot != null)
            {
                dot.gameObject.SetActive(hit);
                dot.position = end;
            }

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                if (button != null)
                {
                    button.Click();
                    Haptics.Pulse(HandSide.Right, 0.35f, 0.04f);
                }
            }
        }

        void SetHovered(MenuButton b)
        {
            if (b == hovered) return;
            if (hovered != null) hovered.SetHover(false);
            hovered = b;
            if (hovered != null)
            {
                hovered.SetHover(true);
                Haptics.Pulse(HandSide.Right, 0.12f, 0.02f);
            }
        }

        void SetVisible(bool v)
        {
            if (line != null && line.enabled != v) line.enabled = v;
            if (!v && dot != null && dot.gameObject.activeSelf) dot.gameObject.SetActive(false);
        }
    }
}
