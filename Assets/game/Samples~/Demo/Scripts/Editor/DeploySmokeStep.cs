using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Editor
{
    // 射撃場のルールは Abilities を Off のまま配る (ほかの手順の前提を変えない) ので、この手順の間だけ On にする。
    // 押し続けた入力は smoke から渡せないので、使ったら次の Tick で離したことになり、その場の見本の判定で置くか断る。
    // 撃ち合いは Hitscan.Fire で撃つ。武器の向きを合わせるより確実で、弾が置いた物に届く道筋 (Raycast → Deployable) は同じ。
    public sealed class DeploySmokeStep : ITpsActionSmokeStep
    {
        public const int SlotIndex = 1;
        public const int MaxEnemyShots = 300;
        public const float ShotRange = 15f;

        private enum Phase
        {
            Setup,
            Steep,
            First,
            Friendly,
            Enemy,
            Limit,
            Done,
        }

        private enum UseState
        {
            Teleport,
            Settle,
            Running,
        }

        private readonly List<string> _lines = new List<string>();
        private readonly List<int> _limitSerials = new List<int>();
        private Phase _phase;
        private UseState _use;
        private int _mark;
        private int _shots;
        private int _playerBulletHits;
        private int _targetSerial;
        private BootstrapWorld _world;
        private DeployAbilityDefinition _definition;
        private DeploySpec _spec;
        private DeployOwner _owner;
        private Deployable _target;
        private Transform _shooter;

        public int Order => DeployRangeFixture.FixtureOrder;

        public string Name => "deploy";

        public float TimeoutSeconds => 45f;

        public void Reset()
        {
            _lines.Clear();
            _limitSerials.Clear();
            _phase = Phase.Setup;
            _use = UseState.Teleport;
            _mark = 0;
            _shots = 0;
            _playerBulletHits = 0;
            _targetSerial = 0;
            _world = null;
            _definition = null;
            _spec = null;
            _owner = null;
            _target = null;
            _shooter = null;
        }

        public void OnLog(string condition)
        {
            if (condition != null && condition.StartsWith(DeployLog.Tag, System.StringComparison.Ordinal))
            {
                _lines.Add(condition);
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            _world = world;
            var host = world.Abilities;
            if (host == null)
            {
                problem = "the player has no AbilityHost";
                return false;
            }

            switch (_phase)
            {
                case Phase.Setup:
                    problem = Setup(world, host);
                    return false;
                case Phase.Steep:
                    problem = UseAt(world, host, DeployRangeFixture.RampLaneX, out var steepDone) ?? (steepDone ? CheckSteep() : null);
                    return false;
                case Phase.First:
                    problem = UseAt(world, host, DeployRangeFixture.FlatLaneXs[0], out var firstDone) ?? (firstDone ? CheckFirst() : null);
                    return false;
                case Phase.Friendly:
                    problem = ShootThroughOwnShield(world);
                    return false;
                case Phase.Enemy:
                    problem = ShootUntilDestroyed(world);
                    return false;
                case Phase.Limit:
                    var lane = DeployRangeFixture.FlatLaneXs[_limitSerials.Count];
                    problem = UseAt(world, host, lane, out var limitDone) ?? (limitDone ? CheckLimit() : null);
                    return false;
                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            if (_world == null)
            {
                return;
            }

            if (_world.Abilities != null)
            {
                _world.Abilities.Configure(_world.Rules);
            }

            if (_world.PlayerDamageable != null)
            {
                _world.PlayerDamageable.Damaged -= OnPlayerDamaged;
            }

            // 残すと、この後の手順で弾や移動を止める。
            if (_owner != null)
            {
                _owner.Clear(DeployRemoval.Discarded);
            }
        }

        private string Setup(BootstrapWorld world, AbilityHost host)
        {
            _definition = host.Definition(SlotIndex) as DeployAbilityDefinition;
            if (_definition == null)
            {
                var held = host.Definition(SlotIndex);
                return $"Ability{SlotIndex + 1} holds '{(held != null ? held.Id : AbilityHost.EmptySlotName)}', not a deploy ability; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            var yard = GameObject.Find(DeployRangeFixture.RootName);
            _shooter = yard != null ? yard.transform.Find(DeployRangeFixture.ShooterName) : null;
            if (_shooter == null)
            {
                return $"the scene has no '{DeployRangeFixture.RootName}/{DeployRangeFixture.ShooterName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            _spec = _definition.ToDeploySpec();
            if (_spec.Overflow != DeployOverflow.ReplaceOldest || _spec.MaxActive >= DeployRangeFixture.FlatLaneXs.Count)
            {
                return $"the deploy ability '{_definition.Id}' must replace the oldest and allow fewer than {DeployRangeFixture.FlatLaneXs.Count} at once so the yard can go over the limit; got overflow={_spec.Overflow} max={_spec.MaxActive}";
            }

            if (world.Weapon == null || world.Weapon.Weapon == null)
            {
                return "the player has no weapon to take the damage values from";
            }

            _owner = DeployOwner.GetOrAdd(world.Player);
            _owner.Clear(DeployRemoval.Discarded);
            host.Configure(world.Rules.WithActions(world.Rules.Actions.WithAbilities(Abilities.On)));
            world.PlayerDamageable.Damaged += OnPlayerDamaged;
            _phase = Phase.Steep;
            return null;
        }

        // 移した直後は接地が前の位置のままなので、接地を待ってから使う。毎回枠を満たし、回数や待ち時間で断られないようにする。
        private string UseAt(BootstrapWorld world, AbilityHost host, float laneX, out bool done)
        {
            done = false;
            switch (_use)
            {
                case UseState.Teleport:
                    Teleport(world.Controller, DeployRangeFixture.PlayerGround(laneX) + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f));
                    FaceForward(world);
                    _use = UseState.Settle;
                    return null;
                case UseState.Settle:
                    if (!world.Mover.IsGrounded)
                    {
                        return null;
                    }

                    host.ResetSlots();
                    _mark = _lines.Count;
                    if (!host.TryUse(SlotIndex))
                    {
                        return $"the player could not use the deploy ability at lane x={Format(laneX)} (block {host.UseBlock(SlotIndex)})";
                    }

                    _use = UseState.Running;
                    return null;
                default:
                    if (host.IsRunning(SlotIndex))
                    {
                        return null;
                    }

                    _use = UseState.Teleport;
                    done = true;
                    return null;
            }
        }

        private string CheckSteep()
        {
            var kind = _definition.Id;
            var preview = DeployLog.Preview(_owner.OwnerId, kind, DeployVerdict.TooSteep);
            if (!SawSince(_mark, preview))
            {
                return $"aiming at the steep ramp did not show '{preview}' (lines: {string.Join(" | ", Since(_mark))})";
            }

            var max = _world.Abilities.Slot(SlotIndex).Spec.MaxCharges;
            var refused = DeployLog.Refused(_owner.OwnerId, kind, DeployVerdict.TooSteep, max, max);
            if (!SawSince(_mark, refused))
            {
                return $"releasing on the steep ramp did not log '{refused}' (lines: {string.Join(" | ", Since(_mark))})";
            }

            if (_owner.ActiveCount(kind) != 0)
            {
                return $"something was placed on the steep ramp ({_owner.ActiveCount(kind)} active)";
            }

            _phase = Phase.First;
            return null;
        }

        private string CheckFirst()
        {
            var problem = RequirePlaced(out var placed);
            if (problem != null)
            {
                return problem;
            }

            _target = placed;
            _targetSerial = placed.Serial;
            _phase = Phase.Friendly;
            return null;
        }

        private string ShootThroughOwnShield(BootstrapWorld world)
        {
            if (_target == null || _target.IsRemoved)
            {
                return "the placed shield disappeared before it was shot";
            }

            var spec = world.Weapon.Weapon.Spec;
            var health = _target.Damageable.Health.Current;
            var origin = world.Player.transform.position;
            _mark = _lines.Count;
            var result = Hitscan.Fire(origin, _target.Damageable.AimPoint - origin, ShotRange, spec.Damage, spec.Falloff, world.Player.transform, 0f, spec.Id);
            if (result.Hit && result.RaycastHit.collider != null && result.RaycastHit.collider.transform.IsChildOf(_target.transform))
            {
                return "the owner's bullet hit the owner's own shield";
            }

            if (_target.Damageable.Health.Current != health || SawPrefixSince(_mark, BlockedPrefix(_targetSerial)))
            {
                return $"the owner's bullet was stopped by the owner's own shield (lines: {string.Join(" | ", Since(_mark))})";
            }

            _mark = _lines.Count;
            _playerBulletHits = 0;
            _phase = Phase.Enemy;
            return null;
        }

        private string ShootUntilDestroyed(BootstrapWorld world)
        {
            if (_target == null || _target.IsRemoved)
            {
                var removed = DeployLog.Removed(_owner.OwnerId, _definition.Id, _targetSerial, DeployRemoval.Destroyed);
                if (!SawSince(_mark, removed))
                {
                    return $"the shield disappeared without logging '{removed}' (lines: {string.Join(" | ", Since(_mark))})";
                }

                if (!SawPrefixSince(_mark, BlockedPrefix(_targetSerial)))
                {
                    return "the shield broke without logging a blocked bullet";
                }

                if (_playerBulletHits > 0)
                {
                    return $"{_playerBulletHits} bullets went through the shield and hit the player behind it";
                }

                _phase = Phase.Limit;
                return null;
            }

            if (_shots >= MaxEnemyShots)
            {
                return $"the shield survived {MaxEnemyShots} shots (hp={_target.Damageable.Health.Current})";
            }

            var spec = world.Weapon.Weapon.Spec;
            var origin = _shooter.position;
            Hitscan.Fire(origin, _target.Damageable.AimPoint - origin, ShotRange, spec.Damage, spec.Falloff, _shooter, 0f, spec.Id);
            _shots++;
            return null;
        }

        private string CheckLimit()
        {
            var problem = RequirePlaced(out var placed);
            if (problem != null)
            {
                return problem;
            }

            _limitSerials.Add(placed.Serial);
            if (_limitSerials.Count <= _spec.MaxActive)
            {
                return null;
            }

            var kind = _definition.Id;
            var removed = DeployLog.Removed(_owner.OwnerId, kind, _limitSerials[0], DeployRemoval.Replaced);
            if (!SawSince(_mark, removed))
            {
                return $"placing one over the limit of {_spec.MaxActive} did not log '{removed}' (lines: {string.Join(" | ", Since(_mark))})";
            }

            var active = _owner.Active(kind);
            if (active.Count != _spec.MaxActive)
            {
                return $"{active.Count} shields are active after going over the limit (expected {_spec.MaxActive})";
            }

            for (var i = 0; i < active.Count; i++)
            {
                var expected = _limitSerials[i + 1];
                if (active[i] == null || active[i].Serial != expected)
                {
                    return $"the active shield {i} is serial {(active[i] != null ? active[i].Serial : 0)} (expected {expected}, the newest ones)";
                }
            }

            _phase = Phase.Done;
            return null;
        }

        private string RequirePlaced(out Deployable placed)
        {
            placed = null;
            var kind = _definition.Id;
            var preview = DeployLog.Preview(_owner.OwnerId, kind, DeployVerdict.Ok);
            if (!SawSince(_mark, preview))
            {
                return $"aiming at flat ground did not show '{preview}' (lines: {string.Join(" | ", Since(_mark))})";
            }

            var prefix = DeployLog.Prefix(_owner.OwnerId, kind) + " placed serial=";
            if (!SawPrefixSince(_mark, prefix))
            {
                return $"releasing on flat ground did not place anything (lines: {string.Join(" | ", Since(_mark))})";
            }

            var active = _owner.Active(kind);
            placed = active.Count > 0 ? active[active.Count - 1] : null;
            if (placed == null || placed.IsRemoved || placed.Damageable == null)
            {
                return "the placed shield is not in the owner's list";
            }

            return null;
        }

        private string BlockedPrefix(int serial)
        {
            return $"{DeployLog.Prefix(_owner.OwnerId, _definition.Id)} serial={serial} blocked ";
        }

        private void OnPlayerDamaged(Damageable _, DamageReport report)
        {
            if (_phase == Phase.Enemy && report.Kind == DamageKind.Bullet)
            {
                _playerBulletHits++;
            }
        }

        private bool SawSince(int mark, string line)
        {
            for (var i = mark; i < _lines.Count; i++)
            {
                if (_lines[i] == line)
                {
                    return true;
                }
            }

            return false;
        }

        private bool SawPrefixSince(int mark, string prefix)
        {
            for (var i = mark; i < _lines.Count; i++)
            {
                if (_lines[i].StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<string> Since(int mark)
        {
            for (var i = mark; i < _lines.Count; i++)
            {
                yield return _lines[i];
            }
        }

        // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
        private static void Teleport(CharacterController controller, Vector3 position)
        {
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = position;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        // 坂のレーンで足元の手前の床を狙わないよう、水平に向かせる。FirstPersonLook が自分の角度を持っているので、Transform を直接回すと戻る。
        private static void FaceForward(BootstrapWorld world)
        {
            var look = world.Look;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, 0f), look.Pitch));
            }
            else
            {
                world.Player.transform.rotation = Quaternion.identity;
            }
        }

        private static string Format(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
