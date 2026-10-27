using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Knoppen voor een aanraakscherm (story 14.2), wat op een pc het toetsenbord doet: Start (het startscherm, T),
    /// Terug (sluit wat open staat, Escape), Rem (ingedrukt houden, spatiebalk) en met de simulator het vermogen
    /// ±25 W (pijltjes). Pauzeren kan al met de knop rechtsboven. Verschijnt alleen als er een aanraakscherm is.
    /// </summary>
    public sealed class TouchBar : MonoBehaviour
    {
        const float ButtonWidth = 132f, ButtonHeight = 84f, Gap = 12f, PowerStep = 25f;

        RideController _ride;
        GameObject _powerButtons;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<RideController>() == null || FindAnyObjectByType<TouchBar>() != null) return;
            if (Touchscreen.current == null && !Application.isMobilePlatform) return;
            new GameObject("Aanraakknoppen").AddComponent<TouchBar>();
        }

        void Awake()
        {
            _ride = FindAnyObjectByType<RideController>();
            UiFactory.EnsureEventSystem();
            Build();
        }

        void Update() => _powerButtons.SetActive(_ride.Trainer == _ride.Simulator);

        void Build()
        {
            var canvas = UiFactory.Canvas(transform, "Aanraak Canvas", 40);

            // Linkerrand, midden: naast de HUD-tegels en vrij van het hoogteprofiel rechtsonder.
            var bar = new GameObject("Knoppen", typeof(RectTransform)).GetComponent<RectTransform>();
            bar.SetParent(canvas, false);
            bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0f, 0.5f);
            bar.anchoredPosition = new Vector2(16f, 0f);
            bar.sizeDelta = new Vector2(ButtonWidth, 5 * ButtonHeight + 4 * Gap);
            // Op een telefoon (×1,67) worden de knoppen ±8 mm hoog: groot genoeg om op de fiets te raken.
            bar.gameObject.AddComponent<ScreenFit>().BaseScale = 0.8f;

            float y = 0f;
            Button(bar, "Start", ref y, OpenHome);
            Button(bar, "Terug", ref y, Back);
            var (brake, _) = Button(bar, "Rem", ref y, null);
            brake.GetComponent<Image>().color = new Color(0.7f, 0.2f, 0.15f, 1f);
            var hold = brake.gameObject.AddComponent<HoldButton>();
            hold.Changed += pressed => _ride.Brake = pressed ? 1f : 0f;

            _powerButtons = new GameObject("Vermogen", typeof(RectTransform));
            var power = (RectTransform)_powerButtons.transform;
            power.SetParent(bar, false);
            power.anchorMin = Vector2.zero;
            power.anchorMax = Vector2.one;
            power.offsetMin = power.offsetMax = Vector2.zero;
            Button(power, $"+{PowerStep:0} W", ref y, () => _ride.Simulator.TargetPowerWatts += PowerStep);
            Button(power, $"−{PowerStep:0} W", ref y, () => _ride.Simulator.TargetPowerWatts -= PowerStep);
        }

        static (Button button, Text label) Button(RectTransform parent, string text, ref float y,
            UnityEngine.Events.UnityAction onClick)
        {
            var made = UiFactory.Button(parent, text, new Vector2(0f, y), ButtonWidth, ButtonHeight, onClick);
            made.label.text = text;
            made.label.fontSize = 26;
            y -= ButtonHeight + Gap;
            return made;
        }

        void OpenHome()
        {
            Back();
            var home = FindAnyObjectByType<HomeScreen>();
            if (home != null && !home.IsOpen) home.Open();
        }

        /// <summary>Sluit alle open panelen, zoals Escape.</summary>
        static void Back()
        {
            var open = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IPanel>().Where(p => p.IsOpen);
            foreach (var panel in open.ToList()) panel.Close();
        }
    }
}
