using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Editor
{
    // 能力の枠は 2 つとも埋まっている (グラップルと盾) ので、この手順の間だけ盾の枠をタレットに入れ替え、終えたら戻す。
    // 射撃場のルールは Abilities を Off のまま配る (ほかの手順の前提を変えない) ので、この手順の間だけ On にする。
    // 確かめること: 置いたタレットが敵を撃って倒す・撃ち尽くすと消える・敵がタレットを撃って壊す。
    // 撃ち尽くす・壊されるの 2 つは、置いた時点の弾の数と持続の秒数を変えた複製で試す。アセットの値のままだと、撃ち尽くすのと壊されるのどちらが先かが決まらないため。
    // 倒す・撃ち尽くすの間は敵の AI を止めておく。撃ち返されてタレットが先に壊れると、確かめたいことが起きない。
    public sealed class TurretSmokeStep : ITpsActionSmokeStep
    {
        public const int SlotIndex = 1;
        public const int ExpendRounds = 5;
        public const int EnduringRounds = 100000;
        public const float EnduringSeconds = 120f;

        private enum Phase
        {
            Setup,
            Kill,
            Expend,
            Destroy,
            Done,
        }

        private enum UseState
        {
            Teleport,
            Settle,
            Running,
            Placed,
        }

        private readonly List<string> _lines = new List<string>();
        private Phase _phase;
        private UseState _use;
        private int _mark;
        private BootstrapWorld _world;
        private AbilityDefinition _original;
        private TurretDeployDefinition _definition;
        private TurretDeployDefinition _expending;
        private TurretDeployDefinition _enduring;
        private DeployOwner _owner;
        private EnemyAgent _prey;
        private EnemyAgent _raider;
        private Turret _turret;
        private int _serial;

        public int Order => TurretRangeFixture.FixtureOrder;

        public string Name => "turret";

        public float TimeoutSeconds => 60f;

        public void Reset()
        {
            _lines.Clear();
            _phase = Phase.Setup;
            _use = UseState.Teleport;
            _mark = 0;
            _world = null;
            _original = null;
            _definition = null;
            _expending = null;
            _enduring = null;
            _owner = null;
            _prey = null;
            _raider = null;
            _turret = null;
            _serial = 0;
        }

        public void OnLog(string condition)
        {
            if (condition != null
                && (condition.StartsWith(TurretLog.Tag, StringComparison.Ordinal) || condition.StartsWith(DeployLog.Tag, StringComparison.Ordinal)))
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
                case Phase.Kill:
                    problem = PlaceWith(world, host, _definition, _prey, false) ?? (_use == UseState.Placed ? CheckKill() : null);
                    return false;
                case Phase.Expend:
                    problem = PlaceWith(world, host, _expending, _raider, false) ?? (_use == UseState.Placed ? CheckExpended() : null);
                    return false;
                case Phase.Destroy:
                    problem = PlaceWith(world, host, _enduring, _raider, true) ?? (_use == UseState.Placed ? CheckDestroyed() : null);
                    return _phase == Phase.Done;
                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            // 残すと、この後の手順で敵がプレイヤーを撃つ。
            Sleep(_prey);
            Sleep(_raider);

            if (_owner != null)
            {
                _owner.Clear(DeployRemoval.Discarded);
            }

            if (_world != null && _world.Abilities != null)
            {
                if (_definition != null)
                {
                    _world.Abilities.Equip(SlotIndex, _original);
                }

                _world.Abilities.Configure(_world.Rules);
            }

            DestroyCopy(_expending);
            DestroyCopy(_enduring);
            _expending = null;
            _enduring = null;
        }

        private string Setup(BootstrapWorld world, AbilityHost host)
        {
            var yard = GameObject.Find(TurretRangeFixture.RootName);
            if (yard == null)
            {
                return $"the scene has no '{TurretRangeFixture.RootName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            var navMesh = yard.GetComponent<NavMeshBakeActivator>();
            if (navMesh == null || !navMesh.IsBaked)
            {
                return $"'{TurretRangeFixture.RootName}' did not bake its NavMesh";
            }

            _prey = FindEnemy(yard.transform, TurretRangeFixture.PreyName);
            _raider = FindEnemy(yard.transform, TurretRangeFixture.RaiderName);
            if (_prey == null || _raider == null)
            {
                return $"'{TurretRangeFixture.RootName}' has no '{TurretRangeFixture.PreyName}' and '{TurretRangeFixture.RaiderName}' enemies; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            if (_prey.gameObject.activeSelf || _raider.gameObject.activeSelf)
            {
                return "the turret yard's enemies were awake before the turret step; they must wait inactive";
            }

            var asset = AssetDatabase.LoadAssetAtPath<TurretDeployDefinition>(AbilityAssetBuilder.TurretAbilityPath);
            if (asset == null)
            {
                return $"'{AbilityAssetBuilder.TurretAbilityPath}' is missing; rerun {AbilityAssetBuilder.MenuPath} or {TpsRangeSceneBuilder.MenuPath}";
            }

            _expending = Copy(asset, ExpendRounds, EnduringSeconds);
            _enduring = Copy(asset, EnduringRounds, EnduringSeconds);
            _original = host.Definition(SlotIndex);
            _definition = asset;
            _owner = DeployOwner.GetOrAdd(world.Player);
            _owner.Clear(DeployRemoval.Discarded);
            host.Configure(world.Rules.WithActions(world.Rules.Actions.WithAbilities(Abilities.On)));
            _phase = Phase.Kill;
            _use = UseState.Teleport;
            return null;
        }

        // 敵を起こしてから置く。置いたタレットが最初の選び直しで敵を見つけられるように。
        private string PlaceWith(BootstrapWorld world, AbilityHost host, TurretDeployDefinition definition, EnemyAgent enemy, bool acting)
        {
            switch (_use)
            {
                case UseState.Teleport:
                    _owner.Clear(DeployRemoval.Discarded);
                    host.Equip(SlotIndex, definition);
                    Teleport(world.Controller, TurretRangeFixture.PlayerGround + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f));
                    FaceForward(world);
                    _use = UseState.Settle;
                    return null;
                case UseState.Settle:
                    if (!world.Mover.IsGrounded)
                    {
                        return null;
                    }

                    var woke = Wake(enemy, false);
                    if (woke != null)
                    {
                        return woke;
                    }

                    host.ResetSlots();
                    _mark = _lines.Count;
                    if (!host.TryUse(SlotIndex))
                    {
                        return $"the player could not use the turret ability (block {host.UseBlock(SlotIndex)})";
                    }

                    _use = UseState.Running;
                    return null;
                case UseState.Running:
                    if (host.IsRunning(SlotIndex))
                    {
                        return null;
                    }

                    var problem = RequirePlaced(definition);
                    if (problem != null)
                    {
                        return problem;
                    }

                    // 置いてから AI を動かす。タレットが置かれる前に起きると、遠いプレイヤーを先に狙う。
                    enemy.enabled = acting;
                    _use = UseState.Placed;
                    return null;
                default:
                    return null;
            }
        }

        private string RequirePlaced(TurretDeployDefinition definition)
        {
            var kind = definition.Id;
            var prefix = DeployLog.Prefix(_owner.OwnerId, kind) + " placed serial=";
            if (!SawPrefixSince(_mark, prefix))
            {
                return $"releasing the turret ability did not place a turret (lines: {string.Join(" | ", Since(_mark))})";
            }

            var active = _owner.Active(kind);
            var placed = active.Count > 0 ? active[active.Count - 1] : null;
            _turret = placed != null ? placed.GetComponent<Turret>() : null;
            if (_turret == null || !_turret.IsArmed)
            {
                return "the placed turret is not in the owner's list or is not armed";
            }

            _serial = placed.Serial;
            var armed = TurretLog.Prefix(_owner.OwnerId, kind, _serial) + " armed ";
            if (!SawPrefixSince(_mark, armed))
            {
                return $"the placed turret did not log '{armed}' (lines: {string.Join(" | ", Since(_mark))})";
            }

            return null;
        }

        private string CheckKill()
        {
            var kind = _definition.Id;
            if (!SawSince(_mark, TurretLog.Killed(_owner.OwnerId, kind, _serial, TurretRangeFixture.PreyId)))
            {
                return Ended(kind, "before killing the enemy in front of it");
            }

            if (!SawSince(_mark, TurretLog.Target(_owner.OwnerId, kind, _serial, TurretRangeFixture.PreyId)))
            {
                return $"the turret killed '{TurretRangeFixture.PreyId}' without logging it as its target";
            }

            Sleep(_prey);
            Next(Phase.Expend);
            return null;
        }

        private string CheckExpended()
        {
            var kind = _expending.Id;
            if (!SawSince(_mark, DeployLog.Removed(_owner.OwnerId, kind, _serial, DeployRemoval.Expended)))
            {
                return Ended(kind, "before running out of rounds");
            }

            var fired = CountPrefixSince(_mark, TurretLog.Prefix(_owner.OwnerId, kind, _serial) + " fire ");
            if (fired != ExpendRounds)
            {
                return $"the turret fired {fired} rounds before running out (expected {ExpendRounds})";
            }

            if (_turret != null && !_turret.Deployable.IsRemoved)
            {
                return "the turret logged running out of rounds but is still active";
            }

            Next(Phase.Destroy);
            return null;
        }

        private string CheckDestroyed()
        {
            var kind = _enduring.Id;
            if (!SawSince(_mark, DeployLog.Removed(_owner.OwnerId, kind, _serial, DeployRemoval.Destroyed)))
            {
                return Ended(kind, "before the enemy destroyed it");
            }

            var blocked = DeployLog.Prefix(_owner.OwnerId, kind) + $" serial={_serial} blocked ";
            var shooter = " shooter=" + TurretRangeFixture.RaiderId;
            if (!SawSince(_mark, line => line.StartsWith(blocked, StringComparison.Ordinal) && line.EndsWith(shooter, StringComparison.Ordinal)))
            {
                return $"the turret was destroyed without '{TurretRangeFixture.RaiderId}' shooting it (lines: {string.Join(" | ", Since(_mark))})";
            }

            Sleep(_raider);
            _phase = Phase.Done;
            return null;
        }

        // 待っている間に別の理由で消えたら、その理由を添えて失敗にする。消えていなければ待ち続ける。
        private string Ended(string kind, string waitingFor)
        {
            var removed = DeployLog.Prefix(_owner.OwnerId, kind) + $" serial={_serial} removed ";
            foreach (var line in Since(_mark))
            {
                if (line.StartsWith(removed, StringComparison.Ordinal))
                {
                    return $"the turret was removed {waitingFor} ('{line}')";
                }
            }

            return null;
        }

        private void Next(Phase phase)
        {
            _phase = phase;
            _use = UseState.Teleport;
            _turret = null;
        }

        private static string Wake(EnemyAgent enemy, bool acting)
        {
            if (!enemy.gameObject.activeSelf)
            {
                enemy.gameObject.SetActive(true);
            }

            if (!enemy.IsInitialized)
            {
                return $"'{enemy.name}' did not initialize when it woke up";
            }

            enemy.enabled = acting;
            return null;
        }

        private static void Sleep(EnemyAgent enemy)
        {
            if (enemy != null)
            {
                enemy.gameObject.SetActive(false);
            }
        }

        private static EnemyAgent FindEnemy(Transform yard, string name)
        {
            var child = yard.Find(name);
            return child != null ? child.GetComponent<EnemyAgent>() : null;
        }

        private static TurretDeployDefinition Copy(TurretDeployDefinition source, int rounds, float seconds)
        {
            var copy = Object.Instantiate(source);
            copy.name = source.name;
            copy.Id = source.Id;
            copy.Rounds = rounds;
            copy.LifetimeSeconds = seconds;
            return copy;
        }

        private static void DestroyCopy(Object copy)
        {
            if (copy != null)
            {
                Object.Destroy(copy);
            }
        }

        private bool SawSince(int mark, string line)
        {
            return SawSince(mark, l => l == line);
        }

        private bool SawSince(int mark, Predicate<string> match)
        {
            for (var i = mark; i < _lines.Count; i++)
            {
                if (match(_lines[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private bool SawPrefixSince(int mark, string prefix)
        {
            return SawSince(mark, l => l.StartsWith(prefix, StringComparison.Ordinal));
        }

        private int CountPrefixSince(int mark, string prefix)
        {
            var count = 0;
            for (var i = mark; i < _lines.Count; i++)
            {
                if (_lines[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
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

        // 置く向き (+Z) を向かせる。FirstPersonLook が自分の角度を持っているので、Transform を直接回すと戻る。
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
    }
}
