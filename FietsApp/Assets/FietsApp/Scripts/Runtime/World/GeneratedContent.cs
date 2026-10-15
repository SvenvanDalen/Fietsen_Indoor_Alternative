using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FietsApp
{
    /// <summary>
    /// Houdt bij wat een component zelf aanmaakt (objecten en meshes), zodat de wereld ook in de
    /// bewerkmodus zichtbaar is zonder in het scènebestand te belanden. In de bewerkmodus krijgt alles
    /// <see cref="HideFlags.DontSave"/>; <see cref="DestroyAll"/> ruimt het weer op.
    /// </summary>
    sealed class GeneratedContent
    {
        readonly List<Object> _owned = new List<Object>();

        static HideFlags Flags => Application.isPlaying ? HideFlags.None : HideFlags.DontSave | HideFlags.NotEditable;

        public GameObject NewObject(string name, Transform parent, params Type[] components)
        {
            var go = new GameObject(name, components) { hideFlags = Flags };
            go.transform.SetParent(parent, false);
            _owned.Add(go);
            return go;
        }

        public Mesh Track(Mesh mesh)
        {
            mesh.hideFlags = Flags;
            _owned.Add(mesh);
            return mesh;
        }

        public void DestroyAll()
        {
            // Omgekeerd, zodat kinderen vóór hun ouder weg zijn.
            for (int i = _owned.Count - 1; i >= 0; i--)
            {
                if (_owned[i] == null) continue;
                if (Application.isPlaying) Object.Destroy(_owned[i]);
                else Object.DestroyImmediate(_owned[i]);
            }
            _owned.Clear();
        }
    }
}
