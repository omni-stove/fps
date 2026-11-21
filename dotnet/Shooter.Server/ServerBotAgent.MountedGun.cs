using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // オフラインの EnemyMountedGunner と同じ判断 (MountedGunEnemyBehavior) で、bots.json の mountedGun を書いた Bot を固定銃座へ乗りに行かせる。
    // 乗る・降りる・撃つは乗り物と戦闘のモジュールを使う ServerBots が行い、ここは行き先・銃の向き・降りる時を決める
    internal sealed partial class ServerBotAgent
    {
        // 降りる位置が塞がって降りられなかったとき、次に降りようとするまでの秒数。tick ごとに断られたログを出さないため
        public const float LeaveRetrySeconds = 1f;

        private MountedGunEnemyBehavior _gunner;
        private VehicleWeaponAim _gunAim;
        private bool _wasSeekingGun;
        private float _nextGunLeave;

        // 乗りに行く候補の乗り物の番号。無ければ -1
        public int GunCandidate { get; private set; } = -1;

        // 乗っている固定銃座の乗り物の番号。乗っていなければ -1
        public int MannedGun { get; private set; } = -1;

        public bool MansGuns => _gunner != null;

        public bool IsSeekingGun => _gunner != null && _gunner.IsSeeking && GunCandidate >= 0;

        public bool CanTryLeavingGun => Clock >= _nextGunLeave;

        // 銃座が今向いている向き (乗り物から見た向き)。乗っていなければ前
        public Vector3 GunLocalDirection => _gunAim?.LocalDirection ?? Vector3.UnitZ;

        internal MountedGunEnemyBehavior GunnerBehavior => _gunner;

        public void SetGunCandidate(int vehicle, Vector3? boardPoint)
        {
            GunCandidate = boardPoint.HasValue ? vehicle : -1;
            _gunner?.SetCandidate(boardPoint);
        }

        // 判断は Brain.Tick の中で乗りに行くと決めるので、乗りに行き始めたことはその後に知る。乗りに行き始めた tick だけ true
        public bool NoteSeekingGun()
        {
            var seeking = IsSeekingGun;
            var started = seeking && !_wasSeekingGun;
            _wasSeekingGun = seeking;
            return started;
        }

        public void StartManning(int vehicle, VehicleWeaponSpec weapon)
        {
            ArgumentNullException.ThrowIfNull(weapon);
            MannedGun = vehicle;
            _gunAim = new VehicleWeaponAim(weapon);
            _wasSeekingGun = false;
            _nextGunLeave = Clock;
            SetGunCandidate(-1, null);
        }

        // 乗っている間は判断 (Brain) を進めない (オフラインの EnemyAgent が座席に留めるのと同じ)。
        // localTarget は乗り物から見た目標の向きで、銃座が届かなければ null。銃座はその向きへ回る速さで回り、許しの内に向いたら撃つ
        public MountedGunOrder Man(float deltaSeconds, bool hasTarget, bool visible, Vector3? localTarget)
        {
            if (_gunner == null || _gunAim == null)
            {
                throw new InvalidOperationException("bot " + PlayerId + " does not man a gun");
            }

            var canAim = localTarget.HasValue;
            var pointing = false;
            if (canAim)
            {
                _gunAim.Step(localTarget.Value, deltaSeconds);
                pointing = _gunAim.OffsetDegrees(localTarget.Value) <= _spec.Input.FireToleranceDegrees;
            }

            // 目標がいないのは射角の外ではないので、降りる理由が OutOfArc にならないよう届くものとして渡す (オフラインと同じ)
            return _gunner.Man(deltaSeconds, hasTarget && visible, !hasTarget || canAim, pointing);
        }

        public void DelayGunLeave()
        {
            _nextGunLeave = Clock + LeaveRetrySeconds;
        }

        // 自分で降りたのでなくても (倒れた・壊れた・置き直された) すぐ同じ銃座へ戻らせない
        public void NoteLeftGun()
        {
            MannedGun = -1;
            _gunAim = null;
            _gunner?.NoteLeft();
        }

        // 固定銃座で撃つのは Rifleman だけ。ほかの種類は判断が行き先と射撃を上書きし、銃座へ向かう判断と取り合うため
        private void AddMountedGunBehavior()
        {
            if (_spec.MountedGun == null || Archetype != EnemyArchetype.Rifleman)
            {
                return;
            }

            _gunner = new MountedGunEnemyBehavior(_spec.MountedGun);
            Brain.AddBehavior(_gunner);
        }

        // 前の命の行き先・待ち時間・乗っていた銃座を持ち越さない
        private void ResetMountedGun()
        {
            _gunner?.Reset();
            _gunAim = null;
            _wasSeekingGun = false;
            _nextGunLeave = 0f;
            MannedGun = -1;
            GunCandidate = -1;
        }
    }
}
