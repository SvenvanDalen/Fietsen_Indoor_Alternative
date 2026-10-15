using FietsApp.World;
using UnityEngine;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Tekent een hoogteprofiel als gevulde stukken, gekleurd naar hellingsklasse.
    /// Coördinaten binnen de rect: x = afstand, y = hoogte (met wat ruimte onder en boven).
    /// </summary>
    public sealed class ProfileGraphic : MaskableGraphic
    {
        const float BottomMargin = 0.06f, TopMargin = 0.12f;

        ElevationProfile _profile;

        public ElevationProfile Profile
        {
            get => _profile;
            set
            {
                _profile = value;
                SetVerticesDirty();
            }
        }

        public static Color ColorFor(GradeBand band)
        {
            switch (band)
            {
                case GradeBand.Descent: return new Color(0.45f, 0.7f, 0.95f);
                case GradeBand.Flat: return new Color(0.4f, 0.75f, 0.35f);
                case GradeBand.Moderate: return new Color(0.95f, 0.85f, 0.25f);
                case GradeBand.Hard: return new Color(0.98f, 0.55f, 0.15f);
                case GradeBand.VeryHard: return new Color(0.9f, 0.2f, 0.15f);
                default: return new Color(0.55f, 0.1f, 0.35f);
            }
        }

        /// <summary>Plek in lokale coördinaten van deze graphic voor een afstand en hoogte.</summary>
        public Vector2 PointAt(float distance, float elevation)
        {
            var r = rectTransform.rect;
            if (_profile == null || _profile.Length <= 0f) return r.min;
            float range = Mathf.Max(1f, _profile.MaxElevation - _profile.MinElevation);
            float x = Mathf.Clamp01(distance / _profile.Length);
            float y = BottomMargin + (elevation - _profile.MinElevation) / range * (1f - BottomMargin - TopMargin);
            return new Vector2(r.xMin + x * r.width, r.yMin + y * r.height);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_profile == null) return;
            float bottom = rectTransform.rect.yMin;

            foreach (var s in _profile.Segments)
            {
                var a = PointAt(s.StartDistance, s.StartElevation);
                var b = PointAt(s.EndDistance, s.EndElevation);
                Color32 c = ColorFor(Grades.Band(s.GradePercent));
                int i = vh.currentVertCount;
                vh.AddVert(new Vector3(a.x, bottom), c, Vector4.zero);
                vh.AddVert(new Vector3(a.x, a.y), c, Vector4.zero);
                vh.AddVert(new Vector3(b.x, b.y), c, Vector4.zero);
                vh.AddVert(new Vector3(b.x, bottom), c, Vector4.zero);
                vh.AddTriangle(i, i + 1, i + 2);
                vh.AddTriangle(i, i + 2, i + 3);
            }
        }
    }
}
