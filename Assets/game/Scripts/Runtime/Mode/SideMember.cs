using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// GameObject に陣営 (<see cref="Fps.Core.Side"/>) を持たせる。プレイヤーと <see cref="EnemyAgent"/> に付ける。
    /// <see cref="ModeRunnerBase"/> はこれの付いたものをモードの参加者にし、<see cref="EnemyAgent"/> はこれがあれば
    /// 自分と違う陣営のいちばん近い相手を狙う。体力は同じ GameObject の <see cref="Damageable"/> を使う。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SideMember : MonoBehaviour
    {
        public const string LogTag = ModeLog.Tag;

        [SerializeField, Min(0), Tooltip("陣営の ID。0 以上。2 チームのモードでは 0 が Alpha、1 が Bravo")]
        private int _sideId;

        private Damageable _damageable;

        /// <summary>陣営。</summary>
        public Side Side
        {
            get => new Side(Math.Max(0, _sideId));
            set => _sideId = value.Id;
        }

        /// <summary>同じ GameObject の体力。無ければ null。</summary>
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

        /// <summary>倒れていなければ true。体力が無ければ (撃たれないので) 生きているとみなす。</summary>
        public bool IsAlive => Damageable == null || !Damageable.IsDead;

        /// <summary>同じ陣営なら true。other が null なら false。</summary>
        public bool IsAllyOf(SideMember other) => other != null && other.Side == Side;

        /// <summary>
        /// 位置の比較に使う足元の点。<see cref="EnemyAgent"/> なら NavMesh 上の足元、体力があればそのコライダーの底、無ければ Transform の位置。
        /// </summary>
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

        /// <summary>go かその親に付いた陣営。無ければ null。</summary>
        public static SideMember Of(GameObject go)
        {
            return go != null ? go.GetComponentInParent<SideMember>() : null;
        }

        /// <summary>シーンにある有効な陣営をすべて返す (順番は決まっていない)。</summary>
        public static SideMember[] FindAll()
        {
            return FindObjectsByType<SideMember>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        /// <summary>
        /// members のうち、self と違う陣営で生きている、from にいちばん近いものを返す。いなければ null。
        /// 判定は <see cref="HostileSelector.Nearest"/> に任せる。
        /// </summary>
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

        /// <summary>
        /// 体力を満たして groundPosition (足元) に戻す。<see cref="EnemyAgent"/> なら <see cref="EnemyAgent.Respawn"/> に任せる。
        /// それ以外 (プレイヤー) は CharacterController を切って足元が groundPosition に来るように移し、体力を作り直す。
        /// プレイヤーの向きは視点の部品 (<see cref="FirstPersonLook"/>) が持っているので変えない。
        /// </summary>
        public void Respawn(Vector3 groundPosition, Quaternion rotation)
        {
            var agent = GetComponent<EnemyAgent>();
            if (agent != null)
            {
                agent.Respawn(groundPosition, rotation);
                return;
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
                weapon.ApplyRules(weapon.Rules);
            }
        }

        /// <summary>
        /// 操作と行動を止める・戻す。プレイヤーなら移動 (<see cref="PlayerController"/>) と射撃 (<see cref="WeaponController"/>)、
        /// bot なら <see cref="EnemyAgent"/> を切り替える。倒れている間と試合が終わった後に止める。
        /// </summary>
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
