using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed class ServerBotAgent
    {
        private const float _minHorizontalLengthSquared = 1e-6f;

        private readonly NetBotSpec _spec;
        private readonly ISightBlocker _sightBlocker;
        private readonly Random _random;
        private Vector3 _progressPosition;
        private float _progressTime;
        private bool _tracking;
        private FireTrigger _trigger;
        private WeaponSpec _triggerSpec;
        private bool _dead = true;

        public ServerBotAgent(int playerId, NetBotSpec spec, ServerBotNavigation navigation, IReadOnlyList<Vector3> route, Random random,
            ISightBlocker sightBlocker)
        {
            PlayerId = playerId;
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _sightBlocker = sightBlocker ?? throw new ArgumentNullException(nameof(sightBlocker));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            Brain = new EnemyBrain(spec.Brain);
            Brain.SetPatrolRoute(route);
            Driver = new BotDriver(spec.Input, random);
            Route = new ServerBotRoute(navigation, spec);
        }

        public int PlayerId { get; }

        public EnemyBrain Brain { get; }

        public BotDriver Driver { get; }

        public ServerBotRoute Route { get; }

        public List<Vector3> Corners { get; } = new List<Vector3>();

        // 移動のモジュールは上下の向きを持たないのでここで持つ
        public float Pitch { get; set; }

        // int.MaxValue は「まだ無い」印で、出現直後の最初の tick を被弾に数えない
        public int LastHealth { get; set; } = int.MaxValue;

        public float Clock { get; set; }

        public long SpawnTick { get; set; } = -1;

        public float NextPositionLog { get; set; }

        public bool WasClimbing { get; set; }

        public void MarkDead()
        {
            if (_dead)
            {
                return;
            }

            _dead = true;
            Brain.Kill();
        }

        public void Restart(bool respawned)
        {
            if (!_dead && !respawned)
            {
                return;
            }

            _dead = false;
            if (Brain.IsDead)
            {
                Brain.Revive();
            }

            Driver.ResetAim();
            Pitch = 0f;
            LastHealth = int.MaxValue;
            Route.Clear();
            _tracking = false;
            WasClimbing = false;
        }

        // はしごの上では上下に進むので、水平ではなく 3 次元の距離で測る
        public bool IsStuck(Vector3 position, bool moving)
        {
            if (!moving || !_tracking)
            {
                _tracking = moving;
                _progressPosition = position;
                _progressTime = Clock;
                return false;
            }

            if (Clock - _progressTime < _spec.StuckSeconds)
            {
                return false;
            }

            var moved = Vector3.Distance(position, _progressPosition);
            _progressPosition = position;
            _progressTime = Clock;
            return moved < _spec.StuckProgress;
        }

        /// <summary>目 eye から target への視線を、世界の当たりのほかに遮る物 (煙など) があれば true。</summary>
        public bool IsSightBlocked(Vector3 eye, Vector3 target)
        {
            return _sightBlocker.Blocks(eye, target);
        }

        /// <summary>
        /// 撃つ向き direction と、目 eye から lookAt への向きの水平の差が <see cref="EnemySpec.FireToleranceDegrees"/> 以下なら true
        /// (オフラインの EnemyAgent.IsFacing と同じ)。lookAt が無いか真上・真下なら true。
        /// </summary>
        public bool IsFacing(Vector3 eye, Vector3 direction, Vector3? lookAt)
        {
            var tolerance = Brain.Spec.FireToleranceDegrees;
            if (!lookAt.HasValue || tolerance >= EnemySpec.MaxFireToleranceDegrees)
            {
                return true;
            }

            var toTarget = new Vector2(lookAt.Value.X - eye.X, lookAt.Value.Z - eye.Z);
            var forward = new Vector2(direction.X, direction.Z);
            if (toTarget.LengthSquared() < _minHorizontalLengthSquared || forward.LengthSquared() < _minHorizontalLengthSquared)
            {
                return true;
            }

            var cos = Vector2.Dot(Vector2.Normalize(toTarget), Vector2.Normalize(forward));
            var degrees = Math.Acos(Math.Max(-1f, Math.Min(1f, cos))) * ServerBotMath.DegreesPerRadian;
            return degrees <= tolerance;
        }

        /// <summary>
        /// direction に、Attack に入ってからの発数の散らばり (<see cref="EnemyBrain.NextShotSpreadDegrees"/>) と
        /// lookAt までの距離の散らばり (<see cref="EnemySpec.RangeSpreadDegreesAt"/>) を足した向き (長さ 1)。散らばりが 0 なら乱数を引かない。
        /// </summary>
        public Vector3 Scatter(Vector3 eye, Vector3 direction, Vector3? lookAt)
        {
            var range = lookAt.HasValue ? Brain.Spec.RangeSpreadDegreesAt(Vector3.Distance(eye, lookAt.Value)) : 0f;
            var halfAngle = Math.Min(EnemySpec.MaxSpreadDegrees, Brain.NextShotSpreadDegrees + range);
            return BlindFireShot.Spread(direction, halfAngle, _random);
        }

        public FireTrigger TriggerFor(WeaponSpec spec)
        {
            if (_trigger == null || !ReferenceEquals(spec, _triggerSpec))
            {
                _trigger = new FireTrigger(spec);
                _triggerSpec = spec;
            }

            return _trigger;
        }
    }
}
