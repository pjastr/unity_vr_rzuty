using UnityEngine;
using UnityEngine.UI;

namespace VRThrowing
{
    /// <summary>
    /// Przycisk menu w przestrzeni 3D: obrazek UI + BoxCollider (trigger), w który celuje promień <see cref="UiPointer"/>.
    /// Co robi przycisk, określają pola: opcja + krok, preset albo zamknięcie menu.
    /// </summary>
    public class MenuButton : MonoBehaviour
    {
        [Tooltip("Indeks opcji z ThrowSettings (-1 = brak).")]
        public int option = -1;
        [Tooltip("Krok zmiany opcji: -1 (poprzednia wartość) albo +1 (następna).")]
        public int delta = 1;
        [Tooltip("Indeks presetu (-1 = brak).")]
        public int preset = -1;
        public bool closesMenu;

        public Image background;
        public Color normalColor = new Color(0.18f, 0.22f, 0.3f, 1f);
        public Color hoverColor = new Color(0.25f, 0.55f, 0.95f, 1f);

        public void SetHover(bool hover)
        {
            if (background != null) background.color = hover ? hoverColor : normalColor;
        }

        public void Click()
        {
            if (ThrowMenu.Instance != null) ThrowMenu.Instance.OnButton(this);
        }
    }
}
