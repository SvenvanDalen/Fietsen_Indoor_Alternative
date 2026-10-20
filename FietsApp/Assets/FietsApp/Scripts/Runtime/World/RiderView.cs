using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Zet de rijder op de route op de afgelegde afstand en laat de camera meebewegen (story 3.1c).
    /// Geeft ook de helling van de route door aan de rit, zodat klimmen echt zwaarder rijdt.
    /// </summary>
    public sealed class RiderView : MonoBehaviour
    {
        [SerializeField] RideController ride;
        [SerializeField] RouteView routeView;
        [SerializeField] Transform rider;
        [SerializeField] Camera chaseCamera;
        [SerializeField] Vector3 cameraOffset = new Vector3(0f, 2.6f, -7f);
        [SerializeField, Min(0f)] float lookAhead = 12f;
        [SerializeField, Min(0.1f)] float cameraSmoothing = 4f;

        // Springt de rit verder dan dit (bijvoorbeeld na StartAt), dan springt de camera mee.
        const float SnapDistance = 20f;
        float _lastDistance;

        [Tooltip("Hoe ver de rijder in een bocht hooguit schuin hangt (graden).")]
        [SerializeField, Range(0f, 45f)] float maxLean = 35f;
        float _lean;
        Vector3 _shake; // trilling van de vorige frame, eerst weer weghalen zodat de camera-demping hem niet opstapelt

        void Awake()
        {
            if (ride == null) ride = FindAnyObjectByType<RideController>();
            if (routeView == null) routeView = FindAnyObjectByType<RouteView>();
            if (chaseCamera == null) chaseCamera = Camera.main;
        }

        void Start()
        {
            routeView.ClimbChanged += SnapCamera;
            SnapCamera();
        }

        void OnDestroy()
        {
            if (routeView != null) routeView.ClimbChanged -= SnapCamera;
        }

        void LateUpdate()
        {
            var route = routeView.Route;
            float distance = Mathf.Min(ride.DistanceM, route.Length);
            var here = route.Sample(distance);

            // De helling volgt de route; op de finish is het vlak.
            float grade = ride.DistanceM >= route.Length ? 0f : here.GradePercent;
            if (!Mathf.Approximately(ride.GradePercent, grade)) ride.GradePercent = grade;

            // Voor bochten remt de rijder vanzelf af (afdalingen, story 4.12) en hangt hij schuin in de bocht.
            var corners = routeView.Corners;
            ride.SpeedLimit = corners.SpeedLimitAt(distance);
            ride.Surface = routeView.SurfaceAt(distance); // ondergrond: rolweerstand en trilling (story 4.13)
            float curvature = corners.CurvatureAt(distance);
            float targetLean = Mathf.Clamp(Mathf.Atan(ride.SpeedMs * ride.SpeedMs * curvature / 9.81f) * Mathf.Rad2Deg, -maxLean, maxLean);
            _lean = Mathf.Lerp(_lean, targetLean, 1f - Mathf.Exp(-6f * Time.deltaTime));

            Vector3 position = new Vector3(here.X, here.Elevation, here.Z);
            Vector3 forward = new Vector3(here.HeadingX, here.GradePercent / 100f, here.HeadingZ).normalized;
            // Positieve kromming is een bocht naar links: rol om de rijrichting naar links (positieve z-rotatie).
            rider.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, _lean));

            bool jumped = Mathf.Abs(distance - _lastDistance) > SnapDistance;
            _lastDistance = distance;
            var target = CameraPose(distance);
            float t = jumped ? 1f : 1f - Mathf.Exp(-cameraSmoothing * Time.deltaTime);
            chaseCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(chaseCamera.transform.position - _shake, target.position, t),
                Quaternion.Slerp(chaseCamera.transform.rotation, target.rotation, t));

            // Kasseien en grind: het beeld trilt, harder naarmate je sneller gaat.
            float amplitude = FietsApp.World.Surfaces.Shake(ride.Surface) * Mathf.Clamp(ride.SpeedMs / 8.3f, 0f, 1.5f);
            float time = Time.time * 23f;
            _shake = amplitude * new Vector3(Mathf.PerlinNoise(time, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, time) - 0.5f, 0f) * 2f;
            chaseCamera.transform.position += _shake;
        }

        void SnapCamera()
        {
            _lastDistance = Mathf.Min(ride.DistanceM, routeView.Route.Length);
            var pose = CameraPose(_lastDistance);
            chaseCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        /// <summary>Camera achter de rijder (langs de weg terug, niet door bochten heen), kijkend naar een punt verderop.</summary>
        (Vector3 position, Quaternion rotation) CameraPose(float distance)
        {
            var route = routeView.Route;
            float back = distance + cameraOffset.z;
            var behind = route.Sample(back);
            var ahead = route.Sample(distance + lookAhead);
            var here = route.Sample(distance);

            Vector3 behindPos = new Vector3(behind.X, Mathf.Max(behind.Elevation, here.Elevation), behind.Z);
            // Vóór de start bestaat de route niet: reken terug langs de beginrichting.
            if (back < 0f) behindPos += new Vector3(behind.HeadingX, 0f, behind.HeadingZ) * back;

            Vector3 right = new Vector3(behind.HeadingZ, 0f, -behind.HeadingX);
            Vector3 position = behindPos + Vector3.up * cameraOffset.y + right * cameraOffset.x;
            Vector3 lookAt = new Vector3(ahead.X, ahead.Elevation + 1.2f, ahead.Z);
            return (position, Quaternion.LookRotation(lookAt - position, Vector3.up));
        }
    }
}
