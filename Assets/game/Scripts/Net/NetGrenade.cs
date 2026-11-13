using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// ネット対戦で投げられたグレネード 1 個。プレハブ (<c>Fps.Editor.NetSceneBuilder</c> が作る NetGrenade) に NetworkObject・
    /// NetworkTransform・Rigidbody・球のコライダーと並べて付ける。
    /// <para>
    /// サーバーだけが物理で動かし (Rigidbody を動的にして初速を与える)、位置と回転は NetworkTransform が全員に配る。
    /// クライアントでは Rigidbody を kinematic にして当たりを切り、届いた位置をなぞるだけにする (自分で転がすとサーバーとずれる)。
    /// 信管 (<see cref="GrenadeThrower.FuseSeconds"/>) もサーバーだけが数え、切れたら試合 (<see cref="NetTeamDeathmatch"/>) に爆発の判定を頼んで Despawn する。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class NetGrenade : NetworkBehaviour
    {
        private readonly List<Collider> _ownColliders = new List<Collider>();
        private Rigidbody _body;
        private NetTeamDeathmatch _match;
        private NetPlayer _thrower;
        private int _throwerId = NetPlayer.NotJoined;
        private Vector3 _launchVelocity;
        private float _remaining;
        private bool _armed;
        private bool _exploded;

        /// <summary>投げた人。抜けていれば null (サーバーだけ)。</summary>
        public NetPlayer Thrower => _thrower;

        /// <summary>投げた人の試合でのプレイヤー Id (サーバーだけ)。<see cref="ServerLaunch"/> の前は <see cref="NetPlayer.NotJoined"/>。</summary>
        public int ThrowerId => _throwerId;

        /// <summary>爆発までの残り秒数 (サーバーだけ)。爆発した後は 0。</summary>
        public float RemainingSeconds => _exploded ? 0f : Mathf.Max(0f, _remaining);

        /// <summary>爆発したか (サーバーだけ)。</summary>
        public bool HasExploded => _exploded;

        private void Awake()
        {
            ResolveParts();
        }

        /// <summary>
        /// サーバーで、Spawn する前に投げた人・初速・信管を決める。初速は Spawn した時点で Rigidbody に与える。
        /// 投げた人の体のコライダー (CharacterController を含む) とはぶつからない。
        /// </summary>
        /// <param name="match">爆発の判定を頼む試合。</param>
        /// <param name="thrower">投げた人。</param>
        /// <param name="velocity">初速 (m/s)。有限値。</param>
        /// <param name="fuseSeconds">爆発までの秒数。0 以上の有限値。</param>
        internal void ServerLaunch(NetTeamDeathmatch match, NetPlayer thrower, Vector3 velocity, float fuseSeconds)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            if (thrower == null)
            {
                throw new ArgumentNullException(nameof(thrower));
            }

            if (!NetMath.IsFinite(velocity))
            {
                throw new ArgumentOutOfRangeException(nameof(velocity), velocity, "velocity must be finite");
            }

            if (!(fuseSeconds >= 0f) || float.IsInfinity(fuseSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(fuseSeconds), fuseSeconds, "fuseSeconds must be a non-negative finite number");
            }

            if (IsSpawned)
            {
                throw new InvalidOperationException("launch the grenade before spawning it");
            }

            ResolveParts();
            _match = match;
            _thrower = thrower;
            _throwerId = thrower.ServerPlayerId;
            _launchVelocity = velocity;
            _remaining = fuseSeconds;
            _armed = true;
            IgnoreCollisionsWith(thrower.gameObject);
        }

        public override void OnNetworkSpawn()
        {
            ResolveParts();
            if (IsServer)
            {
                if (!_armed)
                {
                    // ServerLaunch を経ずに Spawn されたものは誰の物か分からないので、爆発させずに次の Update で消す
                    // (Spawn の通知の最中に Despawn すると、生成の処理が終わる前に消すことになる)
                    Debug.LogWarning(GrenadeThrower.LogTag + " net grenade " + name + " was spawned without a thrower; despawning it", this);
                    return;
                }

                // CCD は動的な Rigidbody にしか効かないので、先に動的にする
                _body.isKinematic = false;
                _body.interpolation = RigidbodyInterpolation.Interpolate;
                _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                _body.linearVelocity = _launchVelocity;
                return;
            }

            _body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            _body.interpolation = RigidbodyInterpolation.None;
            _body.isKinematic = true;
            foreach (var own in _ownColliders)
            {
                if (own != null)
                {
                    // 届いた位置で動くだけの球に、持ち主の移動の予測が引っかかってサーバーとずれないよう当たりを切る
                    own.enabled = false;
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (_match != null)
            {
                _match.ServerForgetGrenade(this);
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || _exploded)
            {
                return;
            }

            if (!_armed)
            {
                _exploded = true;
                NetworkObject.Despawn(true);
                return;
            }

            _remaining -= Time.deltaTime;
            if (_remaining > 0f)
            {
                return;
            }

            ServerExplode();
        }

        /// <summary>
        /// サーバーで今の位置で爆発させ、Despawn する。試合が無くなっていれば判定せずに消すだけ。2 回目以降は何もしない。
        /// </summary>
        internal void ServerExplode()
        {
            if (!IsServer || !_armed || _exploded)
            {
                return;
            }

            _exploded = true;
            var center = transform.position;

            // 自分の球が中心から引く線を遮らないよう、先に当たり判定から外す
            foreach (var own in _ownColliders)
            {
                if (own != null)
                {
                    own.enabled = false;
                }
            }

            try
            {
                if (_match != null && _match.IsSpawned)
                {
                    _match.ServerExplodeGrenade(this, center);
                }
                else
                {
                    Debug.LogWarning(GrenadeThrower.LogTag + " net grenade exploded without a match; no damage is applied", this);
                }
            }
            finally
            {
                if (IsSpawned)
                {
                    NetworkObject.Despawn(true);
                }
            }
        }

        /// <summary>
        /// 投げた要求を受け入れたときのログ。例: <c>[Grenade] net thrown player=1 remaining=2 at=(1.0, 0.6, -21.3) velocity=(0.0, 3.0, 14.0)</c>。
        /// </summary>
        internal static string ThrownLog(int playerId, int remaining, Vector3 position, Vector3 velocity)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} net thrown player={1} remaining={2} at=({3:0.0}, {4:0.0}, {5:0.0}) velocity=({6:0.0}, {7:0.0}, {8:0.0})",
                GrenadeThrower.LogTag,
                playerId,
                remaining,
                position.x,
                position.y,
                position.z,
                velocity.x,
                velocity.y,
                velocity.z);
        }

        /// <summary>
        /// 爆発が 1 人に当たったときのログ。例: <c>[Grenade] net hit attacker=1 victim=2 dmg=150 distance=0.8 outcome=Killed</c>。
        /// </summary>
        internal static string HitLog(int attackerId, int victimId, int damage, float distance, DamageOutcome outcome)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} net hit attacker={1} victim={2} dmg={3} distance={4:0.0} outcome={5}",
                GrenadeThrower.LogTag,
                attackerId,
                victimId,
                damage,
                distance,
                outcome);
        }

        /// <summary>
        /// 爆発のログ。例: <c>[Grenade] net exploded thrower=1 at=(1.0, -0.9, 9.5) damaged=2 killed=1 structures=0</c>。
        /// </summary>
        internal static string ExplodedLog(int throwerId, GrenadeExplosion explosion)
        {
            var c = explosion.Center;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} net exploded thrower={1} at=({2:0.0}, {3:0.0}, {4:0.0}) damaged={5} killed={6} structures={7}",
                GrenadeThrower.LogTag,
                throwerId,
                c.x,
                c.y,
                c.z,
                explosion.Damaged,
                explosion.Killed,
                explosion.StructureHits);
        }

        /// <summary>サーバーが投げる要求を断ったときのログ。例: <c>[Grenade] net rejected player=1 reason=Empty</c>。</summary>
        internal static string RejectedLog(int playerId, GrenadeThrowRejection reason)
        {
            return GrenadeThrower.LogTag + " net rejected player=" + playerId.ToString(CultureInfo.InvariantCulture) + " reason=" + reason;
        }

        // Edit Mode のテストやプレハブの読み込み順で Awake を経ないこともあるので、使う時点でも探す
        private void ResolveParts()
        {
            if (_body == null)
            {
                _body = GetComponent<Rigidbody>();
            }

            if (_ownColliders.Count > 0)
            {
                return;
            }

            GetComponentsInChildren(true, _ownColliders);
            var material = ThrownGrenade.SharedPhysicsMaterial();
            foreach (var own in _ownColliders)
            {
                own.sharedMaterial = material;
            }
        }

        private void IgnoreCollisionsWith(GameObject body)
        {
            var others = body.GetComponentsInChildren<Collider>(true);
            foreach (var own in _ownColliders)
            {
                if (own == null)
                {
                    continue;
                }

                foreach (var other in others)
                {
                    if (other != null)
                    {
                        Physics.IgnoreCollision(own, other);
                    }
                }
            }
        }
    }
}
