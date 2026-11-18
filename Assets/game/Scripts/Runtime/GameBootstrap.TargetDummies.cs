using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class GameBootstrap
    {
        public static string TargetDummyName(int index)
        {
            return TargetDummyNamePrefix + (index + 1);
        }

        public static Vector3[] TargetDummyPositions(Vector3 playerFeet, Quaternion playerRotation)
        {
            var forward = HorizontalForward(playerRotation);
            var right = Vector3.Cross(Vector3.up, forward);

            var offsets = LineLayout.CenteredOffsets(TargetDummyCount, TargetDummySpacing);
            var positions = new Vector3[offsets.Length];
            for (var i = 0; i < offsets.Length; i++)
            {
                positions[i] = playerFeet + (forward * TargetDummyDistance) + (right * offsets[i]);
            }

            return positions;
        }

        private static Vector3 HorizontalForward(Quaternion rotation)
        {
            // 水平に取るのは、プレイヤーが傾いていても標的が宙に浮いたり埋まったりしないようにするため。
            var forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        private static GameObject CreateTargetDummies(Transform player, GameObject visualPrefab, out TargetDummy[] dummies)
        {
            var feet = player.position + (Vector3.down * (PlayerHeight / 2f));
            var positions = TargetDummyPositions(feet, player.rotation);

            // 標的はプレイヤーの方を向かせる (並べた方向の逆)。
            var facing = Quaternion.LookRotation(-HorizontalForward(player.rotation), Vector3.up);

            var root = new GameObject(TargetsName);
            try
            {
                dummies = new TargetDummy[positions.Length];
                for (var i = 0; i < positions.Length; i++)
                {
                    dummies[i] = TargetDummy.Create(
                        TargetDummyName(i), positions[i], facing, root.transform, TargetDummyMaxHealth, TargetDummyRespawnDelay, visualPrefab);
                }
            }
            catch
            {
                DestroySafely(root);
                throw;
            }

            return root;
        }

        private static void BindDeathBodies(IReadOnlyList<TargetDummy> dummies, GameRules rules)
        {
            var targets = new List<Damageable>(dummies.Count);
            foreach (var dummy in dummies)
            {
                if (dummy != null)
                {
                    targets.Add(dummy.Damageable);
                }
            }

            DeathBodyPresenter.BindAll(targets, rules.DeathBody);
        }

        private static void ApplyZoneDamage(IReadOnlyList<TargetDummy> dummies, GameRules rules)
        {
            foreach (var dummy in dummies)
            {
                if (dummy != null && dummy.Damageable != null)
                {
                    dummy.Damageable.ApplyRules(rules);
                }
            }
        }
    }
}
