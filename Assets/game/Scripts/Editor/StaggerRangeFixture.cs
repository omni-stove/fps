using System;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    // 怯みを試す敵を TPS の射撃場に置く。射撃場の床は実行時に作られて NavMesh が無いので、敵の足元に台を置いて Play の頭に焼く (NavMeshBakeActivator)。
    // 射撃場の他の手順の邪魔をしないよう、敵は標的の列の奥に置き、視界を射撃場の他の場所に届かない広さに絞る。
    public sealed class StaggerRangeFixture : ITpsRangeFixture
    {
        public const string FixtureName = "StaggerRange";
        public const string PadName = "StaggerPad";
        public const string EnemyName = "StaggerEnemy";
        public const string EnemyId = "stagger-enemy";
        public const string HeadName = "Head";
        public const string LegsName = "Legs";

        public const float PadSize = 8f;
        public const float PadHeight = 0.05f;

        public const float EnemyHeight = 2f;
        public const float EnemyRadius = 0.5f;
        public const float EnemySpeed = 3.5f;

        // 何度撃っても倒れずに試し続けられるよう、体力を多くする。
        public const int EnemyMaxHealth = 1000;

        public const float EnemySightRange = 10f;
        public const float EnemyAttackRange = 9f;

        // カプセルの当たり判定より外へはみ出させ、横から撃った弾が胴より先に当たるようにする。
        public const float HeadRadius = 0.55f;
        public const float HeadHeight = 0.75f;
        public static readonly Vector3 LegsCenter = new Vector3(0f, -0.6f, 0f);
        public static readonly Vector3 LegsSize = new Vector3(1.1f, 0.8f, 1.1f);

        public static readonly Vector3 Center = new Vector3(0f, 0f, 21f);

        // 敵の視界 (EnemySightRange) の内側で、台の外の床の上。
        public static readonly Vector3 StandPoint = new Vector3(0f, 0f, 13f);

        public int Order => 500;

        public string Name => FixtureName;

        public Bounds Footprint => new Bounds(Center + (Vector3.up * (EnemyHeight / 2f)), new Vector3(PadSize, EnemyHeight + PadHeight, PadSize));

        public static Vector3 EnemyFeet => Center + (Vector3.up * PadHeight);

        public GameObject Place(Scene scene, GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            // 武器のアセットが無ければ何も置かずに止める (先に WeaponAssetBuilder.Build を走らせる)。
            var weapon = WeaponAssetBuilder.FindInCatalog(WeaponAssetBuilder.LoadCatalog(), EnemyId, WeaponAssetBuilder.RifleId);

            var root = new GameObject(FixtureName);
            try
            {
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.SetPositionAndRotation(Center, Quaternion.identity);
                SceneBuildHelpers.CreateStaticBox(
                    scene, root.transform, PadName, Center + (Vector3.up * (PadHeight / 2f)), new Vector3(PadSize, PadHeight, PadSize));

                var toStand = StandPoint - Center;
                var enemy = SceneBuildHelpers.CreateEnemy(
                    scene,
                    root.transform,
                    EnemyName,
                    EnemyId,
                    EnemyFeet,
                    Quaternion.LookRotation(new Vector3(toStand.x, 0f, toStand.z), Vector3.up),
                    Array.Empty<Vector3>(),
                    new EnemyBody(EnemyHeight, EnemyRadius, EnemySpeed, EnemyMaxHealth),
                    weapon);

                var spec = enemy.CreateSpec();
                enemy.ApplySpec(new EnemySpec(
                    EnemySightRange,
                    EnemyAttackRange,
                    spec.LoseSightSeconds,
                    spec.SearchSeconds,
                    spec.ReactionSeconds,
                    spec.OpeningSpreadDegrees,
                    spec.SettledSpreadDegrees,
                    spec.OpeningShots,
                    spec.CoverHideSeconds,
                    spec.CoverPeekSeconds,
                    spec.CoverHealthFraction,
                    spec.Archetype,
                    spec.Shield,
                    spec.Sniper,
                    spec.Charger,
                    spec.Bomber,
                    spec.StaggerResistance));

                var damageable = enemy.GetComponent<Damageable>();
                var head = AddPart(enemy.transform, HeadName, HitZone.Head, damageable);
                var headCollider = head.AddComponent<SphereCollider>();
                headCollider.radius = HeadRadius;
                head.transform.localPosition = Vector3.up * HeadHeight;

                var legs = AddPart(enemy.transform, LegsName, HitZone.Limb, damageable);
                var legsCollider = legs.AddComponent<BoxCollider>();
                legsCollider.size = LegsSize;
                legs.transform.localPosition = LegsCenter;

                enemy.gameObject.AddComponent<StaggerReaction>();
                enemy.gameObject.SetActive(false);
                root.AddComponent<NavMeshBakeActivator>().Configure(root.transform, new[] { enemy.gameObject }, SceneBuildHelpers.HumanoidAgentTypeId);
                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        private static GameObject AddPart(Transform parent, string name, HitZone zone, Damageable owner)
        {
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.layer = parent.gameObject.layer;
            part.AddComponent<Hitbox>().Initialize(zone, owner);
            return part;
        }
    }
}
