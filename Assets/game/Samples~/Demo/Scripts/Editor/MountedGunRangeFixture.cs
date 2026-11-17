using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 奥の銃座を使う敵の視界は手前の銃座に届かない広さにし、熱を試している間は撃たれず、奥へ近づくと敵が銃座に乗って撃ってくるようにする。
    // 撃った弾が射撃場の物に当たらないよう、ほかの物の無い西の端 (x = -38..-25, z = 9.5..24.5) に置く。
    // 射撃場の床は実行時に作られ NavMesh が無いので、敵の足場は殴ってくる敵 (MeleeEnemyRangeFixture) と同じく Play 中に焼く。
    public sealed class MountedGunRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "MountedGunYard";
        public const string PlayerGunName = "MountedGun_Player";
        public const string EnemyGunName = "MountedGun_Enemy";
        public const string EnemyName = "GunnerEnemy";
        public const string EnemyId = "GunnerEnemy";
        public const string NavFloorName = "NavFloor";
        public const string TargetIdPrefix = "GunTarget";
        public const int FixtureOrder = 610;
        public const int TargetCount = 3;
        public const float TargetSpacing = 2f;

        public const float NavFloorSize = 5f;
        public const float NavFloorThickness = 0.1f;

        // 床の Plane (y=0) と面を重ねると、敵の足がどちらの面に乗るかが定まらないので、わずかに上げる。
        public const float NavFloorTop = 0.02f;

        public const float SightRange = 12f;
        public const float AttackRange = 12f;
        public const float LoseSightSeconds = 2f;
        public const float SearchSeconds = 6f;
        public const float ReactionSeconds = 0.5f;

        public static readonly Vector3 PlayerGunOrigin = new Vector3(-27f, 0f, 12f);
        public static readonly Quaternion PlayerGunRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);

        public static readonly Vector3 TargetCenter = new Vector3(-36f, 0f, 12f);

        public static readonly Vector3 EnemyGunOrigin = new Vector3(-34f, 0f, 21f);
        public static readonly Quaternion EnemyGunRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);

        public static readonly Vector3 NavFloorCenter = new Vector3(-35.5f, 0f, 22f);
        public static readonly Vector3 EnemyGround = new Vector3(-37.5f, 0f, 23.5f);

        public static readonly EnemyBody EnemyBodySize = new EnemyBody(2f, 0.5f, 3.5f, Damageable.DefaultMaxHealth);

        public int Order => FixtureOrder;

        public string Name => RootName;

        public Bounds Footprint
        {
            get
            {
                var bounds = MountedGunFixtureBuilder.FootprintOf(PlayerGunOrigin, PlayerGunRotation);
                bounds.Encapsulate(MountedGunFixtureBuilder.FootprintOf(EnemyGunOrigin, EnemyGunRotation));
                for (var i = 0; i < TargetCount; i++)
                {
                    var ground = TargetGround(i);
                    bounds.Encapsulate(new Bounds(
                        ground + new Vector3(0f, (TargetDummy.HeadLocalPosition.y + TargetDummy.HeadScale.y) / 2f, 0f),
                        new Vector3(1f, TargetDummy.HeadLocalPosition.y + TargetDummy.HeadScale.y, 1f)));
                }

                bounds.Encapsulate(new Bounds(
                    NavFloorCenter + new Vector3(0f, EnemyBodySize.Height / 2f, 0f),
                    new Vector3(NavFloorSize, EnemyBodySize.Height, NavFloorSize)));
                return bounds;
            }
        }

        public static Vector3 TargetGround(int index)
        {
            return TargetCenter + new Vector3(0f, 0f, (index - ((TargetCount - 1) / 2f)) * TargetSpacing);
        }

        public static EnemySpec Spec() => new EnemySpec(SightRange, AttackRange, LoseSightSeconds, SearchSeconds, ReactionSeconds);

        // 撃ち続けて熱が上がり止まるのを試すので、熱で撃つ連射の武器を選ぶ。無ければ弾倉の武器で置き、熱は試せないと知らせる。
        public static WeaponDefinition PickGunWeapon(IReadOnlyList<WeaponDefinition> weapons)
        {
            WeaponDefinition fallback = null;
            foreach (var weapon in weapons)
            {
                if (weapon == null)
                {
                    continue;
                }

                var spec = weapon.ToSpec();
                if (spec.Ammo.Kind == AmmoKind.Heat && spec.FireMode == FireMode.Auto)
                {
                    return weapon;
                }

                fallback ??= weapon;
            }

            return fallback;
        }

        public GameObject Place(Scene scene, GameRules rules)
        {
            return Place(scene, WeaponAssetBuilder.LoadRangeWeapons().Weapons);
        }

        public GameObject Place(Scene scene, IReadOnlyList<WeaponDefinition> weapons)
        {
            var root = new GameObject(RootName);
            try
            {
                SceneManager.MoveGameObjectToScene(root, scene);

                for (var i = 0; i < TargetCount; i++)
                {
                    TargetDummy.Create(
                        $"{TargetIdPrefix}{i}", TargetGround(i), SceneBuildHelpers.FacingTowards(TargetGround(i), PlayerGunOrigin), root.transform);
                }

                var gunWeapon = PickGunWeapon(weapons);
                if (gunWeapon == null)
                {
                    Debug.LogWarning($"[{nameof(MountedGunRangeFixture)}] the range has no weapon for the mounted guns; placing the targets only");
                    return root;
                }

                if (gunWeapon.ToSpec().Ammo.Kind != AmmoKind.Heat)
                {
                    Debug.LogWarning($"[{nameof(MountedGunRangeFixture)}] no automatic heat weapon in the range; the mounted guns use '{gunWeapon.Id}' and do not overheat");
                }

                MountedGunFixtureBuilder.Create(scene, root.transform, PlayerGunName, PlayerGunOrigin, PlayerGunRotation, gunWeapon);
                MountedGunFixtureBuilder.Create(scene, root.transform, EnemyGunName, EnemyGunOrigin, EnemyGunRotation, gunWeapon);
                PlaceEnemy(scene, root.transform);
                return root;
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
        }

        private static void PlaceEnemy(Scene scene, Transform root)
        {
            var navFloor = new GameObject(NavFloorName);
            navFloor.transform.SetParent(root, false);
            navFloor.transform.position = NavFloorCenter;
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            slab.transform.SetParent(navFloor.transform, false);
            slab.transform.localPosition = new Vector3(0f, NavFloorTop - (NavFloorThickness / 2f), 0f);
            slab.transform.localScale = new Vector3(NavFloorSize, NavFloorThickness, NavFloorSize);

            var activated = new List<GameObject>();
            var weapon = MeleeEnemyRangeFixture.FindEnemyWeapon(scene);
            if (weapon != null)
            {
                var enemy = SceneBuildHelpers.CreateEnemy(
                    scene,
                    root,
                    EnemyName,
                    EnemyId,
                    EnemyGround + new Vector3(0f, NavFloorTop, 0f),
                    EnemyGunRotation,
                    new List<Vector3>(),
                    EnemyBodySize,
                    weapon);
                enemy.ApplySpec(Spec());
                enemy.gameObject.AddComponent<EnemyMountedGunner>();
                enemy.gameObject.SetActive(false);
                activated.Add(enemy.gameObject);
            }
            else
            {
                Debug.LogWarning($"[{nameof(MountedGunRangeFixture)}] the range has no weapon for the gunner enemy; placing the floor only");
            }

            root.gameObject.AddComponent<NavMeshBakeActivator>().Configure(navFloor.transform, activated, SceneBuildHelpers.HumanoidAgentTypeId);
        }
    }
}
