using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class OfflineParticipants : IParticipants
    {
        public bool TryGet(int id, out Participant participant)
        {
            var gameObject = CombatEvents.OfflineObject(id);
            if (gameObject == null)
            {
                participant = default;
                return false;
            }

            // 敵の撃ち手 (EnemyAgent) と当たり (Damageable) の付く階層がずれていても同じ人として引けるよう、子も探す。
            var damageable = gameObject.GetComponent<Damageable>();
            if (damageable == null)
            {
                damageable = gameObject.GetComponentInChildren<Damageable>(true);
            }

            participant = damageable != null
                ? new Participant(id, damageable.Id, null, damageable.IsLocalPlayer)
                : new Participant(id, gameObject.name, null, false);
            return true;
        }
    }
}
