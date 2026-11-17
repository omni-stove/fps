using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    // 発見ゲージの囲い (StealthRangeFixture) の見張りを使う。囲いを別に置くと、射撃場の床に空きを探し直すことになるため。
    // 発見ゲージの手順 (StealthSmokeStep) の後に回すので、見張りとプレイヤーのルールを自分で戻してから始める。
    // 能力の枠は 2 つとも埋まっているので、この手順の間だけグラップルの枠を物を投げる能力に入れ替え、終えたら戻す。
    public sealed class DistractionSmokeStep : ITpsActionSmokeStep
    {
        public const int SlotIndex = 0;

        public const float MinInvestigateProgress = 1f;

        private readonly List<string> _lines = new List<string>();
        private Phase _phase;
        private int _mark;
        private BootstrapWorld _world;
        private EnemyAgent _enemy;
        private EnemyStealth _stealth;
        private StealthTarget _target;
        private AbilityDefinition _original;
        private bool _equipped;
        private Vector3 _spot;
        private float _spotDistance;

        private enum Phase
        {
            Setup,
            Calm,
            Throw,
            Investigate,
            ThrowWhileEngaged,
            Done,
        }

        public int Order => 105;

        public string Name => "distraction";

        public float TimeoutSeconds => 45f;

        // 見張りが +Z を向くので、手前 (StealthRangeFixture.ApproachStart) に立つプレイヤーは視野の真後ろにいて見つからない。
        private static Quaternion GuardFacing => Quaternion.LookRotation(Vector3.forward, Vector3.up);

        private static Vector3 ThrowSpot => StealthRangeFixture.ApproachStart;

        // 横の壁へ投げれば、手元からすぐ当たって落ちる。弧の行き先が床の高さや見張りの体で変わらないように。
        private static Vector3 WallPoint => new Vector3(StealthRangeFixture.LeftWallCenter.x, 0f, ThrowSpot.z);

        public void Reset()
        {
            _lines.Clear();
            _phase = Phase.Setup;
            _mark = 0;
            _world = null;
            _enemy = null;
            _stealth = null;
            _target = null;
            _original = null;
            _equipped = false;
            _spot = Vector3.zero;
            _spotDistance = 0f;
        }

        public void OnLog(string condition)
        {
            if (condition != null
                && (condition.StartsWith(EnemyStealth.HeardLogPrefix(StealthRangeFixture.EnemyId), StringComparison.Ordinal)
                    || condition.StartsWith(ThrownDistraction.LogTag, StringComparison.Ordinal)))
            {
                _lines.Add(condition);
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            _world = world;
            if (world.Abilities == null)
            {
                problem = "the player has no AbilityHost";
                return false;
            }

            switch (_phase)
            {
                case Phase.Setup:
                    problem = Setup(world);
                    return false;
                case Phase.Calm:
                    problem = Calm(world);
                    return false;
                case Phase.Throw:
                    problem = CheckInvestigating(world);
                    return false;
                case Phase.Investigate:
                    problem = Investigate(world);
                    return false;
                case Phase.ThrowWhileEngaged:
                    problem = CheckIgnored(world);
                    return _phase == Phase.Done;
                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            Restore();
        }

        private string Setup(BootstrapWorld world)
        {
            var area = GameObject.Find(StealthRangeFixture.AreaName);
            if (area == null)
            {
                return $"the scene has no '{StealthRangeFixture.AreaName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            _enemy = area.GetComponentInChildren<EnemyAgent>(true);
            if (_enemy == null || !_enemy.gameObject.activeInHierarchy || !_enemy.IsInitialized)
            {
                return $"'{StealthRangeFixture.EnemyName}' was not activated after the NavMesh was built ({RuntimeNavMeshArea.LogTag} log)";
            }

            _stealth = _enemy.GetComponent<EnemyStealth>();
            _target = world.Player.GetComponent<StealthTarget>();
            if (_stealth == null || _stealth.Awareness == null || _target == null)
            {
                return $"'{StealthRangeFixture.EnemyName}' has no initialized EnemyStealth or the player has no StealthTarget";
            }

            var definition = AssetDatabase.LoadAssetAtPath<DistractionThrowDefinition>(AbilityAssetBuilder.DistractionAbilityPath);
            if (definition == null)
            {
                return $"'{AbilityAssetBuilder.DistractionAbilityPath}' is missing; rerun {AbilityAssetBuilder.MenuPath} or {TpsRangeSceneBuilder.MenuPath}";
            }

            var host = world.Abilities;
            _original = host.Definition(SlotIndex);
            host.Equip(SlotIndex, definition);
            _equipped = true;
            host.Configure(world.Rules.WithActions(world.Rules.Actions.WithAbilities(Abilities.On)));

            _target.Rule = Stealth.On;
            Teleport(world.Controller, GameBootstrap.PlayerSpawn);
            _enemy.ResetTo(StealthRangeFixture.EnemyFeet, GuardFacing);
            _stealth.ResetAwareness();
            _phase = Phase.Calm;
            return null;
        }

        // EnemyStealth は相手のルールを自分の Update で読むので、On が効いて落ち着いてから投げる。
        private string Calm(BootstrapWorld world)
        {
            if (_stealth.Rule != Stealth.On || StealthAwareness.IsFighting(_enemy.State) || _stealth.Stage != AwarenessStage.Unaware)
            {
                return null;
            }

            var problem = ThrowAtTheWall(world);
            if (problem == null)
            {
                _phase = Phase.Throw;
            }

            return problem;
        }

        private string CheckInvestigating(BootstrapWorld world)
        {
            if (!ThrowFinished(world))
            {
                return null;
            }

            var heard = FirstHeardSince(_mark);
            if (heard == null)
            {
                return null;
            }

            if (!SawPrefixSince(_mark, ThrownDistraction.LandedLogPrefix))
            {
                return $"the guard heard something before the thrown object landed ('{heard}')";
            }

            if (Token(heard, " outcome=") != nameof(HearingOutcome.Investigating) || Token(heard, " rule=") != nameof(Stealth.On))
            {
                return $"the guard did not go to investigate the clatter behind it ('{heard}')";
            }

            var lastSeen = _stealth.Awareness.LastSeen;
            if (_stealth.Stage != AwarenessStage.Alerted || !lastSeen.HasValue)
            {
                return $"the guard heard the clatter but is {_stealth.Stage} with last seen {(lastSeen.HasValue ? lastSeen.Value.ToUnity().ToString("F1") : "none")}";
            }

            _spot = lastSeen.Value.ToUnity();
            _spotDistance = HorizontalDistance(_enemy.GroundPosition, _spot);
            _phase = Phase.Investigate;
            return null;
        }

        private string Investigate(BootstrapWorld world)
        {
            if (_stealth.Stage == AwarenessStage.Engaged)
            {
                return "the guard engaged while investigating the clatter; the player should be out of sight";
            }

            var remaining = HorizontalDistance(_enemy.GroundPosition, _spot);
            if (_spotDistance - remaining < MinInvestigateProgress && remaining > _enemy.Brain.Spec.ArrivalDistance + 0.5f)
            {
                return _stealth.Stage == AwarenessStage.Alerted ? null : $"the guard stopped investigating at {_stealth.Stage} before walking to the clatter";
            }

            // 仲間から交戦を伝えられた状態にする。プレイヤーを見せて撃ち合わせると、この手順の後に体力が減ったまま残る。
            // 物音の方 (投げる所) を向いたままだと次に投げるプレイヤーが見えて撃たれるので、背を向けた元の所へ戻してから伝える。
            _enemy.ResetTo(StealthRangeFixture.EnemyFeet, GuardFacing);
            if (!_stealth.ReceiveAlarm(null, StealthRangeFixture.EnemyFeet) || _stealth.Stage != AwarenessStage.Engaged)
            {
                return $"the guard could not be put into the engaged stage (stage {_stealth.Stage})";
            }

            var problem = ThrowAtTheWall(world);
            if (problem == null)
            {
                _phase = Phase.ThrowWhileEngaged;
            }

            return problem;
        }

        private string CheckIgnored(BootstrapWorld world)
        {
            if (!ThrowFinished(world))
            {
                return null;
            }

            var heard = FirstHeardSince(_mark);
            if (heard == null)
            {
                return null;
            }

            if (Token(heard, " outcome=") != nameof(HearingOutcome.Engaged))
            {
                return $"the engaged guard reacted to the clatter ('{heard}')";
            }

            Restore();
            _phase = Phase.Done;
            return null;
        }

        private string ThrowAtTheWall(BootstrapWorld world)
        {
            var host = world.Abilities;
            Teleport(world.Controller, ThrowSpot + (Vector3.up * GameBootstrap.PlayerSpawn.y));
            FaceTowards(world, WallPoint);
            host.ResetSlots();
            _mark = _lines.Count;
            if (!host.TryUse(SlotIndex))
            {
                return $"the player could not use the distraction ability (block {host.UseBlock(SlotIndex)})";
            }

            return null;
        }

        // 投げたらすぐ囲いの外へ移す。物音の方を振り向いた見張りに見つからないように。
        private bool ThrowFinished(BootstrapWorld world)
        {
            if (world.Abilities.IsRunning(SlotIndex))
            {
                return false;
            }

            if (Vector3.Distance(world.Player.transform.position, GameBootstrap.PlayerSpawn) > 1f)
            {
                Teleport(world.Controller, GameBootstrap.PlayerSpawn);
            }

            return true;
        }

        private void Restore()
        {
            var world = _world;
            if (world == null)
            {
                return;
            }

            if (_equipped && world.Abilities != null)
            {
                world.Abilities.Equip(SlotIndex, _original);
                world.Abilities.Configure(world.Rules);
                _equipped = false;
            }

            if (_target != null)
            {
                _target.Rule = world.Rules.Actions.Stealth;
            }

            if (_enemy != null && _enemy.IsInitialized && _stealth != null)
            {
                _enemy.ResetTo(StealthRangeFixture.EnemyFeet, StealthRangeFixture.EnemyFacing);
                _stealth.ResetAwareness();
            }
        }

        private string FirstHeardSince(int mark)
        {
            var prefix = EnemyStealth.HeardLogPrefix(StealthRangeFixture.EnemyId);
            for (var i = mark; i < _lines.Count; i++)
            {
                if (_lines[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return _lines[i];
                }
            }

            return null;
        }

        private bool SawPrefixSince(int mark, string prefix)
        {
            for (var i = mark; i < _lines.Count; i++)
            {
                if (_lines[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Token(string condition, string key)
        {
            var start = condition.IndexOf(key, StringComparison.Ordinal);
            if (start < 0)
            {
                return string.Empty;
            }

            start += key.Length;
            var end = condition.IndexOf(' ', start);
            return end < 0 ? condition.Substring(start) : condition.Substring(start, end - start);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var delta = a - b;
            delta.y = 0f;
            return delta.magnitude;
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

        // FirstPersonLook が自分の角度を持っているので、Transform を直接回すと次の視点操作で戻る。
        private static void FaceTowards(BootstrapWorld world, Vector3 point)
        {
            var direction = point - world.Player.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return;
            }

            var yaw = Quaternion.LookRotation(direction, Vector3.up).eulerAngles.y;
            var look = world.Look;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, yaw), look.Pitch));
            }
            else
            {
                world.Player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }
    }
}
