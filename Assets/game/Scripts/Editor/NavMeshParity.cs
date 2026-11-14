using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Core.Navigation;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Editor
{
    // 書き出した .fpsnav で Core が引く経路が、元のシーンで Unity が引く経路と食い違わないかを、ゲームごとのテストから確かめる。
    public static class NavMeshParity
    {
        public const float DefaultSampleRadius = 2f;

        public static NavMeshParityReport Compare(NavMeshFile file, int pairCount, int seed, float lengthRatio, float lengthSlack)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            if (pairCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pairCount), pairCount, "pair count must be positive");
            }

            var points = SamplePoints(pairCount * 2, seed);
            var navigation = new MeshNavigation(file);
            var corePath = new NavPath();
            var unityPath = new NavMeshPath();
            var report = new NavMeshParityReport();
            for (var i = 0; i + 1 < points.Count; i += 2)
            {
                var from = points[i];
                var to = points[i + 1];
                NavMesh.CalculatePath(from, to, NavMesh.AllAreas, unityPath);
                navigation.CalculatePath(from.ToNumerics(), to.ToNumerics(), NavMesh.AllAreas, corePath);
                var unityComplete = unityPath.status == NavMeshPathStatus.PathComplete;
                var coreComplete = corePath.Status == NavPathStatus.Complete;
                report.Pairs++;
                if (unityComplete)
                {
                    report.UnityComplete++;
                }

                if (coreComplete)
                {
                    report.CoreComplete++;
                }

                if (HasLink(corePath))
                {
                    report.CoreWithLink++;
                }

                if (unityComplete != coreComplete)
                {
                    report.Mismatches.Add($"{from} -> {to}: Unity {unityPath.status}, Core {corePath.Status}");
                    continue;
                }

                if (!unityComplete)
                {
                    continue;
                }

                var unityLength = Length(unityPath.corners);
                var coreLength = corePath.Length();
                var difference = Math.Abs(coreLength - unityLength);
                report.MaxLengthDifference = Math.Max(report.MaxLengthDifference, difference);
                if (difference > lengthSlack + (lengthRatio * unityLength))
                {
                    report.Mismatches.Add(string.Format(
                        CultureInfo.InvariantCulture, "{0} -> {1}: Unity length {2:F2} m, Core length {3:F2} m", from, to, unityLength, coreLength));
                }
            }

            return report;
        }

        // 歩ける面の上の点だけを使い、出発点が面の外で Unity だけが失敗する組を比べないようにする。
        public static List<Vector3> SamplePoints(int count, int seed)
        {
            var triangulation = NavMesh.CalculateTriangulation();
            if (triangulation.vertices == null || triangulation.vertices.Length == 0)
            {
                throw new InvalidOperationException("no NavMesh is loaded");
            }

            var bounds = new Bounds(triangulation.vertices[0], Vector3.zero);
            foreach (var vertex in triangulation.vertices)
            {
                bounds.Encapsulate(vertex);
            }

            var random = new System.Random(seed);
            var points = new List<Vector3>(count);
            var attempts = 0;
            while (points.Count < count)
            {
                if (++attempts > count * 100)
                {
                    throw new InvalidOperationException($"found only {points.Count} of {count} points on the NavMesh");
                }

                var probe = new Vector3(
                    Mathf.Lerp(bounds.min.x, bounds.max.x, (float)random.NextDouble()),
                    Mathf.Lerp(bounds.min.y, bounds.max.y, (float)random.NextDouble()),
                    Mathf.Lerp(bounds.min.z, bounds.max.z, (float)random.NextDouble()));
                if (NavMesh.SamplePosition(probe, out var hit, DefaultSampleRadius, NavMesh.AllAreas))
                {
                    points.Add(hit.position);
                }
            }

            return points;
        }

        private static bool HasLink(NavPath path)
        {
            foreach (var point in path.Points)
            {
                if (point.LinkId != NavPathPoint.NoLink)
                {
                    return true;
                }
            }

            return false;
        }

        private static float Length(Vector3[] corners)
        {
            var length = 0f;
            for (var i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return length;
        }
    }
}
