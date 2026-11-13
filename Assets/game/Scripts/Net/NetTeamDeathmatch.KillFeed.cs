using Fps.Core;
using Fps.Runtime;
using Unity.Collections;
using Unity.Netcode;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、キルフィードの配信。サーバーは Session の Killed ごとに
    /// 倒した人・倒された人・武器・頭への止めか・味方撃ちかを <see cref="KilledRpc"/> で全クライアント (ホストを含む) に送り、
    /// 受けた側は <see cref="CombatEvents.RaiseKilled"/> で流す (画面の <see cref="KillFeedView"/> が受ける)。
    /// </summary>
    public sealed partial class NetTeamDeathmatch
    {
        // ServerFire が ApplyDamage を呼んでいる間の、当てた武器と頭に当たったか (サーバーだけ)。射撃の外では null と false
        private string _shotWeaponId;
        private bool _shotHeadshot;

        /// <summary>
        /// Session の Killed (killerId, victimId) をキルフィードの 1 件にする。武器と頭は、その死が今処理中の射撃の撃った人と
        /// 撃たれた人そのものによるときだけ付ける。ダウン中に別の敵へキルが付いたとき・跳ね返った味方撃ち・出血などは、
        /// 撃った弾と倒した人が違うので武器を空にする (別の人の武器を付けないため)。
        /// </summary>
        /// <param name="killerId">Session が倒した人とした Id。</param>
        /// <param name="victimId">倒された人の Id。</param>
        /// <param name="byAlly">倒した人が倒された人の味方 (自滅を除く) か。</param>
        /// <param name="shotAttackerId">処理中のダメージの撃った人。無ければ null。</param>
        /// <param name="shotVictimId">処理中のダメージの撃たれた人。無ければ null。</param>
        /// <param name="shotWeaponId">処理中の射撃の武器の Id。射撃でなければ null。</param>
        /// <param name="shotHeadshot">処理中の射撃が頭に当たったか。</param>
        internal static KillFeedEntry KillFeedEntryFor(
            int killerId, int victimId, bool byAlly, int? shotAttackerId, int? shotVictimId, string shotWeaponId, bool shotHeadshot)
        {
            var byShot = shotWeaponId != null && killerId == shotAttackerId && victimId == shotVictimId;
            return new KillFeedEntry(killerId, victimId, byShot ? shotWeaponId : string.Empty, byShot && shotHeadshot, byAlly);
        }

        private void SendKillFeed(int killerId, int victimId, bool byAlly)
        {
            if (!IsSpawned)
            {
                return;
            }

            var entry = KillFeedEntryFor(killerId, victimId, byAlly, _damageAttackerId, _damageVictimId, _shotWeaponId, _shotHeadshot);

            // 武器の Id はロードアウトの検査で FixedString64Bytes に収まることを確かめてあるが、収まらなければ武器なしで送る
            var weaponId = default(FixedString64Bytes);
            if (System.Text.Encoding.UTF8.GetByteCount(entry.WeaponId) <= NetPlayerWeapon.MaxWeaponIdBytes)
            {
                weaponId = new FixedString64Bytes(entry.WeaponId);
            }

            KilledRpc(entry.KillerId, entry.VictimId, weaponId, entry.Headshot, entry.Friendly);
        }

        [Rpc(SendTo.ClientsAndHost, Delivery = RpcDelivery.Reliable)]
        private void KilledRpc(int killerId, int victimId, FixedString64Bytes weaponId, bool headshot, bool friendly)
        {
            CombatEvents.RaiseKilled(new KillFeedEntry(killerId, victimId, weaponId.ToString(), headshot, friendly));
        }
    }
}
