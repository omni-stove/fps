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
    /// <see cref="NetTeamDeathmatch"/> のうち、グレネード。投げる要求 (<see cref="NetPlayer"/> の ServerRpc) を確かめてプレハブ (<see cref="NetGrenade"/>) を出し、
    /// 信管が切れたら爆発の範囲ダメージ (<see cref="GrenadeSpec.Explosion"/>) と建造物への被害を与え、爆発を <see cref="NetCombatFeed"/> で全員に伝える。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 数と間隔はサーバーが 1 人ごとの <see cref="ServerGrenadeSupply"/> で持ち、出現のたびに試合のルール (<see cref="ActionRules.Grenade"/>・
    /// <see cref="ActionRules.GrenadeCount"/>) の数に戻す。残りは <see cref="NetPlayer.Grenades"/> で持ち主にだけ同期する。
    /// 投げる位置と初速はクライアントの値を使わず、サーバーの知る目の位置 (<see cref="NetPlayer.EyePosition"/>) とクライアントが送った向きから
    /// オフラインの <see cref="GrenadeThrower"/> と同じ決め方で決める。
    /// 信管・間隔・初速・威力と半径は <see cref="GrenadeSpec.Default"/> を使い、NetPlayer のプレハブの <see cref="GrenadeThrower"/> の Inspector の値は読まない。
    /// </para>
    /// <para>
    /// 爆発のダメージは参加中で生きている (ダウン中を含む) 全員に、体のカプセルのいちばん近い点までの距離で <see cref="ExplosionDamage.At"/> から決め、
    /// <see cref="ApplyDamage"/> を通す (味方撃ち・出現直後の無敵・ダウンは射撃と同じく Session のルールに従う)。投げた本人も巻き込む。
    /// 中心から体の中ほどと頭の高さへの線が、どちらもプレイヤーとグレネード以外の物に遮られていれば当たらない。
    /// 投げた人が抜けていれば、攻撃者のいないダメージ (倒れたら自滅) として与える。ラグ補償はかけない (爆発はサーバーの時刻で起きる)。
    /// </para>
    /// </remarks>
    public sealed partial class NetTeamDeathmatch
    {
        // 頭の高さへの線を、カプセルの頂からどれだけ下へ引くか (m)。オフラインの ThrownGrenade と同じく、身をかがめて頭だけ出した相手にも当てる
        private const float _explosionTopProbeInset = 0.1f;

        // これより短い向きは投げる向きとして扱わない
        private const float _minThrowDirectionSqrMagnitude = 1e-8f;

        [SerializeField, Tooltip("投げたグレネードのプレハブ (Assets/game/Prefabs/Net/NetGrenade.prefab)。NetworkObject と NetGrenade 必須。未設定ならルールで On でも投げられない")]
        private GameObject _grenadePrefab;

        private const float _grenadeThrowJitterSeconds = 0.15f;

        // 1 人ごとのグレネードの残り (サーバーだけ)。プレイヤー Id から引く
        private readonly Dictionary<int, ServerGrenadeSupply> _grenadeSupplies = new Dictionary<int, ServerGrenadeSupply>();

        // 飛んでいるグレネード (サーバーだけ)。試合が消えるときに一緒に消す
        private readonly List<NetGrenade> _liveGrenades = new List<NetGrenade>();

        // 爆発 1 回で回す相手の写し。ApplyDamage が倒れた人の処理から一覧に触れても回し続けられるよう、写してから回す
        private readonly List<NetPlayer> _explosionTargets = new List<NetPlayer>();

        // 試合の開始時に決める。ルールで On でもプレハブが使えなければ false にし、全員を 0 個にする
        private bool _grenadesAvailable;

        /// <summary>
        /// 投げたグレネードのプレハブ。サーバーは試合の開始時 (Spawn 時) に <see cref="TryValidateGrenadePrefab"/> で確かめ、
        /// 使えなければルールで On でもグレネードを配らない。<see cref="NetBootstrap"/> はこれをネットワークのプレハブとして登録する。
        /// プレハブでは <c>Fps.Editor.NetSceneBuilder</c> が配線し、テストでは Spawn 前にここへ渡す。
        /// </summary>
        public GameObject GrenadePrefab
        {
            get => _grenadePrefab;
            set => _grenadePrefab = value;
        }

        /// <summary>サーバーで飛んでいるグレネードの数。</summary>
        internal int LiveGrenadeCount => _liveGrenades.Count;

        /// <summary>
        /// prefab が投げたグレネードとして Spawn できるか (NetworkObject と <see cref="NetGrenade"/> を持つか)。
        /// できなければ error に理由を入れて false。
        /// </summary>
        public static bool TryValidateGrenadePrefab(GameObject prefab, out string error)
        {
            if (prefab == null)
            {
                error = "no grenade prefab is assigned";
                return false;
            }

            if (prefab.GetComponent<NetworkObject>() == null)
            {
                error = "grenade prefab '" + prefab.name + "' has no NetworkObject";
                return false;
            }

            if (prefab.GetComponent<NetGrenade>() == null)
            {
                error = "grenade prefab '" + prefab.name + "' has no NetGrenade";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>direction へ投げたときの初速。視線の向きの <see cref="GrenadeSpec.ThrowSpeed"/> に上向きの <see cref="GrenadeSpec.ThrowLiftSpeed"/> を足す。</summary>
        internal static Vector3 GrenadeVelocity(Vector3 direction)
        {
            var spec = GrenadeSpec.Default;
            return (direction * spec.ThrowSpeed) + (Vector3.up * spec.ThrowLiftSpeed);
        }

        /// <summary>
        /// グレネードを出す位置。eye から direction (単位ベクトル) へ <see cref="GrenadeThrower.ReleaseDistance"/> 進んだ所で、
        /// 間に壁があれば球が埋まらない壁の手前。thrower の下のコライダーと、ほかのグレネードは壁として扱わない。
        /// </summary>
        internal static Vector3 GrenadeReleasePoint(Vector3 eye, Vector3 direction, Transform thrower)
        {
            var distance = GrenadeThrower.ReleaseDistance;
            var maxDistance = GrenadeThrower.ReleaseDistance + ThrownGrenade.BodyRadius;
            foreach (var hit in Physics.RaycastAll(eye, direction, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if ((thrower != null && hit.collider.transform.IsChildOf(thrower)) || hit.collider.GetComponentInParent<NetGrenade>() != null)
                {
                    continue;
                }

                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - ThrownGrenade.BodyRadius));
            }

            return eye + (direction * distance);
        }

        /// <summary>
        /// point から、bottom (足元) と top (頭のてっぺん) を両端に持つ半径 radius の縦のカプセルの表面までの距離。中にあれば 0。
        /// カプセルが半径の 2 倍より低ければ、両端の中点を中心にした球として測る。
        /// </summary>
        internal static float DistanceToCapsule(Vector3 point, Vector3 bottom, Vector3 top, float radius)
        {
            if (!(radius >= 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be a non-negative finite number");
            }

            var axis = top - bottom;
            var length = axis.magnitude;
            Vector3 a;
            Vector3 b;
            if (length <= radius * 2f)
            {
                a = b = Vector3.Lerp(bottom, top, 0.5f);
            }
            else
            {
                var unit = axis / length;
                a = bottom + (unit * radius);
                b = top - (unit * radius);
            }

            var segment = b - a;
            var segmentSqr = segment.sqrMagnitude;
            var t = segmentSqr > 0f ? Mathf.Clamp01(Vector3.Dot(point - a, segment) / segmentSqr) : 0f;
            var closest = a + (segment * t);
            return Mathf.Max(0f, Vector3.Distance(point, closest) - radius);
        }

        /// <summary>
        /// player の今の体のカプセル (足元・頭のてっぺん・半径)。姿勢 (<see cref="NetPlayer.Stance"/>) があればその寸法、無ければ CharacterController の寸法。
        /// どちらも無ければ本体の位置に寸法 0 のカプセルを返して false。
        /// </summary>
        internal static bool TryGetBodyCapsule(NetPlayer player, out Vector3 bottom, out Vector3 top, out float radius)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            var position = player.transform.position;
            var controller = player.GetComponent<CharacterController>();
            var stance = player.Stance;
            StanceShape shape;
            if (stance != null)
            {
                shape = stance.Current;
            }
            else if (controller != null)
            {
                shape = new StanceShape(controller.height, controller.center.y, 0f);
            }
            else
            {
                bottom = position;
                top = position;
                radius = 0f;
                return false;
            }

            var scale = player.transform.lossyScale;
            radius = player.BodyRadius;
            if (!(radius > 0f) && controller != null)
            {
                radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            }

            if (!(radius >= 0f) || float.IsInfinity(radius))
            {
                radius = 0f;
            }

            bottom = position + (Vector3.up * (shape.BottomY * scale.y));
            top = position + (Vector3.up * (shape.TopY * scale.y));
            return true;
        }

        /// <summary>
        /// center から体の中ほど bodyCenter と頭の高さ head への線が、どちらもプレイヤーとグレネード以外のコライダー (トリガーを除く) に遮られていれば true。
        /// </summary>
        internal static bool IsExplosionObstructed(Vector3 center, Vector3 bodyCenter, Vector3 head)
        {
            return IsExplosionLineObstructed(center, bodyCenter) && IsExplosionLineObstructed(center, head);
        }

        /// <summary>
        /// サーバーで、thrower が direction へグレネードを投げる。参加していて生きている (ダウン中でない) 試合中の人だけ。
        /// 向き・ルール・残り・前に投げてからの間隔・プレハブを確かめ、通ればプレハブを出して 1 つ減らし、残りを持ち主に同期し、出現直後の無敵を解いて
        /// <c>[Grenade] net thrown player=.. remaining=..</c> を出す。断ったら <c>[Grenade] net rejected player=.. reason=..</c> を出す (連打では間引く)。
        /// </summary>
        internal void ServerThrowGrenade(NetPlayer thrower, Vector3 direction)
        {
            if (!IsServer || _session == null || thrower == null || !IsJoined(thrower))
            {
                return;
            }

            // ダウン中・倒れている人の要求は、射撃と同じく理由を出さずに捨てる
            var playerId = thrower.ServerPlayerId;
            if (!CanFire(_session, playerId))
            {
                return;
            }

            if (!NetMath.IsFinite(direction) || direction.sqrMagnitude < _minThrowDirectionSqrMagnitude)
            {
                WarnGrenadeRejected(playerId, GrenadeThrowRejection.InvalidDirection);
                return;
            }

            if (!_grenadeSupplies.TryGetValue(playerId, out var supply))
            {
                WarnGrenadeRejected(playerId, GrenadeThrowRejection.Disabled);
                return;
            }

            var now = NetworkManager.ServerTime.Time;
            if (double.IsNaN(now) || double.IsInfinity(now))
            {
                return;
            }

            var rejection = supply.Check(now);
            if (rejection != GrenadeThrowRejection.None)
            {
                WarnGrenadeRejected(playerId, rejection);
                return;
            }

            if (!TryValidateGrenadePrefab(_grenadePrefab, out _))
            {
                WarnGrenadeRejected(playerId, GrenadeThrowRejection.NoPrefab);
                return;
            }

            direction.Normalize();
            var position = GrenadeReleasePoint(thrower.EyePosition(), direction, thrower.transform);
            var velocity = GrenadeVelocity(direction);
            if (!TrySpawnGrenade(thrower, position, velocity, out var grenade))
            {
                return;
            }

            supply.Take(now);
            thrower.ServerSetGrenades(supply.State);
            _liveGrenades.Add(grenade);

            // 撃ったときと同じく、投げた時点で解く
            ServerEndSpawnProtection(playerId);
            Debug.Log(NetGrenade.ThrownLog(playerId, supply.Remaining, position, velocity), this);
        }

        /// <summary>
        /// サーバーで、grenade が center で爆発した結果を与える。参加中で生きている全員に範囲ダメージ、ルールで壊せるなら建造物に被害を与え、
        /// ダメージを与えた相手ごとに本人たちへヒット・被弾を、全員に爆発を送り、<c>[Grenade] net exploded ..</c> を出す。
        /// 倒したらキルフィードにはグレネード (<see cref="GrenadeThrower.WeaponId"/>) を載せる。<see cref="NetGrenade"/> が信管が切れたときに呼ぶ。
        /// </summary>
        internal void ServerExplodeGrenade(NetGrenade grenade, Vector3 center)
        {
            if (grenade == null)
            {
                throw new ArgumentNullException(nameof(grenade));
            }

            _liveGrenades.Remove(grenade);
            if (!IsServer || _session == null)
            {
                return;
            }

            var thrower = grenade.Thrower;
            if (thrower != null && !IsJoined(thrower))
            {
                thrower = null;
            }

            var spec = GrenadeSpec.Default;
            var explosion = spec.Explosion;
            var damaged = 0;
            var killed = 0;
            _explosionTargets.Clear();
            foreach (var player in _playerById.Values)
            {
                if (player != null)
                {
                    _explosionTargets.Add(player);
                }
            }

            try
            {
                foreach (var victim in _explosionTargets)
                {
                    var outcome = ApplyExplosionTo(thrower, victim, center, explosion);
                    if (outcome == DamageOutcome.Ignored)
                    {
                        continue;
                    }

                    damaged++;
                    if (outcome == DamageOutcome.Killed)
                    {
                        killed++;
                    }
                }
            }
            finally
            {
                _explosionTargets.Clear();
            }

            var structures = _session.Spec.Rules.Destruction == Destruction.On
                ? StructureDamage.ApplyAround(center, spec.StructureDamageAtCenter, explosion.Radius)
                : 0;

            if (_combatFeed != null)
            {
                _combatFeed.ServerExplosion(center, thrower, GrenadeThrower.WeaponId);
            }

            Debug.Log(NetGrenade.ExplodedLog(grenade.ThrowerId, new GrenadeExplosion(center, damaged, killed, structures)), this);
        }

        /// <summary>grenade を飛んでいる一覧から外す。Despawn されたときに <see cref="NetGrenade"/> が呼ぶ。</summary>
        internal void ServerForgetGrenade(NetGrenade grenade)
        {
            _liveGrenades.Remove(grenade);
        }

        // 爆発 1 回を victim に与える。ダメージを与えなかったら Ignored
        private DamageOutcome ApplyExplosionTo(NetPlayer thrower, NetPlayer victim, Vector3 center, ExplosionDamage explosion)
        {
            if (!IsJoined(victim))
            {
                return DamageOutcome.Ignored;
            }

            var victimId = victim.ServerPlayerId;
            if (!_session.HasPlayer(victimId) || !_session.IsAlive(victimId))
            {
                return DamageOutcome.Ignored;
            }

            TryGetBodyCapsule(victim, out var bottom, out var top, out var radius);
            var distance = DistanceToCapsule(center, bottom, top, radius);
            if (!explosion.Reaches(distance))
            {
                return DamageOutcome.Ignored;
            }

            var bodyCenter = Vector3.Lerp(bottom, top, 0.5f);
            var head = top - (Vector3.up * _explosionTopProbeInset);
            if (head.y < bodyCenter.y)
            {
                head = bodyCenter;
            }

            var obstructed = IsExplosionObstructed(center, bodyCenter, head);
            var damage = explosion.At(distance, obstructed);
            if (damage <= 0)
            {
                if (obstructed)
                {
                    Debug.Log(GrenadeThrower.LogTag + " net blocked victim=" + victimId.ToString(CultureInfo.InvariantCulture), this);
                }

                return DamageOutcome.Ignored;
            }

            // 投げた人が抜けていれば、ApplyDamage は本人のダメージ (倒れたら自滅) として数えるので、キルフィードの撃った人も本人にそろえる
            var attackerId = thrower != null ? thrower.ServerPlayerId : victimId;
            DamageOutcome outcome;
            _killShot = new KillShot(attackerId, victimId, GrenadeThrower.WeaponId, HitZone.Body);
            try
            {
                outcome = ApplyDamage(thrower, victim, damage);
            }
            finally
            {
                _killShot = null;
            }

            if (outcome == DamageOutcome.Ignored)
            {
                return outcome;
            }

            var hurt = thrower != null && IsReflected(thrower, victim) ? thrower : victim;
            hurt.ServerSetLastHitDirection(hurt.transform.position - center);
            SendExplosionHit(thrower, victim, outcome, damage, center);
            Debug.Log(NetGrenade.HitLog(attackerId, victimId, damage, distance, outcome), this);
            return outcome;
        }

        /// <summary>
        /// 爆発 1 人分の結果を本人たちに知らせる。自分の爆発・投げた人が抜けた爆発なら、体力の減った本人に被弾 (爆発の中心から) だけを、
        /// 跳ね返した味方撃ちなら投げた本人に被弾 (位置なし) だけを、それ以外は投げた人にヒット、受けた人に被弾 (爆発の中心から) を送る。
        /// </summary>
        private void SendExplosionHit(NetPlayer thrower, NetPlayer victim, DamageOutcome outcome, int damage, Vector3 center)
        {
            if (_combatFeed == null || outcome == DamageOutcome.Ignored)
            {
                return;
            }

            if (thrower == null || thrower == victim)
            {
                _combatFeed.ServerDamageTaken(victim, center, damage);
                return;
            }

            if (IsReflected(thrower, victim))
            {
                _combatFeed.ServerDamageTaken(thrower, null, damage);
                return;
            }

            var confirm = new HitConfirm(damage, HitZone.Body, killed: outcome == DamageOutcome.Killed, downed: outcome == DamageOutcome.Downed);
            _combatFeed.ServerHitConfirm(thrower, confirm);
            _combatFeed.ServerDamageTaken(victim, center, damage);
        }

        /// <summary>
        /// playerId の残りを試合のルールの数に戻し、持ち主に同期する。出現・リスポーンと試合の開始で呼ぶ。
        /// プレハブが使えない試合では、ルールで On でも 0 個 (使わない) にする。
        /// </summary>
        private void ServerRefillGrenades(int playerId, NetPlayer player)
        {
            if (_session == null || player == null)
            {
                return;
            }

            if (!_grenadeSupplies.TryGetValue(playerId, out var supply))
            {
                // RPC の届く時刻は揺れるので、持ち主の時計どおりに送られた次の要求が間隔より少し早く届いても捨てない
                supply = new ServerGrenadeSupply(GrenadeSpec.Default.ThrowSeconds, _grenadeThrowJitterSeconds);
                _grenadeSupplies.Add(playerId, supply);
            }

            var actions = _session.Spec.Rules.Actions;
            supply.Refill(_grenadesAvailable ? actions : actions.WithGrenade(Grenade.Off));
            player.ServerSetGrenades(supply.State);
        }

        /// <summary>
        /// 試合の開始時に、ルールでグレネードを使うならプレハブを確かめる。使えなければエラーを出し、この試合ではグレネードを配らない
        /// (試合そのものは止めない)。
        /// </summary>
        private void PrepareGrenades(GameRules rules)
        {
            _grenadeSupplies.Clear();
            if (rules.Actions.Grenade != Grenade.On)
            {
                _grenadesAvailable = false;
                Debug.Log("[Match] grenades=" + rules.Actions.Grenade);
                return;
            }

            _grenadesAvailable = TryValidateGrenadePrefab(_grenadePrefab, out var error);
            if (!_grenadesAvailable)
            {
                Debug.LogError("[Match] grenades are on but " + error + " on " + name + "; nobody gets grenades"
                    + "; run Fps.Editor.NetSceneBuilder.Build", this);
                return;
            }

            Debug.Log("[Match] grenades=On count=" + rules.Actions.GrenadeCount.ToString(CultureInfo.InvariantCulture)
                + " prefab=" + _grenadePrefab.name);
        }

        // 抜けた人の残りは捨てる。同じクライアント Id で入り直したら次の出現で満タンから
        private void ForgetGrenades(int playerId)
        {
            _grenadeSupplies.Remove(playerId);
        }

        // 試合が消えるときに、飛んでいるグレネードも消す (爆発させない)
        private void ClearGrenades()
        {
            _grenadeSupplies.Clear();
            _grenadesAvailable = false;
            if (_liveGrenades.Count == 0)
            {
                return;
            }

            var grenades = _liveGrenades.ToArray();
            _liveGrenades.Clear();
            var canDespawn = IsServer && NetworkManager != null && !NetworkManager.ShutdownInProgress;
            foreach (var grenade in grenades)
            {
                if (canDespawn && grenade != null && grenade.IsSpawned)
                {
                    grenade.NetworkObject.Despawn(true);
                }
            }
        }

        private bool TrySpawnGrenade(NetPlayer thrower, Vector3 position, Vector3 velocity, out NetGrenade grenade)
        {
            GameObject instance = null;
            try
            {
                instance = Instantiate(_grenadePrefab, position, Quaternion.identity);
                instance.name = _grenadePrefab.name;
                grenade = instance.GetComponent<NetGrenade>();
                grenade.ServerLaunch(this, thrower, velocity, GrenadeSpec.Default.FuseSeconds);
                instance.GetComponent<NetworkObject>().Spawn(true);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                Debug.LogError("[Match] could not spawn a grenade for player " + thrower.ServerPlayerId.ToString(CultureInfo.InvariantCulture)
                    + "; the grenade is not used", this);
                if (instance != null)
                {
                    Destroy(instance);
                }

                grenade = null;
                return false;
            }
        }

        private void WarnGrenadeRejected(int playerId, GrenadeThrowRejection reason)
        {
            if (Time.unscaledTime < _nextRejectWarningTime)
            {
                return;
            }

            _nextRejectWarningTime = Time.unscaledTime + _rejectWarningInterval;
            Debug.LogWarning(NetGrenade.RejectedLog(playerId, reason), this);
        }

        private static bool IsExplosionLineObstructed(Vector3 from, Vector3 to)
        {
            var offset = to - from;
            var length = offset.magnitude;
            if (length < 1e-4f)
            {
                return false;
            }

            foreach (var hit in Physics.RaycastAll(from, offset / length, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                // 人は爆風を遮らない (オフラインの ThrownGrenade と同じ)。ほかのグレネードも小さいので遮らない
                if (hit.collider.GetComponentInParent<NetPlayer>() != null || hit.collider.GetComponentInParent<NetGrenade>() != null)
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
