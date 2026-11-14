using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    // 殴る的。敵 (MeleeEnemyRangeFixture) の視界 (EnemySpec.DefaultSightRange) の外に置き、的を殴っている間に撃たれないようにする。
    public sealed class MeleeRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "MeleeDummyYard";
        public const string DummyId = "MeleeDummy";
        public const int FixtureOrder = 500;

        public static readonly Vector3 DummyPosition = new Vector3(2f, 0f, -18f);
        public static readonly Vector3 Size = new Vector3(3f, 2.5f, 8f);

        public int Order => FixtureOrder;

        public string Name => RootName;

        public Bounds Footprint => new Bounds(DummyPosition + new Vector3(0f, Size.y / 2f, 0f), Size);

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = DummyPosition;
            TargetDummy.Create(DummyId, DummyPosition, Quaternion.identity, root.transform);
            return root;
        }
    }
}
