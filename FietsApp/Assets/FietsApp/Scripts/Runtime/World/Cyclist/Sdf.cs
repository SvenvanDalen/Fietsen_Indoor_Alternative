using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Eén onderdeel van een vorm: een afgeronde kegel (eventueel afgeplat), een ellipsoïde of een afgerond blok, met
    /// hoe zacht het overgaat in de rest, bij welk bot het hoort en welke kleur het krijgt. Een gewone struct, zodat
    /// Burst-jobs ermee kunnen rekenen.
    /// </summary>
    public struct SdfPrim
    {
        public const int Cone = 0, Ellipsoid = 1, Box = 2;

        public int Kind;
        public float3 A, B;          // kegel: begin en eind; ellipsoïde en blok: midden in A
        public float Ra, Rb;         // kegel: straal begin en eind; blok: afronding in Ra
        public float3 Radii;         // ellipsoïde: stralen; blok: halve maten
        public quaternion Inverse;
        public float3 Side;          // afgeplatte kegel: de richting die smaller wordt
        public float Flat, FlatB;    // breedte opzij gedeeld door de straal, aan begin en eind (1 = rond)
        public float Blend;          // zachtheid van de overgang naar de rest (m)
        public int Subtract;
        public int Bone;
        public float3 Min, Max;      // omhullende doos
        // Kleur: langs de lijn PaintA-PaintB wisselt het slot bij de breekpunten (fractie van de lijn, oplopend):
        // Slots.x vóór Breaks.x, Slots.y tot Breaks.y, ..., en LastSlot na Breaks.w.
        public float3 PaintA, PaintB;
        public float4 Breaks;
        public int4 Slots;
        public int LastSlot;

        public bool PaintsAlong => math.lengthsq(PaintB - PaintA) > 1e-10f;
    }

    /// <summary>
    /// Een vorm als afstandsveld (signed distance field), story 7.3: per punt de afstand tot het oppervlak, negatief
    /// binnenin. Onderdelen worden met een zachte vereniging aan elkaar gezet, zodat ze vloeiend in elkaar overlopen in
    /// plaats van losse stukken te blijven: een knie, een schouder of de las tussen twee framebuizen.
    /// <see cref="SurfaceNets"/> maakt er een mesh van. Deze klasse verzamelt de onderdelen; het rekenen gebeurt in
    /// <see cref="SdfMath"/>.
    /// </summary>
    public sealed class Sdf
    {
        readonly List<SdfPrim> _prims = new List<SdfPrim>();
        SdfPrim _paint = new SdfPrim { Breaks = new float4(9f) };

        /// <summary>Bot voor de onderdelen die hierna komen (voor de huidgewichten).</summary>
        public int Bone { get; set; }

        public int Count => _prims.Count;

        public IReadOnlyList<SdfPrim> Prims => _prims;

        /// <summary>De onderdelen die hierna komen krijgen dit slot.</summary>
        public void Paint(int slot) => _paint = new SdfPrim { Breaks = new float4(9f), Slots = new int4(slot), LastSlot = slot };

        /// <summary>
        /// De onderdelen die hierna komen krijgen langs de lijn van <paramref name="a"/> naar <paramref name="b"/>
        /// opeenvolgende slots, gewisseld bij de breekpunten (fracties van de lijn, oplopend). Onderdelen met dezelfde
        /// lijn kleuren naadloos door, en de grens wordt een strakke rand (zie <see cref="SurfaceNets"/>).
        /// </summary>
        public void PaintAlong(Vector3 a, Vector3 b, float[] breaks, int[] slots)
        {
            var p = new SdfPrim { PaintA = a, PaintB = b, Breaks = new float4(9f) };
            for (int i = 0; i < breaks.Length; i++) p.Breaks[i] = breaks[i];
            for (int i = 0; i < 4; i++) p.Slots[i] = slots[Mathf.Min(i, slots.Length - 1)];
            p.LastSlot = slots[slots.Length - 1];
            _paint = p;
        }

        /// <summary>Afgeronde kegel van <paramref name="a"/> (straal ra) naar <paramref name="b"/> (straal rb).</summary>
        public void Cone(Vector3 a, Vector3 b, float ra, float rb, float blend, bool subtract = false)
        {
            var p = New(SdfPrim.Cone, blend, subtract);
            p.A = a; p.B = b; p.Ra = ra; p.Rb = rb;
            Add(p, Vector3.Min(a - Vector3.one * ra, b - Vector3.one * rb), Vector3.Max(a + Vector3.one * ra, b + Vector3.one * rb));
        }

        /// <summary>Kegel met een platte doorsnede: in de richting <paramref name="side"/> maar <paramref name="flat"/> keer zo dik.</summary>
        public void FlatCone(Vector3 a, Vector3 b, float ra, float rb, Vector3 side, float flat, float blend) =>
            FlatCone(a, b, ra, rb, side, flat, flat, blend);

        /// <summary>
        /// Kegel met een platte doorsnede die verloopt: aan het begin <paramref name="flatA"/>, aan het eind
        /// <paramref name="flatB"/> keer zo dik in de richting <paramref name="side"/>. Een keten hiervan is een
        /// vloeiende romp of ledemaat met ovale doorsneden.
        /// </summary>
        public void FlatCone(Vector3 a, Vector3 b, float ra, float rb, Vector3 side, float flatA, float flatB, float blend)
        {
            var p = New(SdfPrim.Cone, blend, false);
            p.A = a; p.B = b; p.Ra = ra; p.Rb = rb;
            p.Side = Vector3.ProjectOnPlane(side, (b - a).normalized).normalized;
            p.Flat = flatA;
            p.FlatB = flatB;
            float r = Mathf.Max(ra, rb) * Mathf.Max(1f, Mathf.Max(flatA, flatB));
            Add(p, Vector3.Min(a, b) - Vector3.one * r, Vector3.Max(a, b) + Vector3.one * r);
        }

        /// <summary>
        /// Keten van kegels door de punten, met per punt een straal. Alleen het begin loopt zacht over in de rest; de
        /// schakels sluiten precies op elkaar aan, zodat er geen bobbels op de naden komen.
        /// </summary>
        public void Chain(IList<Vector3> points, IList<float> radii, float blend)
        {
            for (int i = 0; i < points.Count - 1; i++)
                Cone(points[i], points[i + 1], radii[i], radii[i + 1], i == 0 ? blend : 0f);
        }

        public void Ellipsoid(Vector3 center, Quaternion rotation, Vector3 radii, float blend, bool subtract = false)
        {
            var p = New(SdfPrim.Ellipsoid, blend, subtract);
            p.A = center; p.Radii = radii; p.Inverse = Quaternion.Inverse(rotation);
            float r = Mathf.Max(radii.x, Mathf.Max(radii.y, radii.z));
            Add(p, center - Vector3.one * r, center + Vector3.one * r);
        }

        public void Sphere(Vector3 center, float radius, float blend) => Ellipsoid(center, Quaternion.identity, Vector3.one * radius, blend);

        /// <summary>Blok met halve maten <paramref name="half"/> en afgeronde randen.</summary>
        public void Box(Vector3 center, Quaternion rotation, Vector3 half, float rounding, float blend, bool subtract = false)
        {
            var p = New(SdfPrim.Box, blend, subtract);
            p.A = center; p.Radii = half; p.Ra = rounding; p.Inverse = Quaternion.Inverse(rotation);
            float r = half.magnitude;
            Add(p, center - Vector3.one * r, center + Vector3.one * r);
        }

        SdfPrim New(int kind, float blend, bool subtract)
        {
            var p = _paint;
            p.Kind = kind;
            p.Blend = blend;
            p.Subtract = subtract ? 1 : 0;
            p.Bone = Bone;
            p.Inverse = quaternion.identity;
            p.Flat = p.FlatB = 1f;
            return p;
        }

        void Add(SdfPrim p, Vector3 min, Vector3 max)
        {
            p.Min = min;
            p.Max = max;
            _prims.Add(p);
        }

        /// <summary>Omhullende doos van alle toegevoegde (niet weggehaalde) onderdelen.</summary>
        public Bounds Extent
        {
            get
            {
                var b = new Bounds();
                bool first = true;
                foreach (var p in _prims)
                {
                    if (p.Subtract != 0) continue;
                    if (first) { b = new Bounds((Vector3)(p.Min + p.Max) * 0.5f, p.Max - p.Min); first = false; }
                    else { b.Encapsulate(p.Min); b.Encapsulate(p.Max); }
                }
                return b;
            }
        }

        public NativeArray<SdfPrim> ToNative(Allocator allocator)
        {
            var array = new NativeArray<SdfPrim>(_prims.Count, allocator);
            for (int i = 0; i < _prims.Count; i++) array[i] = _prims[i];
            return array;
        }

        /// <summary>Afstand tot het oppervlak (voor tests en losse controles; de meshbouw rekent in Burst).</summary>
        public float Distance(Vector3 point)
        {
            using (var prims = ToNative(Allocator.Temp))
                return SdfMath.Evaluate(prims, default, 0, -1, point, out _);
        }
    }

    /// <summary>De rekenkant van <see cref="Sdf"/>: afstand, kleur en botafstanden, bruikbaar in Burst-jobs.</summary>
    public static class SdfMath
    {
        /// <summary>
        /// Afstand tot het oppervlak en het onderdeel dat er de doorslag geeft. Met <paramref name="count"/> &gt;= 0
        /// alleen de onderdelen uit <paramref name="near"/> vanaf <paramref name="offset"/>, anders allemaal.
        /// </summary>
        public static float Evaluate(NativeArray<SdfPrim> prims, NativeArray<int> near, int offset, int count, float3 point, out int dominant)
        {
            int n = count < 0 ? prims.Length : count;
            float d = 1e9f, nearest = 1e9f;
            dominant = -1;
            for (int k = 0; k < n; k++)
            {
                int i = count < 0 ? k : near[offset + k];
                var p = prims[i];
                if (p.Subtract != 0) continue;
                float box = BoxDistance(point, p.Min, p.Max);
                if (box >= d + p.Blend && box >= nearest) continue;
                float di = Single(p, point);
                if (di < nearest) { nearest = di; dominant = i; }
                d = p.Blend > 0f ? SmoothMin(d, di, p.Blend) : math.min(d, di);
            }
            for (int k = 0; k < n; k++)
            {
                int i = count < 0 ? k : near[offset + k];
                var p = prims[i];
                if (p.Subtract == 0) continue;
                if (BoxDistance(point, p.Min, p.Max) > p.Blend - d) continue;
                float cut = -Single(p, point);
                if (cut > d) dominant = i;
                d = p.Blend > 0f ? -SmoothMin(-d, -cut, p.Blend) : math.max(d, cut);
            }
            return d;
        }

        /// <summary>Richting van de snelste toename (vier evaluaties in een tetraëder), ongeveer de normaal.</summary>
        public static float3 Gradient(NativeArray<SdfPrim> prims, NativeArray<int> near, int offset, int count, float3 p, float eps)
        {
            var a = new float3(1f, -1f, -1f);
            var b = new float3(-1f, -1f, 1f);
            var c = new float3(-1f, 1f, -1f);
            var e = new float3(1f, 1f, 1f);
            return (a * Evaluate(prims, near, offset, count, p + a * eps, out _) + b * Evaluate(prims, near, offset, count, p + b * eps, out _) +
                    c * Evaluate(prims, near, offset, count, p + c * eps, out _) + e * Evaluate(prims, near, offset, count, p + e * eps, out _)) / (4f * eps);
        }

        /// <summary>Het slot van een onderdeel op een punt (langs zijn kleurlijn).</summary>
        public static int Paint(SdfPrim p, float3 point)
        {
            if (!p.PaintsAlong) return p.Slots.x;
            float t = Along(p, point);
            return t < p.Breaks.x ? p.Slots.x : t < p.Breaks.y ? p.Slots.y : t < p.Breaks.z ? p.Slots.z : t < p.Breaks.w ? p.Slots.w : p.LastSlot;
        }

        /// <summary>Waar een punt langs de kleurlijn van een onderdeel ligt (0 aan het begin, 1 aan het eind).</summary>
        public static float Along(SdfPrim p, float3 point)
        {
            var ab = p.PaintB - p.PaintA;
            return math.dot(point - p.PaintA, ab) / math.dot(ab, ab);
        }

        public static float Single(SdfPrim p, float3 point)
        {
            if (p.Kind == SdfPrim.Ellipsoid)
            {
                var q = math.mul(p.Inverse, point - p.A);
                var r = p.Radii;
                float k0 = math.length(q / r);
                float k1 = math.length(q / (r * r));
                return k1 < 1e-9f ? -math.cmin(r) : k0 * (k0 - 1f) / k1;
            }
            if (p.Kind == SdfPrim.Box)
            {
                var q = math.mul(p.Inverse, point - p.A);
                var a = math.abs(q) - (p.Radii - p.Ra);
                return math.length(math.max(a, 0f)) + math.min(math.cmax(a), 0f) - p.Ra;
            }
            if (math.abs(p.Flat - 1f) < 0.001f && math.abs(p.FlatB - 1f) < 0.001f) return RoundCone(point, p.A, p.B, p.Ra, p.Rb);
            // Afgeplat: de zijrichting oprekken, rekenen als ronde kegel, en terugschalen.
            var rel = point - p.A;
            var ab = p.B - p.A;
            float flat = math.lerp(p.Flat, p.FlatB, math.saturate(math.dot(rel, ab) / math.max(math.dot(ab, ab), 1e-10f)));
            float side = math.dot(rel, p.Side);
            var stretched = p.A + rel + p.Side * (side / flat - side);
            return RoundCone(stretched, p.A, p.B, p.Ra, p.Rb) * math.min(flat, 1f);
        }

        /// <summary>Afgeronde kegel (Inigo Quilez, "sdRoundCone" met twee punten).</summary>
        static float RoundCone(float3 p, float3 a, float3 b, float r1, float r2)
        {
            var ba = b - a;
            float l2 = math.dot(ba, ba);
            if (l2 < 1e-10f) return math.length(p - a) - math.max(r1, r2);
            float rr = r1 - r2;
            float a2 = l2 - rr * rr;
            float il2 = 1f / l2;
            var pa = p - a;
            float y = math.dot(pa, ba);
            float z = y - l2;
            var xv = pa * l2 - ba * y;
            float x2 = math.dot(xv, xv);
            float y2 = y * y * l2;
            float z2 = z * z * l2;
            float k = math.sign(rr) * rr * rr * x2;
            if (math.sign(z) * a2 * z2 > k) return math.sqrt(x2 + z2) * il2 - r2;
            if (math.sign(y) * a2 * y2 < k) return math.sqrt(x2 + y2) * il2 - r1;
            return (math.sqrt(x2 * a2 * il2) + y * rr) * il2 - r1;
        }

        /// <summary>Zachte vereniging (polynomiaal): binnen <paramref name="k"/> lopen de vormen vloeiend in elkaar over.</summary>
        static float SmoothMin(float a, float b, float k)
        {
            float h = math.max(k - math.abs(a - b), 0f) / k;
            return math.min(a, b) - h * h * k * 0.25f;
        }

        public static float BoxDistance(float3 p, float3 min, float3 max) => math.length(math.max(math.max(min - p, 0f), p - max));

        public static float BoxToBox(float3 aMin, float3 aMax, float3 bMin, float3 bMax) =>
            math.length(math.max(math.max(aMin - bMax, 0f), bMin - aMax));
    }
}
