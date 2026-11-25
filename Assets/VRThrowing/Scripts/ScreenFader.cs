using System.Collections;
using UnityEngine;

namespace VRThrowing
{
    /// <summary>
    /// Zaciemnienie całego obrazu („mrugnięcie”): czarny prostokąt tuż przed oczami, przypięty do CenterEyeAnchor.
    /// Używane przy teleporcie, obrocie skokowym z mrugnięciem i powrocie do punktu kontrolnego.
    /// Materiał (URP Unlit, przezroczysty) tworzy generator sceny, żeby wariant shadera trafił do buildu.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public Material overlayMaterial;
        public Color color = Color.black;

        public float Alpha { get; private set; }

        Renderer quad;
        Material material;
        Transform attachedTo;

        /// <summary>Płynna zmiana krycia. Użycie w korutynie: yield return fader.Fade(1f, 0.1f);</summary>
        public IEnumerator Fade(float target, float duration)
        {
            float start = Alpha;
            if (duration <= 0f)
            {
                SetAlpha(target);
                yield break;
            }
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(start, target, t / duration));
                yield return null;
            }
            SetAlpha(target);
        }

        public void SetAlpha(float a)
        {
            Alpha = Mathf.Clamp01(a);
            if (!EnsureQuad()) return;
            quad.enabled = Alpha > 0.001f;
            Color c = color;
            c.a = Alpha;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);
            if (material.HasProperty("_Color")) material.SetColor("_Color", c);
        }

        void LateUpdate()
        {
            // Rig mógł się pojawić później niż ten obiekt - przypinamy się, gdy tylko jest głowa.
            if (attachedTo == null) SetAlpha(Alpha);
        }

        bool EnsureQuad()
        {
            Transform head = PlayerRig.Head;
            if (head == null || overlayMaterial == null) return false;
            if (quad != null && attachedTo == head) return true;

            if (quad == null)
            {
                var go = new GameObject("ScreenFade");
                var mesh = new Mesh { name = "ScreenFadeQuad" };
                mesh.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                };
                mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
                mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                quad = go.AddComponent<MeshRenderer>();
                material = new Material(overlayMaterial);
                quad.sharedMaterial = material;
                quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                quad.receiveShadows = false;
            }

            attachedTo = head;
            float d = OverlayDistance(head, 0.12f);
            quad.transform.SetParent(head, false);
            quad.transform.localPosition = new Vector3(0f, 0f, d);
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(d * 12f, d * 12f, 1f); // pokrywa pole widzenia z zapasem
            quad.enabled = Alpha > 0.001f;
            return true;
        }

        /// <summary>Odległość nakładki od oka - tuż za bliską płaszczyzną odcięcia kamery.</summary>
        public static float OverlayDistance(Transform head, float minimum)
        {
            var cam = head.GetComponent<Camera>();
            float near = cam != null ? cam.nearClipPlane : 0.05f;
            return Mathf.Max(minimum, near + 0.03f);
        }
    }
}
