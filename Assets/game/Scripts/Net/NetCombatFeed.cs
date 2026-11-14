using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// サーバーで起きた射撃・着弾・ヒット・被弾・キルを RPC でクライアントへ配り、受け取ったクライアントで <see cref="CombatEvents"/> に流す。
    /// NetMatch プレハブに <see cref="NetTeamDeathmatch"/> と並べて付け (<c>Fps.Editor.NetSceneBuilder</c>)、サーバーの
    /// <see cref="NetTeamDeathmatch"/> が射撃・ダメージ・キルの処理から Server 系を呼ぶ。
    /// </summary>
    /// <remarks>
    /// 宛先 (<see cref="CollectRecipients"/> と <see cref="CanReceive"/> で選ぶ):
    /// <list type="bullet">
    /// <item>射撃 (<see cref="CombatEvents.ShotFired"/>) と着弾 (<see cref="CombatEvents.Impact"/>): 撃った本人以外の全員。
    /// 撃った本人は自分のクライアントで撃った時に ShotFired を出しているので重ねない。着弾は射撃と同じ RPC に載せ、まとめずに 1 発ごとに送る。</item>
    /// <item><see cref="CombatEvents.HitConfirmed"/>: 撃った本人だけ。</item>
    /// <item><see cref="CombatEvents.DamageTaken"/>: 撃たれた本人 (跳ね返した味方撃ちなら体力の減った撃った本人) だけ。</item>
    /// <item><see cref="CombatEvents.Killed"/>: 全員。</item>
    /// <item>爆発 (グレネード): 全員。受け取ったクライアントで爆発の演出を出し、<c>[Combat] Explosion</c> 行を出す。</item>
    /// </list>
    /// 専用サーバー自身は宛先に入れない (演出も UI も無い)。ホストはクライアントでもあるので、ほかのクライアントと同じく宛先に入る。
    /// Bot (<see cref="NetPlayer.IsBot"/>) の射撃は誰も除かずに送り、Bot が撃った・撃たれたヒットと被弾はだれにも送らない。
    /// ヒット・被弾・キルは受け取ったクライアントで <c>[Combat]</c> 行を出す。射撃と着弾は 1 発ごとにログを埋めるので出さない。
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetCombatFeed : NetworkBehaviour
    {
        public const string LogTag = "[Combat]";

        // 着弾点から法線の向きへ戻ってから、面へ向けて探し直す距離 (m)。サーバーとクライアントで位置がわずかにずれても拾えるだけの幅
        private const float _impactProbeDistance = 0.05f;

        // 毎発 List を作らないよう使い回す (サーバーだけ)
        private readonly List<ulong> _recipients = new List<ulong>();

        /// <summary>
        /// connectedClientIds のうち、excludedClientId を除いて <see cref="CanReceive"/> が通るものを into に入れ直す。
        /// 同じ Id が重なっていても 1 度だけ入れる。
        /// </summary>
        /// <param name="connectedClientIds">接続中のクライアント (<see cref="NetworkManager.ConnectedClientsIds"/>)。</param>
        /// <param name="serverClientId">サーバーのクライアント Id (<see cref="NetworkManager.ServerClientId"/>)。</param>
        /// <param name="serverIsClient">サーバーがクライアントも兼ねているか (ホスト)。false なら serverClientId を入れない。</param>
        /// <param name="excludedClientId">除くクライアント (撃った本人)。null なら誰も除かない。</param>
        /// <param name="into">結果。前の中身は消す。</param>
        internal static void CollectRecipients(
            IEnumerable<ulong> connectedClientIds, ulong serverClientId, bool serverIsClient, ulong? excludedClientId, List<ulong> into)
        {
            if (connectedClientIds == null)
            {
                throw new ArgumentNullException(nameof(connectedClientIds));
            }

            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();
            foreach (var clientId in connectedClientIds)
            {
                if (clientId == excludedClientId || into.Contains(clientId) || !IsPresentationClient(clientId, serverClientId, serverIsClient))
                {
                    continue;
                }

                into.Add(clientId);
            }
        }

        /// <summary>
        /// clientId に本人宛ての知らせ (ヒット・被弾) を送れるか。接続中で、専用サーバー自身でなければ true。
        /// </summary>
        internal static bool CanReceive(ulong clientId, IEnumerable<ulong> connectedClientIds, ulong serverClientId, bool serverIsClient)
        {
            if (connectedClientIds == null)
            {
                throw new ArgumentNullException(nameof(connectedClientIds));
            }

            if (!IsPresentationClient(clientId, serverClientId, serverIsClient))
            {
                return false;
            }

            foreach (var connected in connectedClientIds)
            {
                if (connected == clientId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPresentationClient(ulong clientId, ulong serverClientId, bool serverIsClient)
        {
            return serverIsClient || clientId != serverClientId;
        }

        /// <example><c>[Combat] HitConfirmed damage=25 zone=Head killed=False downed=False</c></example>
        internal static string HitConfirmedLog(HitConfirm confirm)
        {
            return LogTag + " HitConfirmed damage=" + confirm.Damage.ToString(CultureInfo.InvariantCulture)
                + " zone=" + confirm.Zone + " killed=" + confirm.Killed + " downed=" + confirm.Downed;
        }

        /// <example><c>[Combat] DamageTaken amount=25 source=(1.00, 0.00, -22.00)</c>。攻撃した者の位置が分からなければ <c>source=none</c>。</example>
        internal static string DamageTakenLog(Vector3? sourcePosition, int amount)
        {
            return LogTag + " DamageTaken amount=" + amount.ToString(CultureInfo.InvariantCulture)
                + " source=" + (sourcePosition.HasValue ? sourcePosition.Value.ToString("F2", CultureInfo.InvariantCulture) : "none");
        }

        /// <example><c>[Combat] Killed killer=1 victim=2 weapon=rifle headshot=False friendly=False</c></example>
        internal static string KilledLog(KillFeedEntry entry)
        {
            return LogTag + " Killed " + entry;
        }

        /// <summary>
        /// サーバーで、shooter の 1 発 (検査を通って弾を消費したもの) を撃った本人以外の全員に送る。
        /// 何かに当たっていれば impact にその点と法線を渡し、同じ RPC で着弾も送る。
        /// </summary>
        internal void ServerShot(NetPlayer shooter, string weaponId, Vector3 origin, Vector3 direction, RaycastHit? impact)
        {
            if (!CanSendFromServer() || shooter == null)
            {
                return;
            }

            // Bot は自分で射撃の演出を出さないので、持ち主 (サーバー。ホストなら画面もある) を除かない
            var excluded = shooter.IsBot ? (ulong?)null : shooter.OwnerClientId;
            CollectRecipients(NetworkManager.ConnectedClientsIds, NetworkManager.ServerClientId, IsHost, excluded, _recipients);
            if (_recipients.Count == 0)
            {
                return;
            }

            var hasImpact = impact.HasValue;
            var point = hasImpact ? impact.Value.point : Vector3.zero;
            var normal = hasImpact ? impact.Value.normal : Vector3.zero;
            ShotRpc(
                new NetworkObjectReference(shooter.NetworkObject), ToWireWeaponId(weaponId), origin, direction, hasImpact, point, normal,
                RpcTarget.Group(_recipients, RpcTargetUse.Temp));
        }

        /// <summary>サーバーで、shooter が生きている相手に当てたことを shooter の持ち主にだけ送る。</summary>
        internal void ServerHitConfirm(NetPlayer shooter, HitConfirm confirm)
        {
            // Bot の持ち主はサーバーで、ホストならホストの人にヒットの表示が出てしまう
            if (!CanSendFromServer() || shooter == null || shooter.IsBot)
            {
                return;
            }

            var clientId = shooter.OwnerClientId;
            if (!CanReceive(clientId, NetworkManager.ConnectedClientsIds, NetworkManager.ServerClientId, IsHost))
            {
                return;
            }

            HitConfirmRpc(confirm.Damage, (byte)confirm.Zone, confirm.Killed, confirm.Downed, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        /// <summary>
        /// サーバーで、victim が生きている間に amount のダメージを受けたことを victim の持ち主にだけ送る。
        /// sourcePosition は攻撃した者の位置。分からなければ (自分の弾が跳ね返ったときなど) null。
        /// </summary>
        internal void ServerDamageTaken(NetPlayer victim, Vector3? sourcePosition, int amount)
        {
            if (!CanSendFromServer() || victim == null || victim.IsBot)
            {
                return;
            }

            var clientId = victim.OwnerClientId;
            if (!CanReceive(clientId, NetworkManager.ConnectedClientsIds, NetworkManager.ServerClientId, IsHost))
            {
                return;
            }

            DamageTakenRpc(
                sourcePosition.HasValue, sourcePosition ?? Vector3.zero, amount, RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        /// <summary>
        /// サーバーで、center で起きた爆発 (グレネードなど) を全員に送る。受け取ったクライアントは爆発の演出 (<see cref="ThrownGrenade.PlayExplosionEffects"/>) を出す。
        /// thrower は投げた人。抜けていれば null。
        /// </summary>
        internal void ServerExplosion(Vector3 center, NetPlayer thrower, string weaponId)
        {
            if (!CanSendFromServer())
            {
                return;
            }

            var hasThrower = thrower != null && thrower.IsSpawned;
            var throwerReference = hasThrower ? new NetworkObjectReference(thrower.NetworkObject) : default;
            ExplosionRpc(center, hasThrower, throwerReference, ToWireWeaponId(weaponId));
        }

        /// <example><c>[Combat] Explosion weapon=grenade at=(1.00, -0.92, 9.50) thrower=1</c>。投げた人が分からなければ <c>thrower=none</c>。</example>
        internal static string ExplosionLog(string weaponId, Vector3 center, ulong? throwerClientId)
        {
            return LogTag + " Explosion weapon=" + (weaponId ?? string.Empty)
                + " at=" + center.ToString("F2", CultureInfo.InvariantCulture)
                + " thrower=" + (throwerClientId.HasValue ? throwerClientId.Value.ToString(CultureInfo.InvariantCulture) : "none");
        }

        /// <summary>サーバーで、キルフィードの 1 行を全員に送る。</summary>
        internal void ServerKilled(KillFeedEntry entry)
        {
            if (!CanSendFromServer())
            {
                return;
            }

            KilledRpc(entry.KillerId, entry.VictimId, ToWireWeaponId(entry.WeaponId), entry.Headshot, entry.Friendly);
        }

        private bool CanSendFromServer()
        {
            return IsSpawned && IsServer;
        }

        // 武器の Id はロードアウトの検査 (NetPlayerWeapon.ServerEquip) で長さを確かめてあるが、
        // 収まらない Id で FixedString が例外を投げて試合の処理が止まらないよう、ここでも空文字に寄せる
        private FixedString64Bytes ToWireWeaponId(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
            {
                return default;
            }

            if (!NetPlayerWeapon.FitsWeaponId(weaponId))
            {
                Debug.LogWarning(LogTag + " weapon id '" + weaponId + "' is too long to send; sending it as empty", this);
                return default;
            }

            return new FixedString64Bytes(weaponId);
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Reliable)]
        private void ShotRpc(
            NetworkObjectReference shooter,
            FixedString64Bytes weaponId,
            Vector3 origin,
            Vector3 direction,
            bool hasImpact,
            Vector3 impactPoint,
            Vector3 impactNormal,
            RpcParams rpcParams)
        {
            if (!IsFromServer(rpcParams))
            {
                return;
            }

            // 撃った人がもう消えていても、射撃の演出は出す (撃った者が分からない 1 発として)
            var shooterObject = shooter.TryGet(out var networkObject) ? networkObject.gameObject : null;
            CombatEvents.RaiseShotFired(shooterObject, origin, direction, weaponId.ToString());

            if (hasImpact)
            {
                CombatEvents.RaiseImpact(impactPoint, impactNormal, FindImpactCollider(impactPoint, impactNormal));
            }
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Reliable)]
        private void HitConfirmRpc(int damage, byte zone, bool killed, bool downed, RpcParams rpcParams)
        {
            if (!IsFromServer(rpcParams))
            {
                return;
            }

            HitConfirm confirm;
            try
            {
                confirm = new HitConfirm(damage, (HitZone)zone, killed, downed);
            }
            catch (ArgumentException e)
            {
                Debug.LogWarning(LogTag + " ignored invalid hit confirm from the server: " + e.Message, this);
                return;
            }

            Debug.Log(HitConfirmedLog(confirm), this);
            CombatEvents.RaiseHitConfirmed(confirm);
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Reliable)]
        private void DamageTakenRpc(bool hasSource, Vector3 sourcePosition, int amount, RpcParams rpcParams)
        {
            if (!IsFromServer(rpcParams))
            {
                return;
            }

            if (amount < 0)
            {
                Debug.LogWarning(LogTag + " ignored negative damage " + amount + " from the server", this);
                return;
            }

            var source = hasSource ? sourcePosition : (Vector3?)null;
            Debug.Log(DamageTakenLog(source, amount), this);
            CombatEvents.RaiseDamageTaken(source, amount);
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Reliable)]
        private void KilledRpc(int killerId, int victimId, FixedString64Bytes weaponId, bool headshot, bool friendly, RpcParams rpcParams = default)
        {
            if (!IsFromServer(rpcParams))
            {
                return;
            }

            var entry = new KillFeedEntry(killerId, victimId, weaponId.ToString(), headshot, friendly);
            Debug.Log(KilledLog(entry), this);
            CombatEvents.RaiseKilled(entry);
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Reliable)]
        private void ExplosionRpc(
            Vector3 center, bool hasThrower, NetworkObjectReference thrower, FixedString64Bytes weaponId, RpcParams rpcParams = default)
        {
            if (!IsFromServer(rpcParams))
            {
                return;
            }

            if (!NetMath.IsFinite(center))
            {
                Debug.LogWarning(LogTag + " ignored an explosion at a non-finite point from the server", this);
                return;
            }

            // 投げた人がもう消えていても、爆発の演出は出す
            ulong? throwerClientId = null;
            if (hasThrower && thrower.TryGet(out var throwerObject))
            {
                throwerClientId = throwerObject.OwnerClientId;
            }

            Debug.Log(ExplosionLog(weaponId.ToString(), center, throwerClientId), this);
            ThrownGrenade.PlayExplosionEffects(center);
        }

        private bool IsFromServer(RpcParams rpcParams)
        {
            if (rpcParams.Receive.SenderClientId == NetworkManager.ServerClientId)
            {
                return true;
            }

            Debug.LogWarning(LogTag + " ignored combat event from client " + rpcParams.Receive.SenderClientId, this);
            return false;
        }

        /// <summary>
        /// 着弾点のコライダーをこのクライアントの物理で探し直す (コライダーは RPC で送れない)。
        /// 他のプレイヤーは補間した位置にいてサーバーの判定位置とずれるので、見つからないこともある。そのときは null。
        /// </summary>
        private static Collider FindImpactCollider(Vector3 point, Vector3 normal)
        {
            if (!(normal.sqrMagnitude > 0f))
            {
                return null;
            }

            var unitNormal = normal.normalized;
            var start = point + (unitNormal * _impactProbeDistance);
            return Physics.Raycast(
                start, -unitNormal, out var hit, _impactProbeDistance * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.collider
                : null;
        }
    }
}
