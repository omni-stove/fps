using Fps.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZiplineLine = Fps.Runtime.Traversal.Zipline;

namespace Fps.Editor
{
    // 射撃場の左端 (x = -20) を手前から奥へ下る線。手前の足場に坂で上がり、足場に立ったまま掴まれる高さに始点を置く。
    // 乗り越えの壁 (x = -12..-8) と開けた場所 (右手前) には重ねない。
    public sealed class ZiplineRangeFixture : ITpsRangeFixture
    {
        public const string FixtureName = "Zipline";
        public const string PlatformName = "Zipline_Platform";
        public const string RampName = "Zipline_Ramp";
        public const string StartPoleName = "Zipline_StartPole";
        public const string EndPoleName = "Zipline_EndPole";
        public const string CableName = "Zipline_Cable";

        public const float LineX = -20f;
        public const float PlatformHeight = 3f;
        public const float PlatformSize = 3f;
        public const float PlatformZ = -16f;
        public const float RampRun = 6f;
        public const float RampThickness = 0.2f;
        public const float PoleThickness = 0.2f;
        public const float CableThickness = 0.05f;

        // 体の横 (半径 0.5m) に当たらないよう、支柱は線から横へずらす。
        public const float PoleSideOffset = 1.2f;

        // 足場に立った体 (中心は足場の上 1m) の手 (中心の上 ZiplineMovement.DefaultHangOffset) より少し高くし、
        // ぶら下がった体が足場の縁に引っかからないようにする。
        public static readonly Vector3 StartAnchor = new Vector3(LineX, 5.6f, PlatformZ);

        // ぶら下がった体の足が床から少し浮いた所で着き、降りる。
        public static readonly Vector3 EndAnchor = new Vector3(LineX, 2.6f, 16f);

        public int Order => 100;

        public string Name => FixtureName;

        public Bounds Footprint
        {
            get
            {
                var bounds = new Bounds(new Vector3(LineX, PlatformHeight / 2f, PlatformZ), Vector3.one * PlatformSize);
                bounds.Encapsulate(new Vector3(LineX - PoleSideOffset - PoleThickness, 0f, PlatformZ - (PlatformSize / 2f) - RampRun - 0.5f));
                bounds.Encapsulate(new Vector3(LineX + (PlatformSize / 2f), StartAnchor.y + PoleThickness, EndAnchor.z + PoleThickness));
                return bounds;
            }
        }

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(FixtureName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            SceneBuildHelpers.CreateStaticBox(
                scene, root.transform, PlatformName, new Vector3(LineX, PlatformHeight / 2f, PlatformZ), Vector3.one * PlatformSize);
            CreateRamp(scene, root.transform);
            CreatePole(scene, root.transform, StartPoleName, StartAnchor);
            CreatePole(scene, root.transform, EndPoleName, EndAnchor);
            CreateCable(root.transform);

            var line = root.AddComponent<ZiplineLine>();
            line.SetEnds(StartAnchor, EndAnchor);
            return root;
        }

        private static void CreateRamp(Scene scene, Transform parent)
        {
            var top = new Vector3(LineX, PlatformHeight, PlatformZ - (PlatformSize / 2f));
            var bottom = new Vector3(LineX, 0f, top.z - RampRun);
            var along = top - bottom;
            var rotation = Quaternion.LookRotation(along.normalized, Vector3.up);

            // 上の面が床と足場の縁を結ぶよう、厚みの半分だけ面の内側へ下げる。
            var center = ((top + bottom) / 2f) - (rotation * Vector3.up * (RampThickness / 2f));
            var ramp = SceneBuildHelpers.CreateStaticBox(
                scene, parent, RampName, center, new Vector3(PlatformSize, RampThickness, along.magnitude));
            ramp.transform.rotation = rotation;
        }

        private static void CreatePole(Scene scene, Transform parent, string name, Vector3 anchor)
        {
            var height = anchor.y + PoleThickness;
            SceneBuildHelpers.CreateStaticBox(
                scene,
                parent,
                name,
                new Vector3(anchor.x - PoleSideOffset, height / 2f, anchor.z),
                new Vector3(PoleThickness, height, PoleThickness));
        }

        // 線そのものには当たり判定を付けない。弾や体が線に当たって止まるとジップラインとして使えない。
        private static void CreateCable(Transform parent)
        {
            var cable = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cable.name = CableName;
            Object.DestroyImmediate(cable.GetComponent<Collider>());
            var along = EndAnchor - StartAnchor;
            cable.transform.SetParent(parent, false);
            cable.transform.SetPositionAndRotation((StartAnchor + EndAnchor) / 2f, Quaternion.FromToRotation(Vector3.up, along.normalized));

            // Cylinder の高さは scale.y の 2 倍。
            cable.transform.localScale = new Vector3(CableThickness, along.magnitude / 2f, CableThickness);
        }
    }
}
