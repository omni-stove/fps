using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 射撃場は NavMesh を焼かないので、敵は EnemyAgent を付けず、体 (Damageable) と胴の Hitbox だけのカプセルにする。
    public sealed class DebrisCrushRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "DebrisCrush";
        public const string PillarName = "CrushPillar";
        public const string EnemyName = "CrushEnemy";
        public const string EnemyId = "CrushEnemy";

        public const int BaseIndex = 0;

        public const int PillarHeight = 6;

        public const int BeamLength = 3;

        public const float PieceSize = 1f;

        public const int EnemyUnderBeam = 2;

        public const float EnemyHeight = 2f;
        public const float EnemyRadius = 0.5f;
        public const int EnemyMaxHealth = 100;

        public static readonly Vector3 PillarBase = new Vector3(-14f, 0f, 14f);

        public static Vector3 EnemyFeet => PillarBase + new Vector3(EnemyUnderBeam * PieceSize, 0f, 0f);

        public int Order => 100;

        public string Name => RootName;

        public Bounds Footprint
        {
            get
            {
                var minX = PillarBase.x - (PieceSize / 2f);
                var maxX = PillarBase.x + (BeamLength * PieceSize) + (PieceSize / 2f);
                var height = PillarHeight * PieceSize;
                var depth = Mathf.Max(PieceSize, EnemyRadius * 2f);
                return new Bounds(
                    new Vector3((minX + maxX) / 2f, height / 2f, PillarBase.z),
                    new Vector3(maxX - minX, height, depth));
            }
        }

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var pillar = CreatePillar(root.transform, rules != null && rules.Destruction == Destruction.On);
                pillar.DebrisPool = FindDebrisPool(scene);
                CreateEnemy(root.transform);
                return root;
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
        }

        public static DestructibleStructure CreatePillar(Transform parent, bool destructionEnabled)
        {
            var pillarObject = new GameObject(PillarName);
            pillarObject.transform.SetParent(parent, false);
            pillarObject.transform.SetPositionAndRotation(PillarBase, Quaternion.identity);

            var size = Vector3.one * PieceSize;
            var index = 0;
            for (var level = 0; level < PillarHeight; level++)
            {
                DestructiblePiece.Create(
                    pillarObject.transform,
                    "Pillar_" + level,
                    new Vector3(0f, (level + 0.5f) * PieceSize, 0f),
                    size,
                    index++,
                    grounded: level == 0,
                    carveObstacle: false);
            }

            var beamY = (PillarHeight - 0.5f) * PieceSize;
            for (var i = 1; i <= BeamLength; i++)
            {
                DestructiblePiece.Create(
                    pillarObject.transform,
                    "Beam_" + i,
                    new Vector3(i * PieceSize, beamY, 0f),
                    size,
                    index++,
                    carveObstacle: false);
            }

            var structure = pillarObject.AddComponent<DestructibleStructure>();
            structure.DestructionEnabled = destructionEnabled;
            structure.DebrisCrushEnabled = true;
            return structure;
        }

        private static void CreateEnemy(Transform parent)
        {
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = EnemyName;
            capsule.transform.SetParent(parent, false);

            // Capsule のプリミティブは高さ 2・半径 0.5 なので、拡大して大きさを合わせ、中心が Transform の位置なので半分だけ上げる。
            capsule.transform.localScale = new Vector3(EnemyRadius * 2f, EnemyHeight / 2f, EnemyRadius * 2f);
            capsule.transform.SetPositionAndRotation(EnemyFeet + (Vector3.up * (EnemyHeight / 2f)), Quaternion.identity);

            var damageable = capsule.AddComponent<Damageable>();
            damageable.Initialize(EnemyId, EnemyMaxHealth);
            capsule.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
        }

        private static DebrisPool FindDebrisPool(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == TpsRangeSceneBuilder.DebrisPoolName && root.TryGetComponent<DebrisPool>(out var pool))
                {
                    return pool;
                }
            }

            return null;
        }
    }
}
