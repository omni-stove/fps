using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SideMember : MonoBehaviour
    {
        public const string LogTag = ModeLog.Tag;

        [SerializeField, Min(0), Tooltip("陣営の ID。0 以上。2 チームのモードでは 0 が Alpha、1 が Bravo")]
        private int _sideId;

        private Damageable _damageable;

        public Side Side
        {
            get => new Side(Math.Max(0, _sideId));
            set => _sideId = value.Id;
        }

        public Damageable Damageable
        {
            get
            {
                if (_damageable == null)
                {
                    _damageable = GetComponent<Damageable>();
                }

                return _damageable;
            }
        }

        // 体力が無ければ撃たれないので、生きているとみなす。
        public bool IsAlive => Damageable == null || !Damageable.IsDead;

        public bool IsAllyOf(SideMember other) => other != null && other.Side == Side;

        public Vector3 GroundPosition
        {
            get
            {
                var agent = GetComponent<EnemyAgent>();
                if (agent != null)
                {
                    return agent.GroundPosition;
                }

                return Damageable != null ? Damageable.GroundPoint : transform.position;
            }
        }

        public static SideMember Of(GameObject go)
        {
            return go != null ? go.GetComponentInParent<SideMember>() : null;
        }

        public static SideMember[] FindAll()
        {
            return FindObjectsByType<SideMember>(FindObjectsInactive.Exclude);
        }

        public static SideMember NearestHostile(Side self, Vector3 from, IReadOnlyList<SideMember> members)
        {
            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            var present = new List<SideMember>(members.Count);
            var candidates = new List<HostileCandidate>(members.Count);
            foreach (var member in members)
            {
                if (member == null)
                {
                    continue;
                }

                present.Add(member);
                candidates.Add(new HostileCandidate(member.Side, member.GroundPosition.ToNumerics(), member.IsAlive));
            }

            var index = HostileSelector.Nearest(self, from.ToNumerics(), candidates);
            return index >= 0 ? present[index] : null;
        }

        // 生きていても戻すのは、ラウンドの始めに全員を並べ直すため。
        // プレイヤーの向きは視点の部品 (FirstPersonLook) が持っているので変えない。
        public void Respawn(Vector3 groundPosition, Quaternion rotation)
        {
            // 初期化に失敗した敵は判断を持たず自分で止まっているので、プレイヤーと同じく位置と体力だけ戻す。
            var agent = GetComponent<EnemyAgent>();
            if (agent != null && agent.IsInitialized)
            {
                agent.ResetTo(groundPosition, rotation);
                return;
            }

            // 座ったままだと座席の移動の横取りが次の Tick で体を座席へ戻すので、置き直す前に降ろす。
            var rider = GetComponent<PlayerVehicleRider>();
            if (rider != null && rider.Vehicle != null)
            {
                rider.Vehicle.ForceExit(rider, VehicleLeaveReason.Forced);
            }

            // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
            var controller = GetComponent<CharacterController>();
            var wasEnabled = controller != null && controller.enabled;
            // 切った後は GroundPoint が transform.position に戻り差が 0 になるので、切る前に測る。
            var feetToRoot = transform.position - GroundPosition;
            if (wasEnabled)
            {
                controller.enabled = false;
            }

            transform.position = groundPosition + feetToRoot;

            if (wasEnabled)
            {
                controller.enabled = true;
            }

            if (Damageable != null)
            {
                Damageable.ResetHealth();
            }

            // 予備弾が有限のルールでは、倒れたときの弾数のまま戻ると撃てなくなるので満たし直す。未初期化 (Edit Mode) では作れない。
            var weapon = GetComponentInChildren<WeaponController>();
            if (weapon != null && weapon.IsInitialized)
            {
                weapon.ClearAttachments();
                weapon.ApplyRules(weapon.Rules);
            }
        }

        // 初期化済みの EnemyAgent は判断と NavMeshAgent が倒れたままなので、ResetTo でその場に戻してから体力を合わせる。
        public void Revive(int health)
        {
            if (health <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(health), health, "health must be positive");
            }

            var agent = GetComponent<EnemyAgent>();
            if (agent != null && agent.IsInitialized)
            {
                agent.ResetTo(agent.GroundPosition, transform.rotation);
            }

            if (Damageable != null)
            {
                Damageable.RestoreHealth(health);
            }
        }

        public void SetActing(bool acting)
        {
            // 武器を先に止める。止めると Reload アクションが無効に戻ることがあるので、再開の入力はその後で有効にする (MatchRunner と同じ順)。
            var weapon = GetComponentInChildren<WeaponController>();
            if (weapon != null)
            {
                weapon.enabled = acting;
            }

            var mover = GetComponent<PlayerController>();
            if (mover != null)
            {
                mover.enabled = acting;
            }

            // 初期化に失敗した敵は自分で止まっているので、有効に戻すと壊れた状態で Update が走ってしまう。
            var agent = GetComponent<EnemyAgent>();
            if (agent != null && (!acting || agent.IsInitialized))
            {
                agent.enabled = acting;
            }
        }
    }
}
