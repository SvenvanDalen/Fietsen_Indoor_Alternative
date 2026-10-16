using UnityEngine;

namespace FietsApp
{
    /// <summary>
    /// Alle klimmen die het ritmenu toont (story 4.5). Wordt bijgehouden door FietsApp &gt; Klimmen bijwerken,
    /// op volgorde van gebied en naam.
    /// </summary>
    [CreateAssetMenu(menuName = "FietsApp/Klimbibliotheek", fileName = "Bibliotheek")]
    public sealed class ClimbLibrary : ScriptableObject
    {
        public Climb[] climbs = new Climb[0];
    }
}
