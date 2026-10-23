using System.Collections.Generic;
using FietsApp.Training;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// De renner op zijn fiets, in code opgebouwd (story 7.3): frame, vork, stuur met remgrepen, wielen met spaken,
    /// crankstel, en een rijder met helm, bril, shirt en koersbroek. De benen trappen mee met de cadans (de knie volgt
    /// uit dij- en scheenlengte), de wielen draaien met de snelheid. Uiterlijk en fiets komen uit het profiel
    /// (<see cref="AvatarSettings"/>) en veranderen meteen mee. De oorsprong ligt op de grond midden tussen de wielen.
    /// </summary>
    public sealed class CyclistModel : MonoBehaviour
    {
        [Tooltip("Materiaal waarvan de kleuren afgeleid worden (URP Lit).")]
        [SerializeField] Material baseMaterial;

        /// <summary>Snelheid in m/s; draait de wielen.</summary>
        public float SpeedMs { get; set; }

        /// <summary>Trapfrequentie; 0 = freewheelen, de benen staan dan stil.</summary>
        public float CadenceRpm { get; set; }

        /// <summary>Eén materiaal voor alles, bijvoorbeeld doorzichtig voor de ghost; zet het vóór de eerste frame.</summary>
        public Material OverrideMaterial { get; set; }

        /// <summary>Of het model het profiel volgt; anders bepaalt <see cref="Build"/> het uiterlijk.</summary>
        public bool FollowProfile { get; set; } = true;

        public CyclistGeometry Geometry { get; private set; }

        Transform _model, _crank, _frontWheel, _rearWheel;
        readonly Transform[] _thigh = new Transform[2], _shin = new Transform[2], _foot = new Transform[2], _pedal = new Transform[2];
        readonly List<Material> _materials = new List<Material>();
        readonly List<Mesh> _meshes = new List<Mesh>();
        Material[] _slots;
        string _built;
        float _crankAngle, _wheelAngle;

        void OnEnable() => ProfileStore.Changed += OnProfileChanged;

        void OnDisable() => ProfileStore.Changed -= OnProfileChanged;

        void Start()
        {
            if (_model == null) Build(FollowProfile ? ProfileStore.Current.avatar : new AvatarSettings());
        }

        void OnProfileChanged()
        {
            if (FollowProfile && _model != null) Build(ProfileStore.Current.avatar);
        }

        void OnDestroy() => Clear();

        void LateUpdate()
        {
            if (_model == null) return;
            float dt = Time.deltaTime;
            _crankAngle = Mathf.Repeat(_crankAngle + CadenceRpm / 60f * Mathf.PI * 2f * dt, Mathf.PI * 2f);
            _wheelAngle = Mathf.Repeat(_wheelAngle + SpeedMs / Geometry.WheelRadius * Mathf.Rad2Deg * dt, 360f);
            Pose();
        }

        /// <summary>Bouwt het model (opnieuw) op voor dit uiterlijk; doet niets als het al zo is.</summary>
        public void Build(AvatarSettings avatar)
        {
            avatar ??= new AvatarSettings();
            string key = JsonUtility.ToJson(avatar) + (OverrideMaterial != null ? "|" + OverrideMaterial.name : "");
            if (_model != null && key == _built) return;
            Clear();
            _built = key;
            Geometry = new CyclistGeometry(avatar.Bike);
            _slots = MakeMaterials(avatar);

            _model = new GameObject("Model").transform;
            _model.SetParent(transform, false);
            var g = Geometry;

            Part("Fiets", _model, Vector3.zero, BuildFrame);
            _rearWheel = Part("Achterwiel", _model, g.RearAxle, k => BuildWheel(k, rear: true));
            _frontWheel = Part("Voorwiel", _model, g.FrontAxle, k => BuildWheel(k, rear: false));
            _crank = Part("Crankstel", _model, g.BottomBracket, BuildCrank);
            Part("Lijf", _model, Vector3.zero, k => BuildBody(k, avatar));
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? 1f : -1f;
                _pedal[side] = Part(side == 0 ? "PedaalR" : "PedaalL", _model, Vector3.zero, k => BuildPedal(k, sign));
                _thigh[side] = Part(side == 0 ? "DijR" : "DijL", _model, Vector3.zero, BuildThigh);
                _shin[side] = Part(side == 0 ? "ScheenR" : "ScheenL", _model, Vector3.zero, BuildShin);
                _foot[side] = Part(side == 0 ? "VoetR" : "VoetL", _model, Vector3.zero, BuildFoot);
            }
            Pose();
        }

        // ---- Bewegen ----

        void Pose()
        {
            var g = Geometry;
            _crank.localRotation = Quaternion.Euler(_crankAngle * Mathf.Rad2Deg, 0f, 0f);
            _rearWheel.localRotation = _frontWheel.localRotation = Quaternion.Euler(_wheelAngle, 0f, 0f);
            for (int side = 0; side < 2; side++)
            {
                bool right = side == 0;
                var hip = right ? g.HipRight : Mirror(g.HipRight);
                var (ankle, pitch) = g.Ankle(_crankAngle, right);
                // Knie naar voren en een fractie naar buiten.
                var knee = CyclistGeometry.Joint(hip, ankle, g.Thigh, g.Shin, new Vector3(right ? 0.12f : -0.12f, 0.2f, 1f));
                Aim(_thigh[side], hip, knee);
                Aim(_shin[side], knee, ankle);
                var footRotation = Quaternion.Euler(pitch, 0f, 0f);
                _foot[side].localPosition = ankle;
                _foot[side].localRotation = footRotation;
                _pedal[side].localPosition = g.Pedal(_crankAngle, right);
                _pedal[side].localRotation = footRotation;
            }
        }

        /// <summary>Zet een ledemaat op <paramref name="from"/> met zijn lokale z naar <paramref name="to"/>; lokale y wijst naar rechts.</summary>
        static void Aim(Transform limb, Vector3 from, Vector3 to)
        {
            limb.localPosition = from;
            limb.localRotation = Quaternion.LookRotation(to - from, Vector3.right);
        }

        static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);

        // ---- Onderdelen ----

        Transform Part(string name, Transform parent, Vector3 position, System.Action<MeshKit> build)
        {
            var kit = new MeshKit();
            build(kit);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var mesh = kit.Build(name, (int)CyclistSlot.Count);
            _meshes.Add(mesh);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = _slots;
            if (OverrideMaterial != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        const int F = (int)CyclistSlot.Frame, Metal = (int)CyclistSlot.Metal, Black = (int)CyclistSlot.Black,
            TyreSlot = (int)CyclistSlot.Tyre, RimSlot = (int)CyclistSlot.Rim, Jersey = (int)CyclistSlot.Jersey,
            Accent = (int)CyclistSlot.Accent, Shorts = (int)CyclistSlot.Shorts, Skin = (int)CyclistSlot.Skin,
            Helmet = (int)CyclistSlot.Helmet, Shoe = (int)CyclistSlot.Shoe, Sock = (int)CyclistSlot.Sock,
            Lens = (int)CyclistSlot.Lens, Bottle = (int)CyclistSlot.Bottle;

        void BuildFrame(MeshKit k)
        {
            var g = Geometry;
            var kind = g.Kind;
            bool aero = kind == BikeKind.Aero, classic = kind == BikeKind.Classic;
            float s = kind == BikeKind.Climbing ? 0.88f : classic ? 0.75f : kind == BikeKind.Gravel ? 1.08f : 1f;
            var seatDir = (g.SeatTop - g.BottomBracket).normalized;
            var headUp = (g.HeadTop - g.HeadBottom).normalized;

            void Main(Vector3 a, Vector3 b, float r, float aeroDepth = 2.4f)
            {
                if (aero) k.FlatTube(F, a, b, r * 0.62f, r * aeroDepth, 16);
                else k.Tube(F, a, b, r * s, r * s, 16);
            }

            // Hoofdbuizen
            Main(g.HeadBottom - headUp * 0.015f, g.HeadTop + headUp * 0.01f, 0.024f, 1.6f);
            Main(g.SeatTop + seatDir * 0.005f, g.HeadTop - headUp * 0.035f, 0.016f, 1.7f);
            Main(g.BottomBracket, g.HeadBottom + headUp * 0.025f, 0.022f);
            Main(g.BottomBracket, g.SeatTop + seatDir * 0.02f, 0.017f);
            k.Tube(F, g.BottomBracket + Vector3.left * 0.04f, g.BottomBracket + Vector3.right * 0.04f, 0.024f * s, 0.024f * s);
            if (classic)
                foreach (var lug in new[] { g.SeatTop, g.HeadTop - headUp * 0.035f, g.HeadBottom + headUp * 0.025f })
                    k.Ellipsoid(F, lug, Quaternion.identity, Vector3.one * 0.019f, 8, 12);

            // Achtervork: liggende en staande achtervork aan beide kanten.
            var stayTop = aero ? g.SeatTop - seatDir * 0.13f : g.SeatTop - seatDir * 0.015f;
            for (int side = -1; side <= 1; side += 2)
            {
                var axle = g.RearAxle + Vector3.right * (0.064f * side);
                var bb = g.BottomBracket + Vector3.right * (0.03f * side);
                var mid = Vector3.Lerp(bb, axle, 0.45f) + Vector3.right * (0.03f * side);
                k.Tube(F, new[] { bb, mid, axle }, Radii(0.0125f * s, 0.011f * s, 0.008f * s), 12, Vector3.right);
                var top = stayTop + Vector3.right * (0.022f * side);
                var bend = Vector3.Lerp(top, axle, 0.6f) + Vector3.right * (0.03f * side);
                k.Tube(F, new[] { top, bend, axle }, Radii(0.009f * s, 0.0085f * s, 0.007f * s), 10, Vector3.right);
                k.Ellipsoid(F, axle, Quaternion.identity, new Vector3(0.006f, 0.016f, 0.016f), 6, 10);
            }

            // Voorvork met kroon; bij de klassieker chroom.
            int fork = classic ? Metal : F;
            var crown = g.HeadBottom - headUp * 0.022f;
            k.Ellipsoid(fork, crown, Quaternion.identity, new Vector3(0.058f, 0.018f, 0.03f), 8, 14);
            for (int side = -1; side <= 1; side += 2)
            {
                var top = crown + Vector3.right * (0.042f * side);
                var tip = g.FrontAxle + Vector3.right * (0.052f * side);
                var mid = Vector3.Lerp(top, tip, 0.55f) + new Vector3(0.006f * side, 0f, -0.012f);
                k.Tube(fork, new[] { top, mid, tip },
                    aero ? new[] { new Vector2(0.011f, 0.024f), new Vector2(0.009f, 0.02f), new Vector2(0.006f, 0.012f) }
                         : Radii(0.016f * s, 0.012f * s, 0.008f * s), 12, Vector3.right);
            }

            // Balhoofdlager, stuurpen en stuur.
            k.Tube(Black, g.HeadTop, g.HeadTop + headUp * 0.03f, 0.018f, 0.018f);
            if (classic)
            {
                k.Tube(Metal, g.HeadTop, g.HeadTop + headUp * 0.07f, 0.012f, 0.012f);
                k.Tube(Metal, g.HeadTop + headUp * 0.065f, g.StemEnd, 0.011f, 0.011f);
            }
            else k.FlatTube(Black, g.HeadTop + headUp * 0.02f, g.StemEnd + Vector3.forward * 0.012f, 0.017f, 0.02f);
            k.Ellipsoid(Black, g.StemEnd, Quaternion.identity, new Vector3(0.024f, 0.022f, 0.022f), 8, 12);
            BuildBars(k);

            // Zadelpen en zadel.
            var postTop = g.SaddleTop - Vector3.up * 0.02f;
            if (aero) k.FlatTube(Black, g.SeatTop, postTop, 0.011f, 0.022f);
            else k.Tube(classic ? Metal : Black, g.SeatTop, postTop, 0.0135f, 0.0135f);
            var saddle = g.SaddleTop + new Vector3(0f, -0.012f, 0f);
            k.Tube(Black, new[]
            {
                saddle + new Vector3(0f, 0.004f, -0.125f), saddle + new Vector3(0f, 0f, -0.09f), saddle + new Vector3(0f, -0.003f, -0.02f),
                saddle + new Vector3(0f, -0.004f, 0.06f), saddle + new Vector3(0f, -0.002f, 0.135f),
            }, new[] { new Vector2(0.06f, 0.014f), new Vector2(0.07f, 0.018f), new Vector2(0.05f, 0.016f), new Vector2(0.024f, 0.013f), new Vector2(0.016f, 0.01f) },
                16, Vector3.right);

            // Bidon in een bidonhouder op de onderbuis.
            var down = (g.HeadBottom - g.BottomBracket).normalized;
            var above = Vector3.Cross(down, Vector3.right).normalized; // loodrecht op de buis, naar boven
            if (Vector3.Dot(above, Vector3.up) < 0f) above = -above;
            var bottleBase = g.BottomBracket + down * 0.17f + above * (0.045f * s + 0.012f);
            k.Tube(Bottle, bottleBase, bottleBase + down * 0.19f, 0.034f, 0.034f, 16);
            k.Tube(Black, bottleBase + down * 0.19f, bottleBase + down * 0.225f, 0.02f, 0.012f, 12);

            // Ketting, derailleur en remmen.
            var ring = g.BottomBracket + Vector3.right * 0.065f;
            var cassette = g.RearAxle + Vector3.right * 0.05f;
            var jockey = g.RearAxle + new Vector3(0.05f, -0.095f, 0.025f);
            k.Tube(Metal, ring + Vector3.up * 0.105f, cassette + Vector3.up * 0.042f, 0.004f, 0.004f, 6);
            k.Tube(Metal, ring - Vector3.up * 0.105f, jockey, 0.004f, 0.004f, 6);
            k.Ellipsoid(Black, g.RearAxle + new Vector3(0.058f, -0.055f, 0.012f), Quaternion.Euler(-15f, 0f, 0f), new Vector3(0.011f, 0.048f, 0.022f), 8, 10);
            if (classic || kind == BikeKind.Climbing)
            {
                k.Ellipsoid(Metal, crown + new Vector3(0f, -0.015f, 0.035f), Quaternion.identity, new Vector3(0.05f, 0.02f, 0.015f), 6, 12);
                k.Ellipsoid(Metal, Vector3.Lerp(stayTop, g.RearAxle, 0.35f) + Vector3.forward * 0.005f, Quaternion.identity, new Vector3(0.05f, 0.02f, 0.015f), 6, 12);
            }
            else
            {
                k.Ellipsoid(Black, g.FrontAxle + new Vector3(-0.06f, 0.06f, -0.06f), Quaternion.identity, new Vector3(0.012f, 0.03f, 0.04f), 6, 10);
                k.Ellipsoid(Black, g.RearAxle + new Vector3(-0.07f, 0.06f, 0.05f), Quaternion.identity, new Vector3(0.012f, 0.03f, 0.04f), 6, 10);
            }
        }

        void BuildBars(MeshKit k)
        {
            var g = Geometry;
            float w = g.BarHalfWidth, flare = g.Kind == BikeKind.Gravel ? 0.035f : 0.008f;
            var c = g.StemEnd;
            var right = new List<Vector3>
            {
                c + new Vector3(0.07f, 0f, 0.002f), c + new Vector3(w * 0.88f, 0f, 0.008f), c + new Vector3(w, 0.004f, 0.045f),
                c + new Vector3(w, 0.004f, 0.085f), c + new Vector3(w + flare * 0.3f, -0.04f, 0.13f), c + new Vector3(w + flare * 0.7f, -0.105f, 0.12f),
                c + new Vector3(w + flare, -0.135f, 0.075f), c + new Vector3(w + flare, -0.135f, 0.005f),
            };
            var bar = new List<Vector3>();
            for (int i = right.Count - 1; i >= 0; i--) bar.Add(Mirror(right[i]));
            bar.AddRange(right);
            k.Curve(Black, bar, new Vector2(0.0125f, 0.0125f), 10, Vector3.up);
            k.Tube(Black, c + Vector3.left * 0.07f, c + Vector3.right * 0.07f, 0.0155f, 0.0155f);

            for (int side = -1; side <= 1; side += 2)
            {
                var hood = side > 0 ? g.HoodRight : Mirror(g.HoodRight);
                // Remgreep: rubber kap op het stuur met een hendel naar beneden.
                k.Tube(Black, new[] { hood + new Vector3(0f, -0.012f, -0.035f), hood + new Vector3(0f, 0.002f, 0f), hood + new Vector3(0f, 0.012f, 0.032f), hood + new Vector3(0f, 0.022f, 0.048f) },
                    new[] { new Vector2(0.016f, 0.018f), new Vector2(0.017f, 0.021f), new Vector2(0.015f, 0.02f), new Vector2(0.008f, 0.01f) }, 12, Vector3.right);
                k.Curve(Metal, new[] { hood + new Vector3(0f, 0.005f, 0.045f), hood + new Vector3(0f, -0.04f, 0.07f), hood + new Vector3(0.004f * side, -0.1f, 0.065f), hood + new Vector3(0.008f * side, -0.13f, 0.045f) },
                    new Vector2(0.0045f, 0.007f), 8, Vector3.right, 4);
            }
        }

        void BuildWheel(MeshKit k, bool rear)
        {
            var g = Geometry;
            bool classic = g.Kind == BikeKind.Classic, gumwall = classic || g.Kind == BikeKind.Gravel;
            float tyreCenter = g.WheelRadius - g.TyreRadius;
            // Band: bij gravel en klassieker bruine zijwanden met een zwart loopvlak.
            k.Torus(gumwall ? TyreSlot : Black, Vector3.zero, tyreCenter, g.TyreRadius, 64, 12);
            if (gumwall) k.Torus(Black, Vector3.zero, tyreCenter + g.TyreRadius * 0.12f, g.TyreRadius * 0.92f, 64, 12, 0.62f);

            float outer = g.WheelRadius - 2f * g.TyreRadius + 0.004f, depth = g.RimDepth, half = 0.0125f;
            float inner = outer - depth;
            k.Lathe(RimSlot, Vector3.zero, new[]
            {
                new Vector2(outer, 0f), new Vector2(outer - 0.002f, half), new Vector2(outer - depth * 0.5f, half * 0.98f),
                new Vector2(inner + 0.004f, half * 0.6f), new Vector2(inner, 0f), new Vector2(inner + 0.004f, -half * 0.6f),
                new Vector2(outer - depth * 0.5f, -half * 0.98f), new Vector2(outer - 0.002f, -half),
            }, 64);

            // Naaf, cassette en spaken (om en om naar de linker- en rechterflens, licht gekruist).
            float hubHalf = rear ? 0.066f : 0.05f;
            k.Tube(Metal, Vector3.left * hubHalf, Vector3.right * hubHalf, 0.009f, 0.009f, 10);
            k.Tube(Metal, Vector3.left * (hubHalf - 0.02f), Vector3.right * (hubHalf - 0.02f), 0.02f, 0.02f, 14);
            for (int side = -1; side <= 1; side += 2)
                k.Torus(Metal, Vector3.right * ((hubHalf - 0.02f) * side), 0.026f, 0.004f, 20, 6, 0.6f);
            if (rear)
                for (int i = 0; i < 9; i++)
                    k.Torus(Metal, Vector3.right * (0.058f - i * 0.0042f), 0.03f + i * 0.0055f, 0.0028f, 24, 4, 0.5f);
            if (!classic && g.Kind != BikeKind.Climbing)
                k.Torus(Metal, Vector3.left * (hubHalf - 0.008f), 0.072f, 0.008f, 32, 4, 0.18f); // remschijf

            int spokes = rear ? g.Spokes : Mathf.Max(16, g.Spokes - 4);
            float spokeRadius = g.Kind == BikeKind.Aero ? 0.0022f : 0.0016f;
            for (int i = 0; i < spokes; i++)
            {
                float a = i * Mathf.PI * 2f / spokes;
                int side = i % 2 == 0 ? 1 : -1;
                float hubAngle = a + (classic ? 0.35f : 0.15f) * side;
                var hub = new Vector3((hubHalf - 0.02f) * side, Mathf.Cos(hubAngle) * 0.026f, Mathf.Sin(hubAngle) * 0.026f);
                var rim = new Vector3(0f, Mathf.Cos(a) * (inner + 0.002f), Mathf.Sin(a) * (inner + 0.002f));
                k.Tube(g.Kind == BikeKind.Aero || g.Kind == BikeKind.Allround ? Black : Metal,
                    new[] { hub, rim }, new[] { new Vector2(spokeRadius, spokeRadius * (g.Kind == BikeKind.Aero ? 2.2f : 1f)), new Vector2(spokeRadius, spokeRadius) },
                    4, Vector3.right, open: true);
            }
        }

        void BuildCrank(MeshKit k)
        {
            var g = Geometry;
            float L = g.CrankLength;
            k.Tube(Metal, Vector3.left * 0.085f, Vector3.right * 0.085f, 0.012f, 0.012f);
            // Twee kettingbladen met een spin.
            k.Torus(Metal, Vector3.right * 0.065f, 0.098f, 0.008f, 48, 6, 0.3f);
            k.Torus(Metal, Vector3.right * 0.058f, 0.072f, 0.007f, 40, 6, 0.3f);
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI / 4f + i * Mathf.PI / 2f;
                k.FlatTube(Black, Vector3.right * 0.068f, new Vector3(0.066f, Mathf.Cos(a) * 0.09f, Mathf.Sin(a) * 0.09f), 0.004f, 0.009f, 8);
            }
            // Crankarmen: rechts omhoog bij hoek 0, links omlaag.
            k.FlatTube(Black, new Vector3(0.085f, 0f, 0f), new Vector3(0.09f, L, 0f), 0.008f, 0.016f, 10);
            k.FlatTube(Black, new Vector3(-0.085f, 0f, 0f), new Vector3(-0.09f, -L, 0f), 0.008f, 0.016f, 10);
        }

        void BuildPedal(MeshKit k, float sign)
        {
            k.Tube(Metal, new Vector3(-0.02f * sign, 0f, 0f), new Vector3(0.035f * sign, 0f, 0f), 0.006f, 0.006f, 8);
            k.Ellipsoid(Black, new Vector3(0.025f * sign, 0f, 0.005f), Quaternion.identity, new Vector3(0.03f, 0.011f, 0.045f), 6, 12);
        }

        // ---- Rijder ----

        void BuildBody(MeshKit k, AvatarSettings avatar)
        {
            var g = Geometry;
            var torsoDir = (g.ShoulderCenter - g.Pelvis).normalized;
            var back = Vector3.Cross(torsoDir, Vector3.right).normalized;
            if (Vector3.Dot(back, Vector3.up) < 0f) back = -back;

            // Billen en heupen in de koersbroek, op het zadel.
            k.Ellipsoid(Shorts, g.Pelvis + new Vector3(0f, 0.015f, 0.045f), Quaternion.FromToRotation(Vector3.up, torsoDir),
                new Vector3(0.148f, 0.13f, 0.1f), 12, 18);

            // Romp: smalle taille, brede borst en schouders, licht gebolde rug.
            float length = Vector3.Distance(g.Pelvis, g.ShoulderCenter) + 0.06f;
            float[] t = { 0f, 0.2f, 0.42f, 0.62f, 0.8f, 0.92f, 1f };
            Vector2[] torso =
            {
                new Vector2(0.158f, 0.115f), new Vector2(0.148f, 0.105f), new Vector2(0.158f, 0.112f), new Vector2(0.178f, 0.12f),
                new Vector2(0.19f, 0.115f), new Vector2(0.175f, 0.095f), new Vector2(0.1f, 0.06f),
            };
            Vector3 Spine(float f) => g.Pelvis + new Vector3(0f, 0.04f, 0.02f) + torsoDir * (f * length) + back * (0.03f * Mathf.Sin(Mathf.PI * f));
            var spine = new Vector3[t.Length];
            for (int i = 0; i < t.Length; i++) spine[i] = Spine(t[i]);
            k.Tube(Jersey, spine, torso, 20, Vector3.right);

            // Gekleurde band over de borst en de rugzakken, in de accentkleur.
            k.Tube(Accent, new[] { Spine(0.5f), Spine(0.6f) }, new[] { torso[2] * 1.02f + new Vector2(0.004f, 0.004f), torso[3] * 1.02f + new Vector2(0.004f, 0.004f) },
                20, Vector3.right, open: true);
            k.Tube(Accent, new[] { Spine(0.05f), Spine(0.1f) }, new[] { torso[0] * 1.02f + new Vector2(0.003f, 0.003f), torso[1] * 1.04f + new Vector2(0.003f, 0.003f) },
                20, Vector3.right, open: true);

            // Nek, hoofd, helm en bril.
            var neckBase = Spine(0.95f);
            k.Tube(Skin, neckBase, g.HeadCenter + new Vector3(0f, -0.06f, -0.02f), 0.05f, 0.046f, 12);
            k.Ellipsoid(Skin, g.HeadCenter, Quaternion.identity, new Vector3(0.076f, 0.098f, 0.094f), 12, 18);
            k.Ellipsoid(Skin, g.HeadCenter + new Vector3(0f, -0.05f, 0.045f), Quaternion.Euler(20f, 0f, 0f), new Vector3(0.055f, 0.04f, 0.05f), 8, 14); // kin en kaak
            var helmetCenter = g.HeadCenter + new Vector3(0f, 0.022f, -0.012f);
            var helmetTilt = Quaternion.Euler(-14f, 0f, 0f);
            var helmetRadii = new Vector3(0.1f, 0.102f, 0.138f);
            k.Ellipsoid(Helmet, helmetCenter, helmetTilt, helmetRadii, 12, 24, minLatitude: -8f);
            // Ventilatiesleuven: donkere ribben over de helm.
            foreach (float lon in new[] { -0.55f, -0.2f, 0.2f, 0.55f })
            {
                var vent = new List<Vector3>();
                for (int i = 0; i <= 6; i++)
                {
                    float lat = Mathf.Lerp(-150f, -30f, i / 6f) * Mathf.Deg2Rad; // van achter over de top naar voren
                    // Op het oppervlak van de helm: bij een vaste x krimpt de doorsnede in y en z.
                    float across = Mathf.Sqrt(1f - lon * lon);
                    var local = new Vector3(lon * helmetRadii.x, Mathf.Sin(-lat) * helmetRadii.y * across, Mathf.Cos(lat) * helmetRadii.z * across);
                    vent.Add(helmetCenter + helmetTilt * (local * 1.012f));
                }
                k.Curve(Black, vent, new Vector2(0.006f, 0.0035f), 6, Vector3.right, 3);
            }
            var glasses = new List<Vector3>();
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.Lerp(-80f, 80f, i / 8f) * Mathf.Deg2Rad;
                glasses.Add(g.HeadCenter + new Vector3(Mathf.Sin(a) * 0.083f, 0.012f, Mathf.Cos(a) * 0.1f));
            }
            k.Curve(Lens, glasses, new Vector2(0.019f, 0.006f), 8, Vector3.up, 3);

            // Armen naar de remgrepen, ellebogen licht gebogen en naar buiten.
            for (int side = -1; side <= 1; side += 2)
            {
                var shoulder = side > 0 ? g.ShoulderRight : Mirror(g.ShoulderRight);
                var hand = (side > 0 ? g.HoodRight : Mirror(g.HoodRight)) + new Vector3(0f, 0.03f, -0.005f);
                var elbow = CyclistGeometry.Joint(shoulder, hand, g.UpperArm, g.Forearm, new Vector3(0.7f * side, -0.35f, -0.6f));
                k.Ellipsoid(Jersey, shoulder, Quaternion.LookRotation(elbow - shoulder), new Vector3(0.068f, 0.066f, 0.075f), 10, 16);
                var sleeveEnd = Vector3.Lerp(shoulder, elbow, 0.5f);
                k.Tube(Jersey, shoulder, sleeveEnd, 0.06f, 0.05f, 14);
                k.Tube(Accent, new[] { Vector3.Lerp(shoulder, elbow, 0.44f), sleeveEnd }, new[] { new Vector2(0.0535f, 0.0535f), new Vector2(0.0515f, 0.0515f) }, 14, Vector3.right, open: true);
                k.Tube(Skin, Vector3.Lerp(shoulder, elbow, 0.45f), elbow, 0.046f, 0.038f, 12);
                k.Ellipsoid(Skin, elbow, Quaternion.identity, Vector3.one * 0.038f, 8, 12);
                k.Tube(Skin, new[] { elbow, Vector3.Lerp(elbow, hand, 0.35f), hand - (hand - elbow).normalized * 0.04f },
                    Radii(0.038f, 0.04f, 0.028f), 12, Vector3.right);
                // Handschoen om de remgreep.
                k.Ellipsoid(Black, hand + new Vector3(0.004f * side, -0.004f, 0.005f), Quaternion.LookRotation(hand - elbow), new Vector3(0.04f, 0.03f, 0.052f), 8, 12);
            }
        }

        void BuildThigh(MeshKit k)
        {
            float L = Geometry.Thigh;
            // Lokale z langs het bovenbeen, lokale y naar rechts (zie Aim).
            k.Tube(Shorts, new[] { new Vector3(0f, 0f, -0.04f), new Vector3(0f, 0f, 0.1f), new Vector3(0f, 0f, 0.24f), new Vector3(0f, 0f, 0.31f) },
                new[] { new Vector2(0.084f, 0.09f), new Vector2(0.08f, 0.086f), new Vector2(0.068f, 0.072f), new Vector2(0.061f, 0.064f) }, 18, Vector3.up);
            k.Tube(Skin, new[] { new Vector3(0f, 0f, 0.29f), new Vector3(0f, 0f, 0.38f), new Vector3(0f, 0f, L) },
                new[] { new Vector2(0.058f, 0.061f), new Vector2(0.054f, 0.056f), new Vector2(0.047f, 0.05f) }, 14, Vector3.up);
            k.Ellipsoid(Skin, new Vector3(0f, 0f, L), Quaternion.identity, Vector3.one * 0.05f, 8, 12);
        }

        void BuildShin(MeshKit k)
        {
            float L = Geometry.Shin;
            // Kuit: dikker naar achteren (lokale x wijst bij een hangend onderbeen naar achteren).
            k.Tube(Skin, new[] { new Vector3(0f, 0f, 0f), new Vector3(0.008f, 0f, 0.1f), new Vector3(0.006f, 0f, 0.2f), new Vector3(0f, 0f, 0.31f) },
                new[] { new Vector2(0.046f, 0.048f), new Vector2(0.05f, 0.06f), new Vector2(0.044f, 0.052f), new Vector2(0.034f, 0.036f) }, 14, Vector3.up);
            k.Tube(Sock, new[] { new Vector3(0f, 0f, 0.3f), new Vector3(0f, 0f, L + 0.01f) },
                new[] { new Vector2(0.038f, 0.036f), new Vector2(0.036f, 0.034f) }, 14, Vector3.up);
        }

        void BuildFoot(MeshKit k)
        {
            // Schoen: de bal van de voet staat op het pedaal (zie CyclistGeometry.Ankle).
            k.Ellipsoid(Shoe, new Vector3(0f, -0.04f, 0.062f), Quaternion.identity, new Vector3(0.047f, 0.045f, 0.135f), 10, 16);
            k.Ellipsoid(Black, new Vector3(0f, -0.072f, 0.065f), Quaternion.identity, new Vector3(0.05f, 0.012f, 0.138f), 6, 16);
            k.Tube(Black, new Vector3(-0.05f, -0.02f, 0.06f), new Vector3(0.05f, -0.02f, 0.06f), 0.008f, 0.008f, 8); // klittenband
        }

        static Vector2[] Radii(params float[] r)
        {
            var radii = new Vector2[r.Length];
            for (int i = 0; i < r.Length; i++) radii[i] = new Vector2(r[i], r[i]);
            return radii;
        }

        // ---- Materialen ----

        Material[] MakeMaterials(AvatarSettings avatar)
        {
            var slots = new Material[(int)CyclistSlot.Count];
            if (OverrideMaterial != null)
            {
                for (int i = 0; i < slots.Length; i++) slots[i] = OverrideMaterial;
                return slots;
            }
            bool classic = avatar.Bike == BikeKind.Classic;
            Material M(string name, Color color, float smoothness, float metallic = 0f)
            {
                var m = baseMaterial != null ? new Material(baseMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name = name;
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Smoothness", smoothness);
                m.SetFloat("_Metallic", metallic);
                _materials.Add(m);
                return m;
            }
            slots[F] = M("Frame", Hex(avatar.FrameColor), 0.75f, classic ? 0.3f : 0.1f);
            slots[Metal] = M("Metaal", new Color(0.72f, 0.73f, 0.75f), 0.75f, 0.9f);
            slots[Black] = M("Zwart", new Color(0.035f, 0.035f, 0.04f), 0.35f);
            slots[TyreSlot] = M("Zijwand", new Color(0.55f, 0.4f, 0.24f), 0.2f);
            slots[RimSlot] = classic ? slots[Metal] : M("Carbon", new Color(0.05f, 0.05f, 0.055f), 0.7f, 0.2f);
            slots[Jersey] = M("Shirt", Hex(avatar.JerseyColor), 0.3f);
            slots[Accent] = M("Accent", Hex(avatar.AccentColor), 0.35f);
            slots[Shorts] = M("Broek", new Color(0.03f, 0.03f, 0.035f), 0.35f);
            slots[Skin] = M("Huid", Hex(avatar.SkinColor), 0.35f);
            slots[Helmet] = M("Helm", Hex(avatar.HelmetColor), 0.85f);
            slots[Shoe] = M("Schoen", new Color(0.93f, 0.93f, 0.94f), 0.7f);
            slots[Sock] = M("Sok", new Color(0.96f, 0.96f, 0.96f), 0.15f);
            slots[Lens] = M("Glas", new Color(0.05f, 0.06f, 0.1f), 0.95f, 0.7f);
            slots[Bottle] = M("Bidon", Hex(avatar.AccentColor), 0.5f);
            return slots;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;

        void Clear()
        {
            if (_model != null) Destroy(_model.gameObject);
            _model = null;
            foreach (var m in _materials) Destroy(m);
            foreach (var m in _meshes) Destroy(m);
            _materials.Clear();
            _meshes.Clear();
        }
    }
}
