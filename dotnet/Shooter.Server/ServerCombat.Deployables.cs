using System;
using Shooter.Core;

namespace Shooter.Server
{
    // 置いた物は ServerDeployables が持ち、戦闘は弾と爆発が当たったことを渡すだけにする (乗り物の ServerVehicles と同じ分け方)
    internal sealed partial class ServerCombat
    {
        private ServerDeployables _deployables;

        // 当たり判定はマップごとに作るときに置いた物を受け取るので、戦闘が始まる前に ServerDeployables のコンストラクターがつなぐ
        internal ServerDeployables Deployables
        {
            get => _deployables;
            set
            {
                if (_hitboxes != null)
                {
                    throw new InvalidOperationException("deployables must be attached before the combat module starts");
                }

                _deployables = value;
            }
        }

        // 量はオフラインの Deployable.Absorb と同じく、着弾の効果を通さずに弾の威力へ胴の倍率と距離減衰を掛けて決める。
        // オフラインでも置いた物で止まった弾はヒットマーカーを出さないので、撃った人には知らせない
        internal void DamageDeployable(in ServerShot shot, in ShotHit hit)
        {
            if (_deployables == null)
            {
                return;
            }

            var impact = shot.Impact;
            var amount = DamageCalculator.Calculate(impact.Damage, HitZone.Body, Math.Max(0f, hit.Distance), impact.Falloff, ZoneDamage);
            var attacker = _match.TryGetPlayer(shot.ShooterId, out _) ? shot.ShooterId : (int?)null;
            _deployables.Damage(hit.DeployableId, attacker, amount);
        }
    }
}
