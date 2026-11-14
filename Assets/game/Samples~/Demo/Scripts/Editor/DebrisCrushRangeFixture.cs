using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場に、崩せる柱 (<see cref="PillarName"/>) と、その梁の下に立つ敵 (<see cref="EnemyName"/>) を置く。
    /// 柱は地面に接する根元の部品 1 つで立ち、根元を壊すと上の部品と梁がすべて崩れ落ちて、梁の破片が敵に当たる。
    /// 柱は破片の当たり判定 (<see cref="DestructibleStructure.DebrisCrushEnabled"/>) を入れ、壊れるかはルールの <see cref="GameRules.Destruction"/> で決める。
    /// 射撃場は NavMesh を焼かないので、敵は <see cref="EnemyAgent"/> を付けず、体 (<see cref="Damageable"/>) と胴の <see cref="Hitbox"/> だけのカプセルにする。
    /// </summary>
    public sealed class DebrisCrushRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "DebrisCrush";
        public const string PillarName = "CrushPillar";
        public const string EnemyName = "CrushEnemy";
        public const string EnemyId = "CrushEnemy";

        /// <summary>柱の根元の部品の添字。これを壊すと柱全体が崩れる。</summary>
        public const int BaseIndex = 0;

        /// <summary>柱の高さ (部品の数)。</summary>
        public const int PillarHeight = 6;

        /// <summary>柱の上端から +X へ伸ばす梁の長さ (部品の数)。</summary>
        public const int BeamLength = 3;

        public const float PieceSize = 1f;

        /// <summary>梁の何番目 (柱から数えて 1 から) の下に敵を立たせるか。</summary>
        public const int EnemyUnderBeam = 2;

        public const float EnemyHeight = 2f;
        public const float EnemyRadius = 0.5f;
        public const int EnemyMaxHealth = 100;

        /// <summary>柱の根元の床面の中心。開けた場所・小屋・遮蔽物・出現位置・標的の列と重ならない左奥。</summary>
        public static readonly Vector3 PillarBase = new Vector3(-14f, 0f, 14f);

        /// <summary>敵の足元。</summary>
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

        /// <summary>
        /// parent の子に、<see cref="PillarBase"/> から立つ柱と、その上端から伸びる梁を作る。添字は根元から上へ 0..<see cref="PillarHeight"/>-1、
        /// 続けて梁を柱に近い方から振る。根元だけが地面に接する。
        /// </summary>
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
