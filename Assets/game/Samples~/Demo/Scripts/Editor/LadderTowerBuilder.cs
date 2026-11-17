using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using LadderComponent = Shooter.Runtime.Traversal.Ladder;

namespace Shooter.Editor
{
    public static class LadderTowerBuilder
    {
        public const string PlatformName = "Platform";
        public const string LadderName = "Ladder";
        public const string VisualName = "Visual";

        public const float BarThickness = 0.06f;

        public const float RungSpacing = 0.3f;

        public static GameObject Create(Scene scene, string name, Vector3 baseCenter, Vector3 size, Vector3 ladderSide, float width)
        {
            SceneBuildHelpers.RequireLoaded(scene);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("name must not be empty", nameof(name));
            }

            if (!(size.x > 0f && size.y > 0f && size.z > 0f))
            {
                throw new ArgumentException($"size must be positive: {size}", nameof(size));
            }

            if (!(width > 0f))
            {
                throw new ArgumentException($"width must be positive: {width}", nameof(width));
            }

            var halfDepth = HalfDepthTowards(size, ladderSide);
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(baseCenter, Quaternion.identity);

            SceneBuildHelpers.CreateStaticBox(
                scene, root.transform, PlatformName, baseCenter + (Vector3.up * (size.y / 2f)), size);

            var ladderObject = new GameObject(LadderName);
            ladderObject.transform.SetParent(root.transform, false);
            ladderObject.transform.SetPositionAndRotation(
                baseCenter + (ladderSide * halfDepth), Quaternion.LookRotation(-ladderSide, Vector3.up));
            var ladder = ladderObject.AddComponent<LadderComponent>();
            ladder.Height = size.y;
            ladder.Width = width;
            CreateVisual(ladderObject.transform, size.y, width);
            return root;
        }

        public static Bounds Footprint(Vector3 baseCenter, Vector3 size, Vector3 ladderSide, float approach)
        {
            var bounds = new Bounds(baseCenter + (Vector3.up * (size.y / 2f)), size);
            bounds.Encapsulate(baseCenter + (ladderSide * (HalfDepthTowards(size, ladderSide) + approach)));
            return bounds;
        }

        public static (Vector3 Bottom, Vector3 Facing) LadderPlacement(Vector3 baseCenter, Vector3 size, Vector3 ladderSide)
        {
            return (baseCenter + (ladderSide * HalfDepthTowards(size, ladderSide)), -ladderSide);
        }

        private static float HalfDepthTowards(Vector3 size, Vector3 ladderSide)
        {
            if (ladderSide == Vector3.right || ladderSide == Vector3.left)
            {
                return size.x / 2f;
            }

            if (ladderSide == Vector3.forward || ladderSide == Vector3.back)
            {
                return size.z / 2f;
            }

            throw new ArgumentException($"ladderSide must be one of ±X or ±Z: {ladderSide}", nameof(ladderSide));
        }

        // はしごの面の手前に立つ人とぶつからないよう、見た目は当たり判定を外して面の手前すれすれに置く。
        private static void CreateVisual(Transform ladder, float height, float width)
        {
            var visual = new GameObject(VisualName);
            visual.transform.SetParent(ladder, false);
            var back = -BarThickness / 2f;
            CreateBar(visual.transform, "Rail_Left", new Vector3(-width / 2f, height / 2f, back), new Vector3(BarThickness, height, BarThickness));
            CreateBar(visual.transform, "Rail_Right", new Vector3(width / 2f, height / 2f, back), new Vector3(BarThickness, height, BarThickness));
            var rungs = Mathf.Max(1, Mathf.FloorToInt(height / RungSpacing));
            for (var i = 1; i <= rungs; i++)
            {
                var y = Mathf.Min(height, i * RungSpacing);
                CreateBar(visual.transform, "Rung_" + i, new Vector3(0f, y, back), new Vector3(width, BarThickness, BarThickness));
            }
        }

        private static void CreateBar(Transform parent, string name, Vector3 localPosition, Vector3 localScale)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            UnityEngine.Object.DestroyImmediate(bar.GetComponent<Collider>());
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPosition;
            bar.transform.localScale = localScale;
            bar.isStatic = true;
        }
    }
}
