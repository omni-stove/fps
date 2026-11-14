using System.Collections.Generic;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Tests.EditMode
{
    /// <summary>テスト用の <see cref="IStructureHittable"/>。渡された被害を順に記録するだけで、何も壊さない。</summary>
    public sealed class StructureHitRecorder : MonoBehaviour, IStructureHittable
    {
        public readonly struct Call
        {
            public Call(Collider collider, Vector3 point, int damage)
            {
                Collider = collider;
                Point = point;
                Damage = damage;
            }

            public Collider Collider { get; }

            public Vector3 Point { get; }

            public int Damage { get; }
        }

        public List<Call> Calls { get; } = new List<Call>();

        public void ApplyStructureHit(Collider collider, Vector3 point, int damage)
        {
            Calls.Add(new Call(collider, point, damage));
        }
    }
}
