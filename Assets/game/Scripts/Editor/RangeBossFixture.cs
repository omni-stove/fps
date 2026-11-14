using System;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    // ボスの受け口 (BossController) を射撃場で試すための仮のボス。個々のボスの作りではなく、
    // 弱点以外が効きにくいこと・体力の半分で段階が進み弱点が出て速くなることを目で確かめるためだけの中身。
    public sealed class RangeBossFixture : ITpsRangeFixture
    {
        public const string FixtureName = "RangeBoss";
        public const string BodyName = "Body";
        public const string HeadName = "Head";
        public const string WeakPointName = "WeakPoint";
        public const string WeakPointId = "Core";
        public const string GuardedPhaseId = "Guarded";
        public const string ExposedPhaseId = "Exposed";

        public const int MaxHealth = 300;
        public const float RespawnDelay = 5f;
        public const float ExposeHealthFraction = 0.5f;
        public const float ExposedSpeedMultiplier = 2.5f;
        public const float StrafeSpeed = 1.5f;
        public const float StrafeHalfWidth = 3f;

        public const double HeadMultiplier = 0.25;
        public const double BodyMultiplier = 0.2;
        public const double LimbMultiplier = 0.1;
        public const double WeakPointMultiplier = 3.0;

        // 標的ダミーの列・乗り越えの壁・小屋から離した左奥。プレイヤーの出現位置から撃てる距離に置く。
        public static readonly Vector3 Position = new Vector3(-14f, 0f, 16f);

        // 出現位置 (原点) の側を向かせる。往復の向き (transform.right) が x 軸に沿うよう、斜めにはしない。
        public static readonly Quaternion Rotation = Quaternion.Euler(0f, 180f, 0f);

        public static readonly Vector3 BodyLocalPosition = new Vector3(0f, 1.25f, 0f);
        public static readonly Vector3 BodyScale = new Vector3(1.5f, 1.25f, 1.5f);
        public static readonly Vector3 HeadLocalPosition = new Vector3(0f, 2.85f, 0f);
        public static readonly Vector3 HeadScale = new Vector3(0.7f, 0.7f, 0.7f);

        // 胴の前面から半分はみ出させ、正面から撃つと胴より先に当たるようにする。
        public static readonly Vector3 WeakPointLocalPosition = new Vector3(0f, 1.4f, 0.75f);
        public static readonly Vector3 WeakPointScale = new Vector3(0.6f, 0.6f, 0.6f);

        public int Order => 100;

        public string Name => FixtureName;

        public Bounds Footprint
        {
            get
            {
                var radius = BodyScale.x / 2f;
                var depth = WeakPointLocalPosition.z + (WeakPointScale.z / 2f);
                var height = HeadLocalPosition.y + (HeadScale.y / 2f);
                return new Bounds(
                    Position + (Vector3.up * (height / 2f)),
                    new Vector3((StrafeHalfWidth + radius) * 2f, height, Mathf.Max(radius, depth) * 2f));
            }
        }

        public static ZoneDamageMultipliers ZoneMultipliers =>
            new ZoneDamageMultipliers(HeadMultiplier, BodyMultiplier, LimbMultiplier, WeakPointMultiplier);

        public static BossPhaseEntry[] Phases()
        {
            return new[]
            {
                new BossPhaseEntry(GuardedPhaseId, 1f, 1f, Array.Empty<string>()),
                new BossPhaseEntry(ExposedPhaseId, ExposeHealthFraction, ExposedSpeedMultiplier, new[] { WeakPointId }),
            };
        }

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(FixtureName);
            try
            {
                root.transform.SetPositionAndRotation(Position, Rotation);

                var damageable = root.AddComponent<Damageable>();
                damageable.Initialize(FixtureName, MaxHealth);

                CreatePart(root.transform, PrimitiveType.Capsule, BodyName, BodyLocalPosition, BodyScale, HitZone.Body, damageable);
                CreatePart(root.transform, PrimitiveType.Sphere, HeadName, HeadLocalPosition, HeadScale, HitZone.Head, damageable);
                var weakPoint = CreatePart(
                    root.transform, PrimitiveType.Sphere, WeakPointName, WeakPointLocalPosition, WeakPointScale, HitZone.WeakPoint, damageable);
                weakPoint.AddComponent<BossWeakPoint>().Initialize(WeakPointId);

                // 最初の段階では隠れている。Play の最初のフレームに一瞬見えないよう、保存する時点で隠しておく。
                weakPoint.SetActive(false);

                root.AddComponent<BossController>().Configure(Phases(), ZoneMultipliers);
                root.AddComponent<BossStrafeMotion>().Configure(StrafeSpeed, StrafeHalfWidth);
                root.AddComponent<TargetDummy>().Initialize(damageable, RespawnDelay);

                SceneManager.MoveGameObjectToScene(root, scene);
                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        private static GameObject CreatePart(
            Transform parent,
            PrimitiveType shape,
            string partName,
            Vector3 localPosition,
            Vector3 localScale,
            HitZone zone,
            Damageable owner)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = localScale;
            part.AddComponent<Hitbox>().Initialize(zone, owner);
            return part;
        }
    }
}
