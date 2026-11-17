using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SurfaceMaterial : MonoBehaviour
    {
        public const string DefaultId = "default";

        [SerializeField, Tooltip("材質の Id (例: concrete, metal, wood)。空なら default")]
        private string _id = DefaultId;

        public string Id
        {
            get => Normalize(_id);
            set => _id = Normalize(value);
        }

        public static string IdOf(Collider collider)
        {
            if (collider == null)
            {
                return DefaultId;
            }

            var material = collider.GetComponentInParent<SurfaceMaterial>(true);
            return material != null ? material.Id : DefaultId;
        }

        public static string Normalize(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? DefaultId : id.Trim();
        }
    }
}
