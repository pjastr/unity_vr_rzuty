using UnityEngine;

namespace VRThrowing
{
    public enum HandSide { Left, Right }

    /// <summary>
    /// Dostęp do OVRCameraRig (wyszukiwany automatycznie) oraz do głowy, kontrolerów i przestrzeni śledzenia.
    /// Wszystkie skrypty projektu korzystają z tej klasy zamiast samodzielnie szukać rigu.
    /// </summary>
    public static class PlayerRig
    {
        static OVRCameraRig rig;
        static float nextFind;

        public static OVRCameraRig Rig
        {
            get
            {
                if (rig == null && Time.unscaledTime >= nextFind)
                {
                    nextFind = Time.unscaledTime + 0.5f;
                    rig = Object.FindAnyObjectByType<OVRCameraRig>();
                }
                return rig;
            }
        }

        /// <summary>Głowa gracza (CenterEyeAnchor).</summary>
        public static Transform Head
        {
            get
            {
                var r = Rig;
                return r != null ? r.centerEyeAnchor : null;
            }
        }

        /// <summary>Punkt zaczepienia kontrolera (LeftControllerAnchor / RightControllerAnchor).</summary>
        public static Transform Controller(HandSide side)
        {
            var r = Rig;
            if (r == null) return null;
            Transform t = side == HandSide.Left ? r.leftControllerAnchor : r.rightControllerAnchor;
            if (t == null) t = side == HandSide.Left ? r.leftHandAnchor : r.rightHandAnchor;
            return t;
        }

        public static OVRInput.Controller Mask(HandSide side) =>
            side == HandSide.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;

        public static bool IsConnected(HandSide side) => OVRInput.IsControllerConnected(Mask(side));

        public static Vector2 Stick(HandSide side) => OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, Mask(side));

        /// <summary>TrackingSpace - układ, w którym OVRInput podaje pozycje i prędkości kontrolerów.</summary>
        public static Transform TrackingSpace
        {
            get
            {
                var r = Rig;
                return r != null ? r.trackingSpace : null;
            }
        }

        /// <summary>Punkt na podłodze pod głową gracza (wysokość = poziom stóp, czyli korzeń rigu).</summary>
        public static bool TryGetFeet(out Vector3 feet)
        {
            feet = Vector3.zero;
            var r = Rig;
            Transform head = Head;
            if (r == null || head == null) return false;
            feet = new Vector3(head.position.x, r.transform.position.y, head.position.z);
            return true;
        }

        /// <summary>Wektor rzutowany na płaszczyznę poziomą i znormalizowany.</summary>
        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            rig = null;
            nextFind = 0f;
        }
    }
}
