using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Editor
{
    // 能力の枠は 2 つとも埋まっている (グラップルと盾) ので、この手順の間だけ盾の枠を囮に入れ替え、終えたら戻す。
    // 射撃場のルールは Abilities を Off のまま配る (ほかの手順の前提を変えない) ので、この手順の間だけ On にする。
    // 置いたらプレイヤーを出現位置へ戻す。近くに残ると、囮が壊れた後に敵がプレイヤーを見つけて、調べに行かずに撃ちに来る。
    // 敵が気づいて壊し切るまでに消えないよう、持続の秒数だけを延ばした複製で置く。
    public sealed class DecoySmokeStep : ITpsActionSmokeStep
    {
        public const int SlotIndex = 1;
        public const float EnduringSeconds = 120f;

        // 調べに行った敵が囮のあった場所に着いたとみなす距離に足す幅。NavMesh の上に寄せた行き先との差を見込む。
        public const float SearchArrivalMargin = 1f;

        private enum Phase
        {
            Setup,
            Teleport,
            Settle,
            Placing,
            Lured,
            Searching,
            Done,
        }

        private readonly List<string> _lines = new List<string>();
        private Phase _phase;
        private int _mark;
        private BootstrapWorld _world;
        private AbilityDefinition _original;
        private DecoyDeployDefinition _enduring;
        private DeployOwner _owner;
        private EnemyAgent _enemy;
        private Decoy _decoy;
        private Vector3 _spot;
        private int _serial;
        private int _removedAt;

        public int Order => DecoyRangeFixture.FixtureOrder;

        public string Name => "decoy";

        public float TimeoutSeconds => 60f;

        public void Reset()
        {
            _lines.Clear();
            _phase = Phase.Setup;
            _mark = 0;
            _world = null;
            _original = null;
            _enduring = null;
            _owner = null;
            _enemy = null;
            _decoy = null;
            _spot = Vector3.zero;
            _serial = 0;
            _removedAt = -1;
        }

        public void OnLog(string condition)
        {
            if (condition == null)
            {
                return;
            }

            if (condition.StartsWith(DecoyLog.Tag, StringComparison.Ordinal)
                || condition.StartsWith(DeployLog.Tag, StringComparison.Ordinal)
                || condition.StartsWith(EnemyStealth.StageLogPrefix(DecoyRangeFixture.EnemyId), StringComparison.Ordinal)
                || condition.StartsWith($"{EnemyAgent.LogTag} id={DecoyRangeFixture.EnemyId} ", StringComparison.Ordinal))
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
                case Phase.Teleport:
                    _owner.Clear(DeployRemoval.Discarded);
                    host.Equip(SlotIndex, _enduring);
                    Teleport(world.Controller, DecoyRangeFixture.PlayerGround + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f));
                    FaceForward(world);
                    _phase = Phase.Settle;
                    return false;
                case Phase.Settle:
                    problem = Use(world, host);
                    return false;
                case Phase.Placing:
                    problem = host.IsRunning(SlotIndex) ? null : Placed(world);
                    return false;
                case Phase.Lured:
                    problem = CheckLured();
                    return false;
                case Phase.Searching:
                    problem = CheckSearching();
                    return _phase == Phase.Done;
                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            // 残すと、この後の手順で敵がプレイヤーを撃つ。
            if (_enemy != null)
            {
                _enemy.gameObject.SetActive(false);
            }

            if (_owner != null)
            {
                _owner.Clear(DeployRemoval.Discarded);
            }

            if (_world != null && _world.Abilities != null)
            {
                if (_enduring != null)
                {
                    _world.Abilities.Equip(SlotIndex, _original);
                }

                _world.Abilities.Configure(_world.Rules);
            }

            if (_enduring != null)
            {
                Object.Destroy(_enduring);
                _enduring = null;
            }
        }

        private string Setup(BootstrapWorld world, AbilityHost host)
        {
            var yard = GameObject.Find(DecoyRangeFixture.RootName);
            if (yard == null)
            {
                return $"the scene has no '{DecoyRangeFixture.RootName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            var navMesh = yard.GetComponent<NavMeshBakeActivator>();
            if (navMesh == null || !navMesh.IsBaked)
            {
                return $"'{DecoyRangeFixture.RootName}' did not bake its NavMesh";
            }

            var child = yard.transform.Find(DecoyRangeFixture.EnemyName);
            _enemy = child != null ? child.GetComponent<EnemyAgent>() : null;
            if (_enemy == null)
            {
                return $"'{DecoyRangeFixture.RootName}' has no '{DecoyRangeFixture.EnemyName}' enemy; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            if (_enemy.gameObject.activeSelf)
            {
                return "the decoy yard's enemy was awake before the decoy step; it must wait inactive";
            }

            var stealth = world.Player.GetComponent<StealthTarget>();
            if (stealth == null || stealth.Rule != Stealth.On)
            {
                return $"the player has no StealthTarget with stealth On; the TPS range rules must turn stealth on (rerun {GameRulesAssetBuilder.MenuPath}; got {PlayerActions.FormatLog(world.Rules.Actions)})";
            }

            var asset = AssetDatabase.LoadAssetAtPath<DecoyDeployDefinition>(AbilityAssetBuilder.DecoyAbilityPath);
            if (asset == null)
            {
                return $"'{AbilityAssetBuilder.DecoyAbilityPath}' is missing; rerun {AbilityAssetBuilder.MenuPath} or {TpsRangeSceneBuilder.MenuPath}";
            }

            _enduring = Object.Instantiate(asset);
            _enduring.name = asset.name;
            _enduring.Id = asset.Id;
            _enduring.LifetimeSeconds = EnduringSeconds;
            _original = host.Definition(SlotIndex);
            _owner = DeployOwner.GetOrAdd(world.Player);
            _owner.Clear(DeployRemoval.Discarded);
            host.Configure(world.Rules.WithActions(world.Rules.Actions.WithAbilities(Abilities.On)));
            _phase = Phase.Teleport;
            return null;
        }

        private string Use(BootstrapWorld world, AbilityHost host)
        {
            if (!world.Mover.IsGrounded)
            {
                return null;
            }

            host.ResetSlots();
            _mark = _lines.Count;
            if (!host.TryUse(SlotIndex))
            {
                return $"the player could not use the decoy ability (block {host.UseBlock(SlotIndex)})";
            }

            _phase = Phase.Placing;
            return null;
        }

        // 置いてから敵を起こす。起きた時点で囮が見えるように。
        private string Placed(BootstrapWorld world)
        {
            var kind = _enduring.Id;
            if (!SawPrefixSince(_mark, DeployLog.Prefix(_owner.OwnerId, kind) + " placed serial="))
            {
                return $"releasing the decoy ability did not place a decoy (lines: {string.Join(" | ", Since(_mark))})";
            }

            var active = _owner.Active(kind);
            var placed = active.Count > 0 ? active[active.Count - 1] : null;
            _decoy = placed != null ? placed.GetComponent<Decoy>() : null;
            if (_decoy == null || !_decoy.IsArmed)
            {
                return "the placed decoy is not in the owner's list or is not armed";
            }

            _serial = placed.Serial;
            _spot = _decoy.Damageable.GroundPoint;
            var armed = DecoyLog.Prefix(_owner.OwnerId, kind, _serial) + " armed ";
            if (!SawSince(_mark, line => line.StartsWith(armed, StringComparison.Ordinal) && line.EndsWith(" stealth=On", StringComparison.Ordinal)))
            {
                return $"the placed decoy did not log '{armed}' with the player's stealth rule (lines: {string.Join(" | ", Since(_mark))})";
            }

            Teleport(world.Controller, GameBootstrap.PlayerSpawn);
            _enemy.gameObject.SetActive(true);
            if (!_enemy.IsInitialized)
            {
                return $"'{_enemy.name}' did not initialize when it woke up";
            }

            _enemy.enabled = true;
            _phase = Phase.Lured;
            return null;
        }

        private string CheckLured()
        {
            var kind = _enduring.Id;
            var removed = DeployLog.Prefix(_owner.OwnerId, kind) + $" serial={_serial} removed ";
            var at = IndexSince(_mark, line => line.StartsWith(removed, StringComparison.Ordinal));
            if (at < 0)
            {
                return null;
            }

            if (_lines[at] != DeployLog.Removed(_owner.OwnerId, kind, _serial, DeployRemoval.Destroyed))
            {
                return $"the decoy was removed before the enemy broke it ('{_lines[at]}')";
            }

            var seen = IndexSince(_mark, line => line == DecoyLog.Seen(DecoyRangeFixture.EnemyId, _owner.OwnerId, kind, _serial));
            if (seen < 0 || seen > at)
            {
                return $"the decoy broke without '{DecoyRangeFixture.EnemyId}' seeing it (lines: {string.Join(" | ", Since(_mark))})";
            }

            var engaged = IndexSince(_mark, line => line.StartsWith(EnemyStealth.StageLogPrefix(DecoyRangeFixture.EnemyId) + AwarenessStage.Engaged, StringComparison.Ordinal));
            if (engaged < 0 || engaged > at)
            {
                return $"'{DecoyRangeFixture.EnemyId}' did not become engaged with the decoy before breaking it (lines: {string.Join(" | ", Since(_mark))})";
            }

            var blocked = DeployLog.Prefix(_owner.OwnerId, kind) + $" serial={_serial} blocked ";
            var shooter = " shooter=" + DecoyRangeFixture.EnemyId;
            if (IndexSince(_mark, line => line.StartsWith(blocked, StringComparison.Ordinal) && line.EndsWith(shooter, StringComparison.Ordinal)) < 0)
            {
                return $"the decoy broke without '{DecoyRangeFixture.EnemyId}' shooting it (lines: {string.Join(" | ", Since(_mark))})";
            }

            _removedAt = at;
            _phase = Phase.Searching;
            return null;
        }

        private string CheckSearching()
        {
            var lost = DecoyLog.Lost(DecoyRangeFixture.EnemyId, _owner.OwnerId, _enduring.Id, _serial, _spot.ToNumerics(), true);
            if (IndexSince(_removedAt, line => line == lost) < 0)
            {
                return null;
            }

            var search = $"{EnemyAgent.LogTag} id={DecoyRangeFixture.EnemyId} state={EnemyState.Search} ";
            if (IndexSince(_removedAt, line => line.StartsWith(search, StringComparison.Ordinal)) < 0)
            {
                return null;
            }

            var ground = _enemy.GroundPosition;
            var flat = new Vector2(ground.x - _spot.x, ground.z - _spot.z);
            if (flat.magnitude > _enemy.Brain.Spec.ArrivalDistance + SearchArrivalMargin)
            {
                return null;
            }

            _enemy.gameObject.SetActive(false);
            _phase = Phase.Done;
            return null;
        }

        private int IndexSince(int mark, Predicate<string> match)
        {
            for (var i = Math.Max(0, mark); i < _lines.Count; i++)
            {
                if (match(_lines[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool SawSince(int mark, Predicate<string> match)
        {
            return IndexSince(mark, match) >= 0;
        }

        private bool SawPrefixSince(int mark, string prefix)
        {
            return SawSince(mark, l => l.StartsWith(prefix, StringComparison.Ordinal));
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
