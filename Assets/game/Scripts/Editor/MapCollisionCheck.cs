using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using NumericsVector3 = System.Numerics.Vector3;

namespace Fps.Editor
{
    public static class MapCollisionCheck
    {
        public const float DefaultPointTolerance = 2e-3f;
        public const float DefaultNormalTolerance = 1e-2f;

        private const float _minFacing = 0.05f;

        public readonly struct Probe
        {
            public Probe(Vector3 origin, Vector3 direction, float maxDistance)
            {
                Origin = origin;
                Direction = direction;
                MaxDistance = maxDistance;
            }

            public Vector3 Origin { get; }

            public Vector3 Direction { get; }

            public float MaxDistance { get; }

            public override string ToString()
            {
                return "origin " + Format(Origin) + " direction " + Format(Direction) + " max " + MaxDistance.ToString("R");
            }
        }

        public static Bounds BoundsOf(IReadOnlyList<Collider> colliders)
        {
            if (colliders == null || colliders.Count == 0)
            {
                throw new ArgumentException("no colliders", nameof(colliders));
            }

            var bounds = colliders[0].bounds;
            for (var i = 1; i < colliders.Count; i++)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }

            return bounds;
        }

        // 向きのばらばらな光線だけだと床や壁にほぼ斜めにしか当たらないので、真下 (接地) と水平 (射撃) の光線も同じ数だけ混ぜる
        public static List<Probe> SampleProbes(Bounds bounds, int count, int seed)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            var random = new System.Random(seed);
            var outer = bounds;
            outer.Expand(2f);
            var reach = outer.size.magnitude;
            var probes = new List<Probe>(count);
            for (var i = 0; i < count; i++)
            {
                switch (i % 3)
                {
                    case 0:
                        probes.Add(new Probe(RandomPoint(random, outer), RandomDirection(random), reach));
                        break;
                    case 1:
                        var top = RandomPoint(random, outer);
                        top.y = outer.max.y;
                        probes.Add(new Probe(top, Vector3.down, outer.size.y));
                        break;
                    default:
                        var angle = (float)(random.NextDouble() * Math.PI * 2.0);
                        probes.Add(new Probe(RandomPoint(random, outer), new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)), reach));
                        break;
                }
            }

            return probes;
        }

        public static List<string> CompareRaycasts(
            IWorldCollision world,
            IReadOnlyList<Probe> probes,
            int layerMask,
            IReadOnlyList<Collider> sourcesById = null,
            float pointTolerance = DefaultPointTolerance,
            float normalTolerance = DefaultNormalTolerance)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (probes == null)
            {
                throw new ArgumentNullException(nameof(probes));
            }

            Physics.SyncTransforms();
            var mismatches = new List<string>();
            foreach (var probe in probes)
            {
                var unityHit = Physics.Raycast(
                    probe.Origin, probe.Direction, out var expected, probe.MaxDistance, layerMask, QueryTriggerInteraction.Ignore);
                var worldHit = world.Raycast(
                    ToNumerics(probe.Origin), ToNumerics(probe.Direction), probe.MaxDistance, layerMask, out var actual);
                if (!unityHit && !worldHit)
                {
                    continue;
                }

                if (unityHit != worldHit)
                {
                    mismatches.Add(probe + ": Physics " + (unityHit ? "hit " + Describe(expected) : "missed")
                        + ", .fpsmap " + (worldHit ? "hit " + Describe(actual) : "missed"));
                    continue;
                }

                var point = new Vector3(actual.Point.X, actual.Point.Y, actual.Point.Z);
                var normal = new Vector3(actual.Normal.X, actual.Normal.Y, actual.Normal.Z);
                var problems = new List<string>();

                // かすめる光線では、面のわずかな丸めの差が光線の向きに大きく伸びるので、面に対する傾きの分だけ許す
                var facing = Mathf.Max(Mathf.Abs(Vector3.Dot(probe.Direction.normalized, expected.normal)), _minFacing);
                var alongRay = pointTolerance / facing;
                if (Vector3.Distance(point, expected.point) > alongRay)
                {
                    problems.Add("point");
                }

                if (Mathf.Abs(actual.Distance - expected.distance) > alongRay)
                {
                    problems.Add("distance");
                }

                if (Vector3.Distance(normal, expected.normal) > normalTolerance)
                {
                    problems.Add("normal");
                }

                if (actual.Layer != expected.collider.gameObject.layer)
                {
                    problems.Add("layer");
                }

                if (sourcesById != null
                    && (actual.ColliderId < 0 || actual.ColliderId >= sourcesById.Count || sourcesById[actual.ColliderId] != expected.collider))
                {
                    problems.Add("collider");
                }

                if (problems.Count > 0)
                {
                    mismatches.Add(probe + ": " + string.Join("/", problems) + " differ; Physics " + Describe(expected) + ", .fpsmap " + Describe(actual));
                }
            }

            return mismatches;
        }

        private static string Describe(RaycastHit hit)
        {
            return hit.collider.name + " at " + Format(hit.point) + " normal " + Format(hit.normal) + " distance " + hit.distance.ToString("R");
        }

        private static string Describe(WorldHit hit)
        {
            return "#" + hit.ColliderId + " at " + Format(new Vector3(hit.Point.X, hit.Point.Y, hit.Point.Z))
                + " normal " + Format(new Vector3(hit.Normal.X, hit.Normal.Y, hit.Normal.Z)) + " distance " + hit.Distance.ToString("R");
        }

        private static string Format(Vector3 v)
        {
            return "(" + v.x.ToString("R") + ", " + v.y.ToString("R") + ", " + v.z.ToString("R") + ")";
        }

        private static Vector3 RandomPoint(System.Random random, Bounds bounds)
        {
            return new Vector3(
                Mathf.Lerp(bounds.min.x, bounds.max.x, (float)random.NextDouble()),
                Mathf.Lerp(bounds.min.y, bounds.max.y, (float)random.NextDouble()),
                Mathf.Lerp(bounds.min.z, bounds.max.z, (float)random.NextDouble()));
        }

        private static Vector3 RandomDirection(System.Random random)
        {
            var z = (float)((random.NextDouble() * 2.0) - 1.0);
            var angle = (float)(random.NextDouble() * Math.PI * 2.0);
            var ring = Mathf.Sqrt(1f - (z * z));
            return new Vector3(ring * Mathf.Cos(angle), ring * Mathf.Sin(angle), z);
        }

        private static NumericsVector3 ToNumerics(Vector3 v)
        {
            return new NumericsVector3(v.x, v.y, v.z);
        }
    }
}
