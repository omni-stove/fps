using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // オフラインの EnemyShield・EnemySniper・EnemyCharger・EnemyBomber と同じ判断 (Core の *EnemyBehavior) を、bots.json の archetype に合わせて足す。
    // 判断は段階を決めるだけなので、体を動かす・当てる・爆ぜるはサーバーの部品 (移動・戦闘・近接) を使う ServerBots が行う
    internal sealed partial class ServerBotAgent
    {
        private readonly List<string> _phaseLog = new List<string>();
        private ChargerEnemyBehavior _charger;
        private BomberEnemyBehavior _bomber;
        private SniperEnemyBehavior _sniper;
        private ShieldEnemyBehavior _shield;
        private ShieldGuard _guard;
        private MeleeSwing _bash;
        private RiotShieldSpec _plate;
        private float _travelled;

        public EnemyArchetype Archetype => Brain.Spec.Archetype;

        // 物陰と手榴弾は Rifleman の戦い方。ほかの種類は判断がしゃがみと射撃を上書きするので、物陰を探しても使わない
        public bool UsesTactics => Archetype == EnemyArchetype.Rifleman;

        // 突進・自爆・盾の殴りは近づいた相手への攻撃を自分で持つので、ふつうの近接は振らせない
        public bool AllowsMelee => Archetype == EnemyArchetype.Rifleman || Archetype == EnemyArchetype.Sniper;

        public float ShotDamageMultiplier => _sniper != null ? Brain.Spec.Sniper.ShotDamageMultiplier : 1f;

        public bool IsCharging => _charger != null && _charger.IsCharging;

        public Vector3? ChargeDirection => _charger?.ChargeDirection;

        public float ChargeTravelled => _travelled;

        public bool IsFuseBurnt => _bomber != null && _bomber.IsFuseBurnt;

        public BomberPhase BomberPhase => _bomber?.Phase ?? BomberPhase.Approaching;

        // 盾を構えている間だけ板を出す。倒れている間は出さない
        public RiotShieldSpec RaisedShield => _guard != null && !_dead && _guard.IsRaised ? _plate : null;

        internal ChargerEnemyBehavior ChargerBehavior => _charger;

        internal BomberEnemyBehavior BomberBehavior => _bomber;

        internal SniperEnemyBehavior SniperBehavior => _sniper;

        internal ShieldEnemyBehavior ShieldBehavior => _shield;

        public BotArchetypeCue Cue
        {
            get
            {
                if (_dead)
                {
                    return BotArchetypeCue.None;
                }

                if (_charger != null)
                {
                    switch (_charger.Phase)
                    {
                        case ChargerPhase.WindingUp:
                            return BotArchetypeCue.Warning;
                        case ChargerPhase.Charging:
                            return BotArchetypeCue.Attacking;
                        case ChargerPhase.Stunned:
                            return BotArchetypeCue.Exposed;
                    }
                }

                if (_bomber != null && _bomber.Phase == BomberPhase.Fusing)
                {
                    return BotArchetypeCue.Warning;
                }

                if (_sniper != null)
                {
                    switch (_sniper.Phase)
                    {
                        case SniperPhase.Aiming:
                            return BotArchetypeCue.Warning;
                        case SniperPhase.Firing:
                            return BotArchetypeCue.Attacking;
                    }
                }

                return _guard != null && !_guard.IsRaised ? BotArchetypeCue.Exposed : BotArchetypeCue.None;
            }
        }

        // 歩きの速さに掛ける倍率。null なら入力の計画 (BotDriver) のまま動く
        public float? GaitMultiplier
        {
            get
            {
                if (_guard != null)
                {
                    return _guard.IsRaised ? Brain.Spec.Shield.ApproachSpeedMultiplier : (float?)null;
                }

                return _bomber != null && _bomber.Phase != BomberPhase.Exploded ? Brain.Spec.Bomber.RunSpeedMultiplier : (float?)null;
            }
        }

        // 盾を構えている間はオフラインと同じく走らせない
        public bool SprintAllowed => _guard == null;

        // 巻く路は狙撃兵の足場も兼ねる。サーバーのマップには足場の印が無いので、巡る点 (出現地点・目的の場所) から撃たせる
        public void SetRoute(IReadOnlyList<Vector3> route)
        {
            ArgumentNullException.ThrowIfNull(route);
            Brain.SetPatrolRoute(route);
            _sniper?.SetPerches(route);
        }

        // ServerBots がこの tick の後に段階の変化をログへ出せるよう、変わった順に返して空にする
        public IReadOnlyList<string> TakePhaseLog()
        {
            if (_phaseLog.Count == 0)
            {
                return Array.Empty<string>();
            }

            var lines = _phaseLog.ToArray();
            _phaseLog.Clear();
            return lines;
        }

        public bool ConfirmShot(Vector3 feet)
        {
            return _sniper != null && _sniper.ConfirmShot(feet);
        }

        public void AddChargeTravel(float distance)
        {
            _travelled += distance;
        }

        public bool EndCharge(ChargeEnd end)
        {
            return _charger != null && _charger.EndCharge(end);
        }

        public bool CancelCharge()
        {
            return _charger != null && _charger.Cancel();
        }

        public BomberPhase Detonate()
        {
            return _bomber?.Detonate() ?? BomberPhase.Exploded;
        }

        // 倒された時点で大小を決める (オフラインの EnemyBomber.OnDied と同じ)。合図の後なら本来の爆発、前なら小さい爆発。もう爆ぜていれば null
        public ExplosionDamage DeathBlast()
        {
            if (_bomber == null)
            {
                return null;
            }

            var previous = _bomber.Detonate();
            if (previous == BomberPhase.Exploded)
            {
                return null;
            }

            var spec = Brain.Spec.Bomber;
            return previous == BomberPhase.Fusing ? spec.Blast : spec.DeathBlast;
        }

        // 盾を下ろす秒数は受けたダメージの種類で決める (銃弾では下ろさない)。下ろしたら秒数を返す
        public float LowerShield(DamageKind kind)
        {
            if (_guard == null || _dead)
            {
                return 0f;
            }

            var seconds = Brain.Spec.Shield.LowerSecondsFor(kind);
            if (!(seconds > 0f) || !_guard.Lower(seconds, kind.ToString().ToLowerInvariant()))
            {
                return 0f;
            }

            _bash?.Cancel();
            return seconds;
        }

        // 下ろした盾の時間を進め、構え直したら true
        public bool TickGuard(float deltaSeconds)
        {
            return _guard != null && _guard.Tick(deltaSeconds);
        }

        // 盾の殴りを振り始め・進める。当たりの瞬間に true を返し、そのときの振りの値を spec に返す。
        // 判断は目標へ向き直らせるだけなので、扇に入る (facing) までは振り始めない (オフラインの EnemyShield と同じ)
        public bool TickBash(float deltaSeconds, bool facing, out bool started, out MeleeSpec spec)
        {
            started = false;
            spec = null;
            if (_bash == null)
            {
                return false;
            }

            _bash.Spec = Brain.Spec.Shield.Bash;
            if (!_guard.IsRaised)
            {
                _bash.Cancel();
                _bash.Tick(deltaSeconds);
                return false;
            }

            if (_shield.WantsBash && facing && _bash.TryStart())
            {
                started = true;
            }

            spec = _bash.CurrentSpec;
            return _bash.Tick(deltaSeconds);
        }

        // 歩きの速さの multiplier 倍で進む入力にする。人の体は入力の大きさ (1 まで) で速さを下げるので、遅くするときは歩きの入力を縮め、
        // 速くするときは走らせて走りの速さに対する割合へ縮める。走りでも届かない倍率は走りの速さで止まる
        internal static Vector2 GaitMove(Vector2 move, float multiplier, float walkSpeed, float sprintSpeed, out bool sprint)
        {
            if (!(multiplier > 0f) || float.IsInfinity(multiplier))
            {
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "multiplier must be positive and finite");
            }

            sprint = false;
            if (multiplier <= 1f || !(walkSpeed > 0f) || !(sprintSpeed > walkSpeed))
            {
                return move * Math.Min(1f, multiplier);
            }

            sprint = true;
            return move * Math.Min(1f, multiplier * walkSpeed / sprintSpeed);
        }

        private void AddArchetypeBehaviors()
        {
            switch (Archetype)
            {
                case EnemyArchetype.Shield:
                    var shield = Brain.Spec.Shield;
                    _guard = new ShieldGuard();
                    _bash = new MeleeSwing(shield.Bash);
                    _shield = new ShieldEnemyBehavior(_guard, _bash);

                    // 板の大きさと扇は持つ盾 (RiotShieldSpec) の既定にする。遅さは GaitMove が掛けるので、板の倍率は 1 にして二重に掛けない
                    _plate = new RiotShieldSpec(moveSpeedMultiplier: 1f, bash: shield.Bash);
                    Brain.AddBehavior(_shield);
                    break;
                case EnemyArchetype.Sniper:
                    _sniper = new SniperEnemyBehavior();
                    _sniper.PhaseChanged += (previous, next) => _phaseLog.Add(ServerBotLog.Phase(PlayerId, Archetype, previous.ToString(), next.ToString()));
                    Brain.AddBehavior(_sniper);
                    break;
                case EnemyArchetype.Charger:
                    _charger = new ChargerEnemyBehavior();
                    _charger.PhaseChanged += (previous, next) =>
                    {
                        if (next == ChargerPhase.Charging)
                        {
                            _travelled = 0f;
                        }

                        _phaseLog.Add(ServerBotLog.Phase(PlayerId, Archetype, previous.ToString(), next.ToString()));
                    };
                    Brain.AddBehavior(_charger);
                    break;
                case EnemyArchetype.Bomber:
                    _bomber = new BomberEnemyBehavior();
                    _bomber.PhaseChanged += (previous, next) => _phaseLog.Add(ServerBotLog.Phase(PlayerId, Archetype, previous.ToString(), next.ToString()));
                    Brain.AddBehavior(_bomber);
                    break;
            }
        }

        // 前の命の構え・信管・足場・下ろした盾を、出現した所へ持ち越さない
        private void ResetArchetype()
        {
            _charger?.Reset();
            _bomber?.Reset();
            _sniper?.Reset();
            _guard?.Reset();
            _bash?.Reset();
            _travelled = 0f;
            _phaseLog.Clear();
        }
    }
}
