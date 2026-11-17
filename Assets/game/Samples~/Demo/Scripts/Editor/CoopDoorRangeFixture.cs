using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public sealed class CoopDoorRangeFixture : ITpsRangeFixture
    {
        public const string FixtureName = "CoopDoorRange";
        public const string PadName = "CoopPad";
        public const string PanelName = "CoopDoorPanel";
        public const string LeftWallName = "CoopDoorWallLeft";
        public const string RightWallName = "CoopDoorWallRight";

        public const float PadThickness = 0.05f;

        // 立っている人の体の中心 (GameBootstrap.PlayerSpawn の高さ) を範囲に含めるため、踏み板より高く伸ばす。
        public const float TriggerHeight = 2.2f;

        public const float DoorHeight = 2.5f;
        public const float DoorWidth = 2f;
        public const float DoorThickness = 0.3f;
        public const float SideWallWidth = 1.5f;

        public const int PlaceOrder = 600;

        public static readonly Vector3 PadCenter = new Vector3(32f, 0f, 6f);
        public static readonly Vector2 PadSize = new Vector2(3f, 3f);
        public static readonly Vector3 DoorBase = new Vector3(32f, 0f, 9f);
        public static readonly Vector3 StandPoint = new Vector3(31.2f, 0f, 6f);
        public static readonly Vector3 PartnerPoint = new Vector3(32.8f, 0f, 6f);
        public static readonly Vector3 OutPoint = new Vector3(32.8f, 0f, 3f);

        public int Order => PlaceOrder;

        public string Name => FixtureName;

        public Bounds Footprint => new Bounds(
            new Vector3(32f, DoorHeight / 2f, 6f), new Vector3(DoorWidth + (SideWallWidth * 2f), DoorHeight, 7f));

        public static Vector3 BodyAt(Vector3 ground)
        {
            return ground + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f);
        }

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(FixtureName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(PadCenter, Quaternion.identity);

            var pad = SceneBuildHelpers.CreateAreaPad(PadName, PadCenter, new Vector3(PadSize.x, PadThickness, PadSize.y), TriggerHeight);
            pad.transform.SetParent(root.transform, true);

            var wallCenter = DoorBase + new Vector3(0f, DoorHeight / 2f, 0f);

            // 開くと動くので、CreateStaticBox の静的な箱にはしない。
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = PanelName;
            panel.transform.SetParent(root.transform, false);
            panel.transform.SetPositionAndRotation(wallCenter, Quaternion.identity);
            panel.transform.localScale = new Vector3(DoorWidth, DoorHeight, DoorThickness);

            var sideOffset = (DoorWidth + SideWallWidth) / 2f;
            var wallSize = new Vector3(SideWallWidth, DoorHeight, DoorThickness);
            SceneBuildHelpers.CreateStaticBox(scene, root.transform, LeftWallName, wallCenter + new Vector3(-sideOffset, 0f, 0f), wallSize);
            SceneBuildHelpers.CreateStaticBox(scene, root.transform, RightWallName, wallCenter + new Vector3(sideOffset, 0f, 0f), wallSize);

            var interactable = pad.AddComponent<CoopInteractable>();
            var door = pad.AddComponent<CoopDoor>();
            door.Interactable = interactable;
            door.Panel = panel.transform;
            return root;
        }
    }
}
