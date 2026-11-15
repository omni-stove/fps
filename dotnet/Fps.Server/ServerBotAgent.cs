using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Core;

namespace Fps.Server
{
    internal sealed class ServerBotAgent
    {
        private readonly NetBotSpec _spec;
        private readonly ISightBlocker _sightBlocker;
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
