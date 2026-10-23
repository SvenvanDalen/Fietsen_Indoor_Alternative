using FietsApp.Training;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Avatar en fiets kiezen (story 7.3): V opent het paneel. De camera draait om je renner heen en elke keuze is meteen
    /// te zien. Fiets, framekleur, shirt, band, helm en huidskleur gaan via <see cref="ProfileStore"/> in het profiel
    /// (en dus ook naar de Gist); de fiets telt met zijn gewicht en luchtweerstand mee in de fysica.
    /// De rit staat stil zolang het paneel open is.
    /// </summary>
    public sealed class AvatarView : MonoBehaviour
    {
        [SerializeField] RideController ride;
        [SerializeField] RiderView riderView;

        GameObject _panel;
        Text _bike, _bikeInfo, _frame, _jersey, _accent, _helmet, _skin;
        Image _frameSwatch, _jerseySwatch, _accentSwatch, _helmetSwatch, _skinSwatch;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        void Awake()
        {
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            if (riderView == null) riderView = FindAnyObjectByType<RiderView>();
            UiFactory.EnsureEventSystem();
            BuildUi();
            _panel.SetActive(false);
        }

        void Update()
        {
            var kb = UiFactory.IsTyping ? null : Keyboard.current;
            if (kb != null && kb.vKey.wasPressedThisFrame) { if (IsOpen) Close(); else Open(); return; }
            if (IsOpen && kb != null && kb.escapeKey.wasPressedThisFrame) Close();
        }

        public void Open()
        {
            _panel.SetActive(true);
            ride.Hold(this);
            riderView.Showcase = true;
            Refresh();
        }

        public void Close()
        {
            _panel.SetActive(false);
            ride.Release(this);
            riderView.Showcase = false;
        }

        void Change(System.Action<AvatarSettings> change)
        {
            var profile = ProfileStore.Current;
            profile.avatar ??= new AvatarSettings();
            change(profile.avatar);
            ProfileStore.Save();
            Refresh();
        }

        void Refresh()
        {
            var a = ProfileStore.Current.avatar ?? new AvatarSettings();
            var bike = Bike.Of(a.Bike);
            _bike.text = bike.Name;
            _bikeInfo.text = bike.Description;
            Show(_frame, _frameSwatch, a.frame, a.FrameColor);
            Show(_jersey, _jerseySwatch, a.jersey, a.JerseyColor);
            Show(_accent, _accentSwatch, a.accent, a.AccentColor);
            Show(_helmet, _helmetSwatch, a.helmet, a.HelmetColor);
            _skin.text = $"tint {AvatarSettings.Wrap(a.skin, AvatarSettings.SkinTones.Length) + 1}";
            _skinSwatch.color = Hex(a.SkinColor);
        }

        static void Show(Text label, Image swatch, int index, string hex)
        {
            label.text = AvatarSettings.ColorNames[AvatarSettings.Wrap(index, AvatarSettings.ColorNames.Length)];
            swatch.color = Hex(hex);
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;

        static int Step(int value, int delta, int count) => AvatarSettings.Wrap(value + delta, count);

        void BuildUi()
        {
            var canvas = UiFactory.Canvas(transform, "Avatar Canvas", 26);
            var panel = UiFactory.Panel(canvas, "Avatar", new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(470f, 560f));
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);
            _panel = panel.gameObject;

            UiFactory.Text(panel, "Titel", 28, -14f, 40f).text = "Avatar en fiets  <size=16><color=#bbb>(V of Esc sluit)</color></size>";

            int bikes = Bike.All.Length, colors = AvatarSettings.Colors.Length;
            float y = -70f;
            (_bike, _) = Row(panel, "Fiets", ref y, false,
                d => Change(a => a.bike = (int)Bike.All[Step(System.Array.FindIndex(Bike.All, b => b.Kind == a.Bike), d, bikes)].Kind));
            _bikeInfo = UiFactory.Text(panel, "Fiets uitleg", 16, y + 8f, 26f);
            _bikeInfo.color = new Color(1f, 1f, 1f, 0.7f);
            y -= 30f;
            (_frame, _frameSwatch) = Row(panel, "Frame", ref y, true, d => Change(a => a.frame = Step(a.frame, d, colors)));
            (_jersey, _jerseySwatch) = Row(panel, "Shirt", ref y, true, d => Change(a => a.jersey = Step(a.jersey, d, colors)));
            (_accent, _accentSwatch) = Row(panel, "Band", ref y, true, d => Change(a => a.accent = Step(a.accent, d, colors)));
            (_helmet, _helmetSwatch) = Row(panel, "Helm", ref y, true, d => Change(a => a.helmet = Step(a.helmet, d, colors)));
            (_skin, _skinSwatch) = Row(panel, "Huid", ref y, true, d => Change(a => a.skin = Step(a.skin, d, AvatarSettings.SkinTones.Length)));

            var note = UiFactory.Text(panel, "Uitleg", 15, y - 2f, 40f);
            note.color = new Color(1f, 1f, 1f, 0.65f);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;
            note.text = "Het gewicht van de fiets telt mee als je klimt; een aerofiets heeft minder luchtweerstand.";

            UiFactory.Button(panel, "Klaar", new Vector2(16f, -504f), 438f, 44f, Close).label.text = "Klaar";
        }

        /// <summary>Regel met een naam, &lt; en &gt; en de huidige keuze (met een kleurvlak als <paramref name="swatch"/>).</summary>
        static (Text value, Image swatch) Row(RectTransform panel, string label, ref float y, bool swatch, System.Action<int> step)
        {
            var name = UiFactory.Text(panel, label, 22, y, 48f);
            name.text = label;
            UiFactory.Button(panel, label + " vorige", new Vector2(110f, y), 48f, 48f, () => step(-1)).label.text = "<";
            UiFactory.Button(panel, label + " volgende", new Vector2(406f, y), 48f, 48f, () => step(1)).label.text = ">";

            Image square = null;
            if (swatch)
            {
                square = new GameObject(label + " kleur", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                var rt = square.rectTransform;
                rt.SetParent(panel, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(172f, y - 10f);
                rt.sizeDelta = new Vector2(28f, 28f);
            }
            var value = UiFactory.Text(panel, label + " keuze", 22, y, 48f);
            value.alignment = TextAnchor.MiddleCenter;
            value.rectTransform.offsetMin = new Vector2(swatch ? 206f : 166f, value.rectTransform.offsetMin.y);
            value.rectTransform.offsetMax = new Vector2(-72f, value.rectTransform.offsetMax.y);
            y -= 60f;
            return (value, square);
        }
    }
}
