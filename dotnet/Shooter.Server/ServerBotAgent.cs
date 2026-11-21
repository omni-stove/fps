using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed partial class ServerBotAgent
    {
        private readonly NetBotSpec _spec;
        private readonly ISightBlocker _sightBlocker;
        private readonly Random _random;
        private bool _dead = true;

        public ServerBotAgent(int playerId, NetBotSpec spec, ServerBotNavigation navigation, IReadOnlyList<Vector3> route, Random random,
            ISightBlocker sightBlocker)
        {
            PlayerId = playerId;
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _sightBlocker = sightBlocker ?? throw new ArgumentNullException(nameof(sightBlocker));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            Brain = new EnemyBrain(spec.Brain);
            AddArchetypeBehaviors();
            AddMountedGunBehavior();
            SetRoute(route);
            Driver = new BotDriver(spec.Input, random);
            Route = new ServerBotRoute(navigation, spec);
            Decoys = new ServerDecoyPerception(playerId);
        }

        public int PlayerId { get; }

        public ServerDecoyPerception Decoys { get; }

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

        // 目的が示した行き先へ巡る路を向けている間はその先。null なら BotRally か出現地点を巡る
        public Vector3? Goal { get; set; }

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

            // 前の命で探した物陰と投げた間隔は、出現した所では意味が無い
            Brain.UseCover(null);
            _nextCoverSearch = Clock;
            _nextGrenadeAt = Clock;
            ResetArchetype();
            ResetMountedGun();
        }
    }
}
