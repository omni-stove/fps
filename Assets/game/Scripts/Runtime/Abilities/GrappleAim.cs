using UnityEngine;

namespace Shooter.Runtime
{
    public readonly struct GrappleAim
    {
        public GrappleAim(Collider surface, Vector3 point, Vector3 normal, float distance, bool snappedToEdge)
        {
            Surface = surface;
            Point = point;
            Normal = normal;
            Distance = distance;
            SnappedToEdge = snappedToEdge;
        }

        public Collider Surface { get; }

        public Vector3 Point { get; }

        public Vector3 Normal { get; }

        public float Distance { get; }

        public bool SnappedToEdge { get; }
    }
}
