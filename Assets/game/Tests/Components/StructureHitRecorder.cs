using System.Collections.Generic;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
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
