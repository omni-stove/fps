using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    // 狙撃兵を試す仮の敵と、その高台。高台の上面だけを Play 中に焼き (NavMeshBakeActivator)、狙撃兵は上面の足場の間だけを移る。
    // 右奥の隅に置き、視界を出現位置・開けた場所に届かない長さにして、ほかの smoke の手順の間に撃ってこないようにする。
    public sealed class SniperRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "SniperPerch";
        public const string DeckName = "Deck";
        public const string EnemyName = "Sniper";
        public const string EnemyId = "Sniper";
        public const int FixtureOrder = 530;

        public const float DeckHeight = 4f;
        public const float DeckWidth = 3f;
        public const float DeckLength = 7f;

        // 足場は高台の端から少し内側に置く。端に寄せると、足場に着いたかの判定の距離 (EnemyBrain.ArrivalDistance) の内側が NavMesh の外になる。
        public const float PerchInset = 1f;

        public const float SightRange = 18f;
        public const float LoseSightSeconds = 2f;
        public const float SearchSeconds = 4f;
        public const float ReactionSeconds = 0.5f;
        public const int MaxHealth = 80;

        public static readonly Vector3 DeckCenter = new Vector3(22f, 0f, 21f);

        public static readonly EnemyBody EnemyBodySize = new EnemyBody(2f, 0.5f, 3f, MaxHealth);

        public int Order => FixtureOrder;

        public string Name => RootName;

        public Bounds Footprint
        {
            get
            {
                var height = DeckHeight + EnemyBodySize.Height;
                return new Bounds(DeckCenter + new Vector3(0f, height / 2f, 0f), new Vector3(DeckWidth, height, DeckLength));
            }
        }

        public static Vector3[] Perches()
        {
            var top = DeckCenter + new Vector3(0f, DeckHeight, 0f);
            var reach = (DeckLength / 2f) - PerchInset;
            return new[]
            {
                top + new Vector3(0f, 0f, -reach),
                top,
                top + new Vector3(0f, 0f, reach),
            };
        }

        // 散らばりを 0 にするのは、光線を見せた一発が狙った所へ飛ぶようにするため。外れる一発では、光線を見て隠れる意味が無くなる。
        public static EnemySpec Spec() => new EnemySpec(
            SightRange,
            SightRange,
            LoseSightSeconds,
            SearchSeconds,
            ReactionSeconds,
            0f,
            0f,
            0,
            archetype: EnemyArchetype.Sniper,
            sniper: SniperSpec.Default);

        public static Quaternion Facing => SceneBuildHelpers.FacingTowards(DeckCenter, Vector3.zero);

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(RootName);
            try
            {
                SceneManager.MoveGameObjectToScene(root, scene);

                var deck = SceneBuildHelpers.CreateStaticBox(
                    scene,
                    root.transform,
                    DeckName,
                    DeckCenter + new Vector3(0f, DeckHeight / 2f, 0f),
                    new Vector3(DeckWidth, DeckHeight, DeckLength));

                var activated = new List<GameObject>();
                var weapon = MeleeEnemyRangeFixture.FindEnemyWeapon(scene);
                if (weapon != null)
                {
                    var perches = Perches();
                    var enemy = SceneBuildHelpers.CreateEnemy(
                        scene,
                        root.transform,
                        EnemyName,
                        EnemyId,
                        perches[1],
                        Facing,
                        new List<Vector3>(),
                        EnemyBodySize,
                        weapon);
                    enemy.ApplySpec(Spec());

                    var sniper = enemy.gameObject.AddComponent<EnemySniper>();
                    sniper.SetPerches(perches);

                    // 光線は狙撃兵の子にしない。子にすると、狙撃兵の向きに合わせて回り、目から目標へ伸ばした向きがずれる。
                    sniper.AimBeam = EnemySniper.CreateBeamPlaceholder(root.transform);
                    enemy.gameObject.SetActive(false);
                    activated.Add(enemy.gameObject);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(SniperRangeFixture)}] the range has no weapon for the sniper; placing the deck only");
                }

                root.AddComponent<NavMeshBakeActivator>().Configure(deck.transform, activated, SceneBuildHelpers.HumanoidAgentTypeId);
                return root;
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
        }
    }
}
