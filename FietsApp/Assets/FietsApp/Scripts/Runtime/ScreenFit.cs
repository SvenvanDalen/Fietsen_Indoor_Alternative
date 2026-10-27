using FietsApp.Mobile;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Vergroot een paneel op een klein scherm (story 14.2), rond zijn draaipunt, zodat tekst en knoppen op een
    /// telefoon leesbaar en aan te tikken blijven; het paneel blijft altijd in beeld (<see cref="UiScale"/>).
    /// Op een pc verandert er niets, behalve dat een te groot paneel krimpt tot het past. <see cref="UiFactory.Panel"/> zet hem op elk paneel.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ScreenFit : MonoBehaviour
    {
        /// <summary>
        /// Doen alsof het scherm zo veel pixels hoog is bij deze dpi, om in de editor te zien hoe het op een telefoon
        /// oogt (bijvoorbeeld 1080 bij 400). Null = het echte scherm.
        /// </summary>
        public static (int heightPixels, float dpi)? Preview;

                RectTransform _rect, _canvas;
        float _baseScale = 1f;
        Vector2 _lastCanvas, _lastSize, _lastPosition;
        int _lastHeight;

        /// <summary>Eigen schaal van het paneel (zoals de HUD-grootte), waarbovenop de vergroting komt.</summary>
        public float BaseScale
        {
            get => _baseScale;
            set
            {
                _baseScale = value;
                Apply();
            }
        }

        void Awake() => _rect = (RectTransform)transform;

        void OnEnable() => Apply();

        void LateUpdate()
        {
            if (_canvas == null) _canvas = FindCanvas();
            if (_canvas == null) return;
            if (_canvas.rect.size != _lastCanvas || _rect.rect.size != _lastSize || _rect.anchoredPosition != _lastPosition
                || Screen.height != _lastHeight || Preview.HasValue) Apply();
        }

        RectTransform FindCanvas()
        {
            var canvas = GetComponentInParent<Canvas>();
            return canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
        }

        void Apply()
        {
            if (_rect == null) return;
            if (_canvas == null) _canvas = FindCanvas();
            // Alleen op een telefoon of tablet groeien; op een pc is Screen.height de hoogte van het venster, en een
            // klein venster hoort niet anders te ogen dan voorheen. Passend maken in het scherm gebeurt overal.
            var (height, dpi) = Preview ?? (Screen.height, Screen.dpi);
            float desired = Preview.HasValue || Application.isMobilePlatform ? UiScale.Desired(height, dpi) : 1f;
            float scale = desired * _baseScale;
            var size = _rect.rect.size;
            if (_canvas != null && size.x > 0f && size.y > 0f)
            {
                var canvas = _canvas.rect.size;
                var pivot = _rect.pivot;
                var position = _rect.anchoredPosition;
                scale = UiScale.Fit(scale, size.x, size.y,
                    UiScale.Available(canvas.x, pivot.x, position.x), UiScale.Available(canvas.y, pivot.y, position.y), margin: 0f);
                _lastCanvas = canvas;
            }
            _lastSize = _rect.rect.size;
            _lastPosition = _rect.anchoredPosition;
            _lastHeight = Screen.height;
            _rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
