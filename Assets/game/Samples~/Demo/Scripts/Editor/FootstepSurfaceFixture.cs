using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public sealed class FootstepSurfaceFixture : ITpsRangeFixture
    {
        public const string FixtureName = "FootstepWalkway";

        public const string PlateNamePrefix = "Plate_";

        public static readonly Vector3 Center = new Vector3(32f, 0f, -16.5f);

        public const float PlateWidth = 3f;

        // 歩幅 (FootstepCadence.DefaultWalkStride) より長くしないと、1 歩も鳴らずに通り過ぎる板が出る。
        public const float PlateLength = 4f;

        public const float PlateThickness = 0.1f;

        // 射撃場の床と同じ高さだと、足元を調べる Raycast がどちらを先に拾うか決まらない。段差として引っかからない分だけ上げる。
        public const float PlateTop = 0.02f;

        // 最初の板に乗る前に射撃場の床で 1 歩鳴らし、板の材質への切り替えをログに出させる。
        public const float RunUp = 2.5f;

        public static readonly IReadOnlyList<string> SurfaceIds = new[]
        {
            FootstepSurfaceAssetStep.MetalId,
            FootstepSurfaceAssetStep.WoodId,
            FootstepSurfaceAssetStep.ConcreteId,
        };

        public static float Length => PlateLength * SurfaceIds.Count;

        public static Vector3 Start => Center + new Vector3(0f, 0f, -((Length / 2f) + RunUp));

        public static Vector3 Finish => Center + new Vector3(0f, 0f, (Length / 2f) + 1f);

        private const float Margin = 0.5f;

        public int Order => 0;

        public string Name => FixtureName;

        public Bounds Footprint
        {
            get
            {
                var near = Start.z - Margin;
                var depth = (Finish.z + Margin) - near;
                return new Bounds(
                    new Vector3(Center.x, PlateTop / 2f, near + (depth / 2f)),
                    new Vector3(PlateWidth, PlateTop, depth));
            }
        }

        public static string PlateName(int index)
        {
            return PlateNamePrefix + SurfaceIds[index];
        }

        public static Vector3 PlateCenter(int index)
        {
            var z = Center.z - (Length / 2f) + (PlateLength * (index + 0.5f));
            return new Vector3(Center.x, PlateTop - (PlateThickness / 2f), z);
        }

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(FixtureName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var size = new Vector3(PlateWidth, PlateThickness, PlateLength);
            for (var i = 0; i < SurfaceIds.Count; i++)
            {
                var plate = SceneBuildHelpers.CreateStaticBox(scene, root.transform, PlateName(i), PlateCenter(i), size);
                SceneBuildHelpers.SetSurfaceMaterial(plate, SurfaceIds[i]);
            }

            return root;
        }
    }
}
