using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// <see cref="NetTeamDeathmatch"/> のうち、サーバーの射撃・ダメージ・キルを <see cref="NetCombatFeed"/> に渡してクライアントへ配る処理。
    /// 宛先は <see cref="NetCombatFeed"/> が決める。ここでは何を誰の名前で送るか (跳ね返した味方撃ち・キルフィードの武器と頭) を決める。
    /// </summary>
    public sealed partial class NetTeamDeathmatch
    {
        // 同じ NetMatch に付いた配り役。無ければ配らない (警告は Spawn 時に 1 度だけ)
        private NetCombatFeed _combatFeed;

        // ApplyDamage の最中の 1 発。Session の Killed (HandleKilled) で、キルフィードの武器と頭を決めるのに使う
        private KillShot? _killShot;

        /// <summary>
        /// サーバーの 1 発のうち、キルフィードに要るもの。撃った人・当たった人・武器・部位。
        /// </summary>
        internal readonly struct KillShot
        {
            public KillShot(int shooterId, int targetId, string weaponId, HitZone zone)
            {
                ShooterId = shooterId;
                TargetId = targetId;
                WeaponId = weaponId ?? string.Empty;
                Zone = zone;
            }

            public int ShooterId { get; }

            public int TargetId { get; }

            public string WeaponId { get; }

            public HitZone Zone { get; }
        }

        /// <summary>
        /// Session の Killed (killerId, victimId) をキルフィードの 1 行にする。
        /// shot (その最中の 1 発) を撃った人が倒した者で、倒れたのが当たった人か撃った本人 (跳ね返し) なら、その武器を載せる。
        /// 頭への止めは、当たった人をその弾で倒したときだけ。それ以外 (出血・全滅で倒れた・ダウン中に最後に当てた敵へキルが付いた) は武器なし。
        /// </summary>
        /// <param name="friendly">倒した側と倒された側が味方どうしか (自滅は false)。</param>
        internal static KillFeedEntry KillFeedEntryFor(int killerId, int victimId, bool friendly, KillShot? shot)
        {
            if (!shot.HasValue || shot.Value.ShooterId != killerId)
            {
                return new KillFeedEntry(killerId, victimId, null, headshot: false, friendly);
            }

            var byShot = victimId == shot.Value.TargetId;
            if (!byShot && victimId != killerId)
            {
                return new KillFeedEntry(killerId, victimId, null, headshot: false, friendly);
            }

            var headshot = byShot && shot.Value.Zone == HitZone.Head;
            return new KillFeedEntry(killerId, victimId, shot.Value.WeaponId, headshot, friendly);
        }

        /// <summary>
        /// 1 発の結果を本人たちに知らせる。体力が変わっていなければ (Ignored) 何も送らない。
        /// 跳ね返した味方撃ちでは体力が減ったのは撃った本人なので、撃った本人に被弾 (攻撃した者の位置なし) だけを送る。
        /// それ以外は撃った本人にヒット、撃たれた本人に被弾 (撃った人の位置から) を送る。
        /// </summary>
        private void SendHitResult(NetPlayer shooter, NetPlayer victim, DamageOutcome outcome, int damage, HitZone zone)
        {
            if (_combatFeed == null || outcome == DamageOutcome.Ignored)
            {
                return;
            }

            if (IsReflected(shooter, victim))
            {
                _combatFeed.ServerDamageTaken(shooter, null, damage);
                return;
            }

            var confirm = new HitConfirm(damage, zone, killed: outcome == DamageOutcome.Killed, downed: outcome == DamageOutcome.Downed);
            _combatFeed.ServerHitConfirm(shooter, confirm);
            _combatFeed.ServerDamageTaken(victim, shooter.transform.position, damage);
        }

        private void SendShot(
            NetPlayer shooter, string weaponId, Vector3 origin, Vector3 direction, RaycastHit? impact, bool impactIsCharacter, HitZone? impactZone)
        {
            if (_combatFeed != null)
            {
                _combatFeed.ServerShot(shooter, weaponId, origin, direction, impact, impactIsCharacter, impactZone);
            }
        }

        private void SendKill(int killerId, int victimId, bool friendly)
        {
            if (_combatFeed != null)
            {
                _combatFeed.ServerKilled(KillFeedEntryFor(killerId, victimId, friendly, _killShot));
            }
        }

        private void ResolveCombatFeed()
        {
            _combatFeed = GetComponent<NetCombatFeed>();
            if (_combatFeed == null && IsServer)
            {
                Debug.LogWarning("[Match] " + name + " has no NetCombatFeed; clients will not get shots, hits or kills"
                    + "; run Fps.Editor.NetSceneBuilder.Build", this);
            }
        }
    }
}
