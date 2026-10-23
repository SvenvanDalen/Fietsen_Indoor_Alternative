using System.Collections.Generic;
using FietsApp.Training;
using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// De renner op zijn fiets (story 7.3). Lichaam, helm, schoenen en frame zijn vloeiend gebeeldhouwd
    /// (<see cref="CyclistShapes"/>); wielen met spaken, crankstel en pedalen zijn losse draaiende delen. Het lichaam is
    /// één huid op vijf botten: de benen trappen mee met de cadans (de knie volgt uit dij- en scheenlengte) en buigen
    /// zonder naden. De wielen draaien met de snelheid. Uiterlijk en fiets komen uit het profiel
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
        float _crankAngle = CyclistGeometry.RestCrank, _wheelAngle;

        void OnEnable() => ProfileStore.Changed += OnProfileChanged;

        void OnDisable() => ProfileStore.Changed -= OnProfileChanged;

        void Start()
        {
            if (_model == null) Build(FollowProfile ? ProfileStore.Current.avatar : new AvatarSettings());
        }

        void OnProfileChanged()
        {
            if (FollowProfile) Build(ProfileStore.Current.avatar);
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

        /// <summary>
        /// Bouwt het model (opnieuw) op voor dit uiterlijk; doet niets als het al zo is. De vormen per fietssoort worden
        /// de eerste keer gemaakt en daarna hergebruikt, dus een andere kleur kiezen is direct.
        /// </summary>
        public void Build(AvatarSettings avatar)
        {
            avatar ??= new AvatarSettings();
            string key = JsonUtility.ToJson(avatar) + (OverrideMaterial != null ? "|" + OverrideMaterial.name : "");
            if (_model != null && key == _built) return;
            var data = CyclistShapes.For(avatar.Bike);
            Clear();
            _built = key;
            Geometry = new CyclistGeometry(avatar.Bike);
            _slots = MakeMaterials(avatar);

            _model = new GameObject("Model").transform;
            _model.SetParent(transform, false);
            var g = Geometry;

            Shaped("Frame", _model, data.Frame);
            Part("Ketting", _model, Vector3.zero, BuildChain);
            _rearWheel = Part("Achterwiel", _model, g.RearAxle, k => BuildWheel(k, rear: true));
            _frontWheel = Part("Voorwiel", _model, g.FrontAxle, k => BuildWheel(k, rear: false));
            _crank = Part("Crankstel", _model, g.BottomBracket, BuildCrank);
            Shaped("Helm", _model, data.Helmet);
            Shaped("Bril", _model, data.Glasses);
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? 1f : -1f;
                _pedal[side] = Part(side == 0 ? "PedaalR" : "PedaalL", _model, Vector3.zero, k => BuildPedal(k, sign));
                _foot[side] = Shaped(side == 0 ? "VoetR" : "VoetL", _model, data.Shoe);
            }
            BuildSkin(data.Body);
            Pose();
        }

        /// <summary>
        /// Het lichaam als vervormbare mesh: botten voor romp en beide dijen en schenen, in de ruststand waarin het
        /// gemodelleerd is (<see cref="CyclistGeometry.RestCrank"/>).
        /// </summary>
        void BuildSkin(MeshData body)
        {
            var g = Geometry;
            var torso = new GameObject("Romp").transform;
            torso.SetParent(_model, false);
            for (int side = 0; side < 2; side++)
            {
                _thigh[side] = new GameObject(side == 0 ? "DijR" : "DijL").transform;
                _shin[side] = new GameObject(side == 0 ? "ScheenR" : "ScheenL").transform;
                _thigh[side].SetParent(_model, false);
                _shin[side].SetParent(_model, false);
                var (hip, knee, ankle, _) = g.Leg(CyclistGeometry.RestCrank, side == 0);
                Aim(_thigh[side], hip, knee);
                Aim(_shin[side], knee, ankle);
            }
            var bones = new Transform[CyclistShapes.BoneCount];
            bones[CyclistShapes.BoneTorso] = torso;
            bones[CyclistShapes.BoneThighRight] = _thigh[0];
            bones[CyclistShapes.BoneShinRight] = _shin[0];
            bones[CyclistShapes.BoneThighLeft] = _thigh[1];
            bones[CyclistShapes.BoneShinLeft] = _shin[1];

            var go = new GameObject("Lijf", typeof(SkinnedMeshRenderer));
            go.transform.SetParent(_model, false);
            var mesh = body.ToMesh("Lijf");
            var bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * go.transform.localToWorldMatrix;
            mesh.bindposes = bindposes;
            _meshes.Add(mesh);
            var skin = go.GetComponent<SkinnedMeshRenderer>();
            skin.bones = bones;
            skin.rootBone = torso;
            skin.sharedMesh = mesh;
            skin.sharedMaterials = _slots;
            skin.localBounds = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(1f, 2f, 1.6f));
            if (OverrideMaterial != null) skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Zet de crank op een hoek (0 = rechterpedaal boven) en de benen erbij, bijvoorbeeld voor een stilstaand beeld.</summary>
        public void ShowCrank(float radians)
        {
            _crankAngle = Mathf.Repeat(radians, Mathf.PI * 2f);
            if (_model != null) Pose();
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
                var (hip, knee, ankle, pitch) = g.Leg(_crankAngle, right);
                Aim(_thigh[side], hip, knee);
                Aim(_shin[side], knee, ankle);
                var footRotation = Quaternion.Euler(pitch, 0f, 0f);
                _foot[side].localPosition = ankle;
                _foot[side].localRotation = footRotation;
                _pedal[side].localPosition = g.Pedal(_crankAngle, right);
                _pedal[side].localRotation = footRotation;
            }
        }

        /// <summary>Zet een bot op <paramref name="from"/> met zijn lokale z naar <paramref name="to"/>; lokale y wijst naar rechts.</summary>
        static void Aim(Transform limb, Vector3 from, Vector3 to)
        {
            limb.localPosition = from;
            limb.localRotation = Quaternion.LookRotation(to - from, Vector3.right);
        }

        // ---- Onderdelen ----

        Transform Part(string name, Transform parent, Vector3 position, System.Action<MeshKit> build)
        {
            var kit = new MeshKit();
            build(kit);
            return Renderer(name, parent, position, kit.Build(name, (int)CyclistSlot.Count));
        }

        Transform Shaped(string name, Transform parent, MeshData data) => Renderer(name, parent, Vector3.zero, data.ToMesh(name));

        Transform Renderer(string name, Transform parent, Vector3 position, Mesh mesh)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            _meshes.Add(mesh);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = _slots;
            if (OverrideMaterial != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void BuildChain(MeshKit k)
        {
            var g = Geometry;
            var ring = g.BottomBracket + Vector3.right * 0.065f;
            var cassette = g.RearAxle + Vector3.right * 0.05f;
            var jockey = g.RearAxle + new Vector3(0.05f, -0.095f, 0.025f);
            k.Tube(Metal, ring + Vector3.up * 0.105f, cassette + Vector3.up * 0.042f, 0.004f, 0.004f, 6);
            k.Tube(Metal, ring - Vector3.up * 0.105f, jockey, 0.004f, 0.004f, 6);
        }

        const int F = (int)CyclistSlot.Frame, Metal = (int)CyclistSlot.Metal, Black = (int)CyclistSlot.Black,
            TyreSlot = (int)CyclistSlot.Tyre, RimSlot = (int)CyclistSlot.Rim, Jersey = (int)CyclistSlot.Jersey,
            Accent = (int)CyclistSlot.Accent, Shorts = (int)CyclistSlot.Shorts, Skin = (int)CyclistSlot.Skin,
            Helmet = (int)CyclistSlot.Helmet, Shoe = (int)CyclistSlot.Shoe, Sock = (int)CyclistSlot.Sock,
            Lens = (int)CyclistSlot.Lens, Bottle = (int)CyclistSlot.Bottle, Hair = (int)CyclistSlot.Hair;

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
            slots[Helmet] = M("Helm", Hex(avatar.HelmetColor), 0.7f);
            slots[Shoe] = M("Schoen", new Color(0.93f, 0.93f, 0.94f), 0.5f);
            slots[Sock] = M("Sok", new Color(0.96f, 0.96f, 0.96f), 0.15f);
            slots[Lens] = M("Glas", new Color(0.05f, 0.06f, 0.1f), 0.95f, 0.7f);
            slots[Bottle] = M("Bidon", Hex(avatar.AccentColor), 0.4f);
            slots[Hair] = M("Haar", new Color(0.13f, 0.09f, 0.06f), 0.25f);
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
