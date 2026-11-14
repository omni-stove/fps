using UnityEngine;

namespace Fps.Runtime
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

        // 縄の刺さった点 (ワールド)。縁へ寄せたなら縁の高さ。
        public Vector3 Point { get; }

        public Vector3 Normal { get; }

        // 目から照準の先で当たった点までの距離。縁へ寄せる前の点で測る。
        public float Distance { get; }

        public bool SnappedToEdge { get; }
    }
}
