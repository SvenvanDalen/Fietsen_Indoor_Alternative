using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FietsApp.Training;
using FietsApp.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Wereldkaart (story 4.11c). K opent hem: links de gebieden in een ring met de wegen ertussen, en in het
    /// midden het gekozen dal met elke klim als spaak in zijn echte richting en lengte (kleur naar categorie);
    /// rechts de klimmen van dat gebied. Een klim kiezen start de rit bij het knooppunt van dat dal, achter een
    /// korte fade zodat je de wereld niet ziet opbouwen. Zolang de kaart open is staat de rit stil.
    /// Het is ook het overzicht van je badges (story 9.1): uitgereden klimmen krijgen een gouden ruit, per gebied en
    /// in de titel staat hoeveel je er hebt.
    /// </summary>
    public sealed class WorldMapView : MonoBehaviour
    {
        [SerializeField] RouteView routeView;
        [SerializeField] RideMenu rideMenu;
        [SerializeField] RideController ride;
        [SerializeField, Min(0.05f)] float fadeSeconds = 0.35f;

        const float MapSize = 820f, ListWidth = 560f, Height = 860f;
        static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");

        GameObject _panel;
        RectTransform _map, _detail, _list;
        Image _fade;
        Text _title;
        WorldMap _world;
        Dictionary<string, Climb> _assets;
        readonly List<(Button button, WorldRegion region)> _regionButtons = new List<(Button, WorldRegion)>();
        WorldRegion _selected;
        bool _busy;

        public bool IsOpen => _panel != null && _panel.activeSelf;

        void Awake()
        {
            if (routeView == null) routeView = FindAnyObjectByType<RouteView>();
            if (rideMenu == null) rideMenu = FindAnyObjectByType<RideMenu>();
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            UiFactory.EnsureEventSystem();
            BuildUi();
            _panel.SetActive(false);
        }

        void Update()
        {
            var kb = UiFactory.IsTyping ? null : Keyboard.current; // niet tijdens typen in een tekstveld
            if (kb == null || _busy || (rideMenu != null && rideMenu.IsOpen)) return;
            if (kb.kKey.wasPressedThisFrame) { if (IsOpen) Close(); else Open(); }
            if (IsOpen && kb.escapeKey.wasPressedThisFrame) Close();
        }

        public void Open()
        {
            _world = routeView.World;
            if (_world == null) return; // geen bibliotheek: geen wereld
            _assets = rideMenu.Climbs.ToDictionary(c => c.name);
            if (_regionButtons.Count == 0) BuildRegions();
            var current = routeView.Climb != null ? _world.Find(routeView.Climb.name) : null;
            Select(current != null ? _world.RegionOf(current) : _world.Regions[0]);
            _panel.SetActive(true);
            ride.Hold(this);
        }

        public void Close()
        {
            _panel.SetActive(false);
            ride.Release(this);
        }

        /// <summary>Rit op deze klim, achter een korte fade naar zwart en terug.</summary>
        public void Ride(WorldClimb climb)
        {
            if (_busy || !_assets.TryGetValue(climb.Id, out var asset)) return;
            StartCoroutine(FadeAndRide(asset));
        }

        IEnumerator FadeAndRide(Climb asset)
        {
            _busy = true;
            _fade.gameObject.SetActive(true);
            yield return Fade(0f, 1f);
            _panel.SetActive(false);
            yield return null; // zwart beeld eerst tonen, dan pas de wereld opbouwen
            rideMenu.Ride(asset);
            ride.Release(this);
            yield return null;
            yield return Fade(1f, 0f);
            _fade.gameObject.SetActive(false);
            _busy = false;
        }

        IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                _fade.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / fadeSeconds));
                yield return null;
            }
            _fade.color = new Color(0f, 0f, 0f, to);
        }

        void Select(WorldRegion region)
        {
            _selected = region;
            var profile = ProfileStore.Current;
            foreach (var (button, r) in _regionButtons)
            {
                button.GetComponent<Image>().color = r == region ? new Color(0.85f, 0.55f, 0.15f) : new Color(0.16f, 0.3f, 0.5f);
                // Badges per gebied (9.1): uitgereden van het totaal.
                int earned = profile.BadgeCount(r.Climbs.Select(c => c.Name));
                button.GetComponentInChildren<Text>().text = earned > 0
                    ? $"{r.Name} <color=#ffd060>{earned}/{r.Climbs.Count}</color>"
                    : $"{r.Name} ({r.Climbs.Count})";
            }
            DrawDetail(region);
            DrawList(region);
            var all = _world.Regions.SelectMany(r => r.Climbs).Select(c => c.Name).ToList();
            _title.text = $"Wereldkaart  <color=#ffd060>{profile.BadgeCount(all)}/{all.Count} badges</color>  " +
                          "<size=16><color=#bbb>(K of Esc sluit; kies een gebied, dan een klim)</color></size>";
        }

        void BuildRegions()
        {
            float ring = _world.Regions.Max(r => Mathf.Sqrt(r.HubX * r.HubX + r.HubZ * r.HubZ));
            float scale = ring > 0f ? (MapSize / 2f - 80f) / ring : 0f;
            Vector2 Pos(WorldRegion r) => new Vector2(r.HubX * scale, r.HubZ * scale);

            // Wegen tussen de dalen (de ring), onder de knoppen.
            for (int i = 0; i < _world.Regions.Count && _world.Regions.Count > 1; i++)
            {
                var a = _world.Regions[i];
                var b = _world.Regions[(i + 1) % _world.Regions.Count];
                Line(_map, Pos(a), Pos(b), 3f, new Color(0.55f, 0.55f, 0.55f, 0.8f));
            }
            foreach (var region in _world.Regions)
            {
                var r = region;
                var (button, label) = UiFactory.Button(_map, r.Name, Vector2.zero, 128f, 36f, () => Select(r));
                var rt = button.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Pos(r);
                label.fontSize = 16; // tekst met badges volgt in Select
                _regionButtons.Add((button, r));
            }
        }

        /// <summary>Het gekozen dal in het midden van de ring: elke klim een spaak in zijn richting, lengte naar schaal.</summary>
        void DrawDetail(WorldRegion region)
        {
            foreach (Transform child in _detail) Destroy(child.gameObject);
            float longest = region.Climbs.Max(c => c.Route.Length + _world.Settings.ApproachLength);
            float scale = 190f / longest;
            float approach = _world.Settings.ApproachLength * scale;
            foreach (var climb in region.Climbs)
            {
                var dir = new Vector2(Mathf.Cos(climb.Angle), Mathf.Sin(climb.Angle));
                Vector2 foot = dir * approach, top = dir * (approach + climb.Route.Length * scale);
                Line(_detail, Vector2.zero, foot, 2f, new Color(0.6f, 0.6f, 0.6f));
                var stats = ClimbStats.Of(climb.Route);
                bool current = routeView.Climb != null && routeView.Climb.name == climb.Id;
                Line(_detail, foot, top, current ? 7f : 5f, CategoryColor(ClimbCategories.Of(stats)));
                if (ProfileStore.Current.HasBadge(climb.Name)) Badge(_detail, top, 16f);
            }
            var hub = new GameObject("Knooppunt", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            hub.SetParent(_detail, false);
            hub.sizeDelta = new Vector2(12f, 12f);
            hub.GetComponent<Image>().color = Color.white;
            var name = UiFactory.Text(_detail, "Dal", 18, 0f, 24f);
            name.alignment = TextAnchor.MiddleCenter;
            name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            name.rectTransform.sizeDelta = new Vector2(300f, 24f);
            name.rectTransform.anchoredPosition = new Vector2(0f, -222f);
            name.text = $"<b>{region.Name}</b>  <color=#bbb>dal {region.ValleyElevation:0} m</color>";
        }

        void DrawList(WorldRegion region)
        {
            foreach (Transform child in _list) Destroy(child.gameObject);
            float y = -8f;
            float rowHeight = Mathf.Min(40f, (Height - 120f) / Mathf.Max(1, region.Climbs.Count) - 4f);
            foreach (var climb in region.Climbs)
            {
                var c = climb;
                var stats = ClimbStats.Of(climb.Route);
                var category = ClimbCategories.Of(stats);
                var record = ProfileStore.Current.Record(climb.Name);
                var (button, label) = UiFactory.Button(_list, climb.Id, new Vector2(0f, y), ListWidth - 32f, rowHeight, () => Ride(c));
                // Uitgereden klimmen (badge, 9.1) in brons met een gouden ruit.
                button.GetComponent<Image>().color = record != null ? new Color(0.42f, 0.32f, 0.1f, 1f) : new Color(0.16f, 0.3f, 0.5f, 1f);
                if (record != null) Badge((RectTransform)button.transform, new Vector2(ListWidth / 2f - 32f, 0f), Mathf.Min(16f, rowHeight - 10f));
                label.alignment = TextAnchor.MiddleLeft;
                label.supportRichText = true;
                label.fontSize = rowHeight < 30f ? 14 : 17;
                label.rectTransform.offsetMin = new Vector2(10f, 0f);
                string color = ColorUtility.ToHtmlStringRGB(CategoryColor(category));
                bool current = routeView.Climb != null && routeView.Climb.name == climb.Id;
                label.text = $"<b>{climb.Name}</b>{(current ? "  <color=#9fd>(nu)</color>" : "")}   " +
                             $"<color=#ddd>{(stats.LengthMeters / 1000f).ToString("0.0", Dutch)} km · " +
                             $"{stats.AverageGradePercent.ToString("0.0", Dutch)}%</color>  " +
                             $"<color=#{color}>{ClimbCategories.Label(category)}</color>" +
                             (record != null ? $"  <color=#ffd060>PR {ClimbResult.Clock(record.bestSeconds)}</color>" : "");
                y -= rowHeight + 4f;
            }
        }

        static Color CategoryColor(ClimbCategory category) => category switch
        {
            ClimbCategory.HC => new Color(0.75f, 0.15f, 0.45f),
            ClimbCategory.Cat1 => new Color(0.9f, 0.2f, 0.15f),
            ClimbCategory.Cat2 => new Color(0.98f, 0.55f, 0.15f),
            ClimbCategory.Cat3 => new Color(0.95f, 0.85f, 0.25f),
            ClimbCategory.Cat4 => new Color(0.5f, 0.8f, 0.35f),
            _ => new Color(0.6f, 0.6f, 0.6f),
        };

        /// <summary>Badge: een gouden ruit met donkere rand, gecentreerd op <paramref name="at"/>.</summary>
        static void Badge(RectTransform parent, Vector2 at, float size)
        {
            foreach (var (s, color) in new[] { (size + 4f, new Color(0.25f, 0.18f, 0.02f)), (size, new Color(1f, 0.82f, 0.25f)) })
            {
                var rt = new GameObject("Badge", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                rt.SetParent(parent, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = at;
                rt.sizeDelta = new Vector2(s, s);
                rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var image = rt.GetComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
            }
        }

        /// <summary>Lijn als dun gedraaid rechthoekje van <paramref name="from"/> naar <paramref name="to"/>.</summary>
        static void Line(RectTransform parent, Vector2 from, Vector2 to, float width, Color color)
        {
            var rt = new GameObject("Lijn", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            var d = to - from;
            rt.anchoredPosition = from;
            rt.sizeDelta = new Vector2(d.magnitude, width);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            var image = rt.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        void BuildUi()
        {
            var canvas = UiFactory.Canvas(transform, "Wereldkaart Canvas", 40); // boven HUD, Bluetooth-paneel en profiel
            var panel = UiFactory.Panel(canvas, "Kaart", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MapSize + ListWidth + 40f, Height));
            panel.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.1f, 0.92f);
            _panel = panel.gameObject;
            _title = UiFactory.Text(panel, "Titel", 28, -12f, 40f);

            _map = new GameObject("Ring", typeof(RectTransform)).GetComponent<RectTransform>();
            _map.SetParent(panel, false);
            _map.anchorMin = _map.anchorMax = _map.pivot = new Vector2(0f, 0.5f);
            _map.sizeDelta = new Vector2(MapSize, MapSize - 40f);
            _map.anchoredPosition = new Vector2(10f, -20f);
            // Het midden van de ring is (0, 0) voor lijnen en knoppen: kinderen ankeren op het midden.
            _detail = new GameObject("Dal", typeof(RectTransform)).GetComponent<RectTransform>();
            _detail.SetParent(_map, false);
            _detail.anchorMin = _detail.anchorMax = new Vector2(0.5f, 0.5f);
            _detail.sizeDelta = Vector2.zero;

            _list = new GameObject("Lijst", typeof(RectTransform)).GetComponent<RectTransform>();
            _list.SetParent(panel, false);
            _list.anchorMin = _list.anchorMax = _list.pivot = new Vector2(0f, 1f);
            _list.anchoredPosition = new Vector2(MapSize + 30f, -70f);
            _list.sizeDelta = new Vector2(ListWidth, Height - 90f);

            _fade = new GameObject("Fade", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _fade.transform.SetParent(canvas, false);
            _fade.rectTransform.anchorMin = Vector2.zero;
            _fade.rectTransform.anchorMax = Vector2.one;
            _fade.rectTransform.offsetMin = _fade.rectTransform.offsetMax = Vector2.zero;
            _fade.color = new Color(0f, 0f, 0f, 0f);
            _fade.gameObject.SetActive(false);
        }
    }
}
