using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed partial class StatusEffectReceiver
    {
        private BlindPerception _blindPerception;

        // 画面を白くする HUD (FlashOverlay) は UI のアセンブリにあり、ここから引けないので、向こうから受け取りに来させる。
        public static event Action<StatusEffectReceiver> LocalPlayerBlinded;

        partial void OnKindStarted(string kindId)
        {
            if (!string.Equals(kindId, StatusEffectKinds.Blind, StringComparison.Ordinal))
            {
                return;
            }

            var agent = GetComponent<EnemyAgent>();
            if (agent != null)
            {
                agent.AddPerceptionContributor(_blindPerception ??= new BlindPerception());
                Debug.Log(LogTag + " blind perception=off target=" + Damageable.Id, this);
            }

            if (Damageable.IsLocalPlayer)
            {
                LocalPlayerBlinded?.Invoke(this);
            }
        }

        partial void OnKindEnded(string kindId)
        {
            if (!string.Equals(kindId, StatusEffectKinds.Blind, StringComparison.Ordinal) || _blindPerception == null)
            {
                return;
            }

            var agent = GetComponent<EnemyAgent>();
            if (agent != null && agent.RemovePerceptionContributor(_blindPerception))
            {
                Debug.Log(LogTag + " blind perception=on target=" + Damageable.Id, this);
            }
        }

        // ドメインの再読み込みを切った Play でも、前の Play の HUD を呼ばないようにする。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLocalPlayerBlinded()
        {
            LocalPlayerBlinded = null;
        }

        // 撃たれたことは分かっても、誰がどこから撃ったかは分からないままにする。
        private sealed class BlindPerception : IPerceptionContributor
        {
            public EnemyPerception Contribute(EnemyPerception perception)
            {
                return new EnemyPerception(
                    perception.Self,
                    null,
                    false,
                    false,
                    null,
                    perception.WasHit,
                    null,
                    perception.HealthFraction,
                    0f,
                    perception.IsSuppressed,
                    perception.SuppressionLevel);
            }
        }
    }
}
