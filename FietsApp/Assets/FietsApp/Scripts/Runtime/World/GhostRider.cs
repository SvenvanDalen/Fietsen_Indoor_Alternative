using FietsApp.Recording;
using FietsApp.Training;
using UnityEngine;
using UnityEngine.UI;

namespace FietsApp
{
    /// <summary>
    /// Ghost van je beste poging (story 9.4): een doorzichtige renner die de klim rijdt zoals jij bij je record. Hij
    /// vertrekt zodra jij de voet passeert, rijdt iets naast je, en een label zegt hoeveel je voor of achter ligt.
    /// Het spoor komt uit <see cref="GhostStore"/> en wordt bij een nieuw record vervangen.
    /// </summary>
    public sealed class GhostRider : MonoBehaviour
    {
        [SerializeField] RideController ride;
        [SerializeField] RouteView routeView;
        [Tooltip("Doorzichtig materiaal voor de ghost.")]
        [SerializeField] Material ghostMaterial;
        [Tooltip("Hoe ver de ghost naast je rijdt (m, naar links).")]
        [SerializeField] float sideOffset = 1.4f;

        GhostTrace _trace;
        CyclistModel _ghost;
        float _lastGhostDistance;
        Text _label;
        GameObject _labelPanel;
        double? _footTime; // ElapsedSeconds toen je de voet passeerde

        void Awake()
        {
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            if (routeView == null) routeView = FindAnyObjectByType<RouteView>();
            BuildUi();
        }

        void OnEnable()
        {
            ride.RideStarting += Reload;
            routeView.ClimbChanged += Reload;
        }

        void Start() => Reload();

        void OnDisable()
        {
            ride.RideStarting -= Reload;
            routeView.ClimbChanged -= Reload;
        }

        /// <summary>Nieuwe rit of klim: het spoor van deze klim (opnieuw) inlezen, de ghost wacht weer aan de voet.</summary>
        public void Reload()
        {
            _footTime = null;
            _trace = routeView.Climb != null ? GhostStore.Load(routeView.Climb.Id) : null;
            if (_trace != null && _ghost == null) _ghost = MakeGhost();
            if (_ghost != null) _ghost.gameObject.SetActive(false);
            _lastGhostDistance = -1f;
            _labelPanel.SetActive(false);
        }

        void LateUpdate()
        {
            if (_trace == null || _ghost == null) return;
            float start = routeView.ClimbStart, end = routeView.ClimbEnd;
            float mine = ride.DistanceM - start; // mijn afstand vanaf de voet

            // De klok van de ghost loopt vanaf het moment dat jij de voet passeert (gepauzeerd telt niet, want ElapsedSeconds staat dan stil).
            if (_footTime == null && mine >= 0f && mine < 50f)
                _footTime = ride.ElapsedSeconds - (ride.SpeedMs > 0.1f ? mine / ride.SpeedMs : 0f);
            bool onClimb = _footTime.HasValue && ride.DistanceM <= end + 200f;
            _ghost.gameObject.SetActive(onClimb);
            _labelPanel.SetActive(onClimb && mine <= end - start);
            if (!onClimb) return;

            double time = ride.ElapsedSeconds - _footTime.Value;
            float ghostDistance = start + _trace.DistanceAt(time);
            var here = routeView.Route.Sample(ghostDistance);
            var forward = new Vector3(here.HeadingX, here.GradePercent / 100f, here.HeadingZ).normalized;
            var left = new Vector3(-here.HeadingZ, 0f, here.HeadingX);
            _ghost.transform.SetPositionAndRotation(new Vector3(here.X, here.Elevation, here.Z) + left * sideOffset,
                Quaternion.LookRotation(forward, Vector3.up));
            // De ghost trapt mee zolang hij vooruitkomt.
            float speed = _lastGhostDistance >= 0f && Time.deltaTime > 0f ? (ghostDistance - _lastGhostDistance) / Time.deltaTime : 0f;
            _lastGhostDistance = ghostDistance;
            _ghost.SpeedMs = Mathf.Max(0f, speed);
            _ghost.CadenceRpm = speed > 0.5f && ride.isActiveAndEnabled ? 85f : 0f;

            // Verschil: hoe lang geleden de ghost op mijn plek was (positief = ik lig achter).
            double gap = time - _trace.TimeAt(Mathf.Clamp(mine, 0f, _trace.length));
            string verdict = System.Math.Abs(gap) < 0.5 ? "gelijk" : gap > 0 ? $"{ClimbResult.Clock(gap)} achter" : $"{ClimbResult.Clock(-gap)} voor";
            _label.text = $"Ghost <color=#bbb>(PR {ClimbResult.Clock(_trace.seconds)})</color>   " +
                          (gap > 0.5 ? $"<color=#ff9a7a>{verdict}</color>" : $"<color=#8fe08f>{verdict}</color>");
        }

        /// <summary>Een renner zoals jij (zelfde avatar en fiets), helemaal in het doorzichtige ghostmateriaal.</summary>
        CyclistModel MakeGhost()
        {
            var ghost = new GameObject("Ghost").AddComponent<CyclistModel>();
            ghost.transform.SetParent(transform, false);
            ghost.OverrideMaterial = ghostMaterial;
            return ghost;
        }

        void BuildUi()
        {
            var canvas = UiFactory.Canvas(transform, "Ghost Canvas", 15);
            var panel = UiFactory.Panel(canvas, "Ghost", new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(520f, 40f));
            _labelPanel = panel.gameObject;
            _label = UiFactory.Text(panel, "Verschil", 22, -2f, 36f);
            _label.alignment = TextAnchor.MiddleCenter;
            _labelPanel.SetActive(false);
        }
    }
}
