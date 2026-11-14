using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core.Navigation
{
    internal static class NavFunnel
    {
        private const float _samePointSq = 1e-8f;

        public static void StringPull(Vector3 start, Vector3 end, List<(Vector3 Left, Vector3 Right)> portals, List<Vector3> corners)
        {
            corners.Clear();
            var apex = start;
            var left = start;
            var right = start;
            var apexIndex = 0;
            var leftIndex = 0;
            var rightIndex = 0;
            var count = portals.Count + 1;
            for (var i = 1; i <= count; i++)
            {
                Vector3 portalLeft, portalRight;
                if (i < count)
                {
                    (portalLeft, portalRight) = portals[i - 1];
                }
                else
                {
                    portalLeft = end;
                    portalRight = end;
                }

                if (Cross(apex, right, portalRight) >= 0f)
                {
                    if (Same(apex, right) || Cross(apex, left, portalRight) < 0f)
                    {
                        right = portalRight;
                        rightIndex = i;
                    }
                    else
                    {
                        AddCorner(corners, start, left);
                        apex = left;
                        apexIndex = leftIndex;
                        left = apex;
                        right = apex;
                        leftIndex = apexIndex;
                        rightIndex = apexIndex;
                        i = apexIndex;
                        continue;
                    }
                }

                if (Cross(apex, left, portalLeft) <= 0f)
                {
                    if (Same(apex, left) || Cross(apex, right, portalLeft) > 0f)
                    {
                        left = portalLeft;
                        leftIndex = i;
                    }
                    else
                    {
                        AddCorner(corners, start, right);
                        apex = right;
                        apexIndex = rightIndex;
                        left = apex;
                        right = apex;
                        leftIndex = apexIndex;
                        rightIndex = apexIndex;
                        i = apexIndex;
                        continue;
                    }
                }
            }

            if (corners.Count > 0 && Same(corners[corners.Count - 1], end))
            {
                corners.RemoveAt(corners.Count - 1);
            }
        }

        internal static float Cross(Vector3 a, Vector3 b, Vector3 c)
        {
            return (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);
        }

        private static void AddCorner(List<Vector3> corners, Vector3 start, Vector3 corner)
        {
            var previous = corners.Count > 0 ? corners[corners.Count - 1] : start;
            if (!Same(previous, corner))
            {
                corners.Add(corner);
            }
        }

        private static bool Same(Vector3 a, Vector3 b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return dx * dx + dz * dz < _samePointSq;
        }
    }
}
