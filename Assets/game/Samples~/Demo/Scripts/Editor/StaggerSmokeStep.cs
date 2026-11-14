using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Editor
{
    // batchmode には入力が無いので、脚を撃つ代わりに脚の当たり判定 (Hitbox.ApplyHit) へ直接当て、近接の代わりにプレイヤーの Damageable へ近接のダメージを渡す。
    public sealed class StaggerSmokeStep : ITpsActionSmokeStep
    {
        public const int LegShotDamage = 25;
        public const int PlayerMeleeDamage = 1;

        private readonly List<string> _logs = new List<string>();
        private Phase _phase;
        private EnemyAgent _enemy;
        private StaggerReaction _reaction;
        private Hitbox _legs;
        private PlayerStagger _playerStagger;
        private Stagger? _savedRule;
        private bool _sawPlayerFlag;

        private enum Phase
        {
            Setup,
            WaitForAttack,
            HitLegs,
            WaitEnemyRecover,
            RuleOff,
            StaggerPlayer,
            WaitPlayerRecover,
            Done,
        }

        public int Order => 500;

        public string Name => "stagger";

        public float TimeoutSeconds => 30f;

        public void Reset()
        {
            _logs.Clear();
            _phase = Phase.Setup;
            _enemy = null;
            _reaction = null;
            _legs = null;
            _playerStagger = null;
            _savedRule = null;
            _sawPlayerFlag = false;
        }

        public void OnLog(string condition)
        {
            if (condition != null && condition.StartsWith(StaggerReaction.LogTag, StringComparison.Ordinal))
            {
                _logs.Add(condition);
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            switch (_phase)
            {
                case Phase.Setup:
                    problem = Setup(world);
                    return false;
                case Phase.WaitForAttack:
                    // 撃ってくる状態から怯ませないと、怯んでいる間に撃たないことを確かめられない。
                    if (_enemy.State == EnemyState.Attack)
                    {
                        _phase = Phase.HitLegs;
                    }

                    return false;
                case Phase.HitLegs:
                    problem = HitLegs(world);
                    return false;
                case Phase.WaitEnemyRecover:
                    problem = WaitEnemyRecover();
                    return false;
                case Phase.RuleOff:
                    problem = HitWithRuleOff(world);
                    return false;
                case Phase.StaggerPlayer:
                    problem = StaggerPlayer(world);
                    return false;
                case Phase.WaitPlayerRecover:
                    problem = WaitPlayerRecover(world);
                    return _phase == Phase.Done;
                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            RestoreRule();
        }

        private string Setup(BootstrapWorld world)
        {
            if (world.Rules.Actions.Stagger != Stagger.On)
            {
                return $"the TPS range rules must turn stagger on; rerun {GameRulesAssetBuilder.MenuPath} (got {PlayerActions.FormatLog(world.Rules.Actions)})";
            }

            if (StaggerReaction.ActiveRule != Stagger.On)
            {
                return $"the stagger rule did not reach the enemies (StaggerReaction.ActiveRule={StaggerReaction.ActiveRule})";
            }

            var root = GameObject.Find(StaggerRangeFixture.FixtureName);
            if (root == null)
            {
                return $"the scene has no '{StaggerRangeFixture.FixtureName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            var navMesh = root.GetComponent<NavMeshBakeActivator>();
            if (navMesh == null || !navMesh.IsBaked)
            {
                return $"'{StaggerRangeFixture.FixtureName}' did not bake its NavMesh";
            }

            _enemy = root.GetComponentInChildren<EnemyAgent>(true);
            _reaction = _enemy != null ? _enemy.GetComponent<StaggerReaction>() : null;
            var legs = _enemy != null ? _enemy.transform.Find(StaggerRangeFixture.LegsName) : null;
            _legs = legs != null ? legs.GetComponent<Hitbox>() : null;
            if (_enemy == null || _reaction == null || _legs == null)
            {
                return $"'{StaggerRangeFixture.FixtureName}' has no enemy with a StaggerReaction and a '{StaggerRangeFixture.LegsName}' hitbox; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            if (!_enemy.gameObject.activeInHierarchy || !_enemy.IsInitialized || !_reaction.IsBound)
            {
                return $"the stagger enemy is not running (active={_enemy.gameObject.activeInHierarchy} initialized={_enemy.IsInitialized} bound={_reaction.IsBound})";
            }

            Teleport(world, StaggerRangeFixture.StandPoint);
            Face(world, _enemy.transform.position);
            _phase = Phase.WaitForAttack;
            return null;
        }

        private string HitLegs(BootstrapWorld world)
        {
            var id = _enemy.GetComponent<Damageable>().Id;
            var shooter = world.Player.transform.position;
            _legs.ApplyHit(LegShotDamage, Vector3.Distance(shooter, _legs.transform.position), DamageFalloff.None, shooter, _legs.transform.position - shooter);

            var staggered = StaggerReaction.FormatStaggerLog(id, StaggerKind.Kneel, 0f, HitZone.Limb, DamageKind.Bullet);
            var prefix = staggered.Substring(0, staggered.IndexOf(" seconds=", StringComparison.Ordinal));
            if (_reaction.Current != StaggerKind.Kneel || !Logged(prefix))
            {
                return $"shooting the legs did not make the enemy kneel (current={_reaction.Current}, logs: {string.Join(" | ", _logs)})";
            }

            if (!_enemy.IsInterrupted || _enemy.InterruptReason != StaggerReaction.InterruptReasonPrefix + StaggerKind.Kneel)
            {
                return $"the kneeling enemy was not interrupted (interrupted={_enemy.IsInterrupted} reason={_enemy.InterruptReason})";
            }

            var before = _logs.Count;
            _legs.ApplyHit(LegShotDamage, Vector3.Distance(shooter, _legs.transform.position), DamageFalloff.None, shooter, _legs.transform.position - shooter);
            if (!LoggedSince(before, StaggerReaction.FormatResistLog(id, StaggerKind.Kneel)) || LoggedSince(before, prefix))
            {
                return $"a second shot right away staggered the enemy again or logged no '{StaggerReaction.FormatResistLog(id, StaggerKind.Kneel)}'";
            }

            _phase = Phase.WaitEnemyRecover;
            return null;
        }

        private string WaitEnemyRecover()
        {
            var id = _enemy.GetComponent<Damageable>().Id;
            var recovered = StaggerReaction.FormatRecoverLog(id, StaggerKind.Kneel, 0);
            var prefix = recovered.Substring(0, recovered.LastIndexOf('=') + 1);
            var line = _logs.Find(l => l.StartsWith(prefix, StringComparison.Ordinal));
            if (line == null)
            {
                return null;
            }

            if (!string.Equals(line, recovered, StringComparison.Ordinal))
            {
                return $"the enemy fired while kneeling ('{line}')";
            }

            _phase = Phase.RuleOff;
            return null;
        }

        private string HitWithRuleOff(BootstrapWorld world)
        {
            // 敵の一時停止は敵自身の Update で解けるので、怯みが解けたフレームにはまだ残っていることがある。解けるまで待ってから撃つ。
            if (_enemy.IsInterrupted)
            {
                return null;
            }

            _savedRule = StaggerReaction.ActiveRule;
            StaggerReaction.ActiveRule = Stagger.Off;
            try
            {
                var before = _logs.Count;
                var shooter = world.Player.transform.position;
                _legs.ApplyHit(LegShotDamage, Vector3.Distance(shooter, _legs.transform.position), DamageFalloff.None, shooter, _legs.transform.position - shooter);
                if (_reaction.IsStaggered || _enemy.IsInterrupted || _logs.Count != before)
                {
                    return $"the enemy reacted to a leg shot with the stagger rule off (current={_reaction.Current} interrupted={_enemy.IsInterrupted} logs: {string.Join(" | ", _logs.GetRange(before, _logs.Count - before))})";
                }
            }
            finally
            {
                RestoreRule();
            }

            _phase = Phase.StaggerPlayer;
            return null;
        }

        private string StaggerPlayer(BootstrapWorld world)
        {
            // 敵の視界の外へ戻し、怯みを確かめている間に撃たれないようにする。
            Teleport(world, Vector3.zero);

            _playerStagger = world.Player.GetComponent<PlayerStagger>();
            if (_playerStagger == null || !_playerStagger.IsBound || _playerStagger.Rule != Stagger.On)
            {
                return $"the player has no bound PlayerStagger with the rule on (rule={(_playerStagger != null ? _playerStagger.Rule.ToString() : "none")})";
            }

            var damageable = world.PlayerDamageable;
            var before = _logs.Count;
            var source = world.Player.transform.position + world.Player.transform.forward;
            damageable.TakeDamage(PlayerMeleeDamage, source, world.Player.transform.position - source, DamageKind.Melee);

            var staggered = StaggerReaction.FormatStaggerLog(damageable.Id, StaggerKind.Stumble, 0f, null, DamageKind.Melee);
            var prefix = staggered.Substring(0, staggered.IndexOf(" seconds=", StringComparison.Ordinal));
            if (_playerStagger.Current != StaggerKind.Stumble || !LoggedSince(before, prefix))
            {
                return $"a melee hit did not make the player stumble (current={_playerStagger.Current})";
            }

            if (world.Look != null && !(world.Look.TurnMultiplier < 1f))
            {
                return $"the stumbling player's aim was not slowed (turn multiplier {world.Look.TurnMultiplier})";
            }

            _phase = Phase.WaitPlayerRecover;
            return null;
        }

        private string WaitPlayerRecover(BootstrapWorld world)
        {
            var reader = world.ActionReader;
            if (reader != null && reader.Current.IsStaggered)
            {
                _sawPlayerFlag = true;
            }

            if (_playerStagger.IsStaggered)
            {
                return null;
            }

            if (reader != null && !_sawPlayerFlag)
            {
                return "the player recovered without PlayerActionConditions.Staggered ever being raised";
            }

            if (world.Look != null && world.Look.TurnMultiplier != 1f)
            {
                return $"the player's aim stayed slowed after recovering (turn multiplier {world.Look.TurnMultiplier})";
            }

            _phase = Phase.Done;
            return null;
        }

        private void RestoreRule()
        {
            if (_savedRule.HasValue)
            {
                StaggerReaction.ActiveRule = _savedRule.Value;
                _savedRule = null;
            }
        }

        private bool Logged(string prefix)
        {
            return LoggedSince(0, prefix);
        }

        private bool LoggedSince(int index, string prefix)
        {
            for (var i = index; i < _logs.Count; i++)
            {
                if (_logs[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
        private static void Teleport(BootstrapWorld world, Vector3 floorPoint)
        {
            var controller = world.Controller;
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = new Vector3(floorPoint.x, floorPoint.y + GameBootstrap.PlayerSpawn.y, floorPoint.z);
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        // FirstPersonLook が自分の角度を持っているので、Transform を直接回すと次の視点操作で戻る。
        private static void Face(BootstrapWorld world, Vector3 target)
        {
            var toTarget = target - world.Player.transform.position;
            var yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            var look = world.Look;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, yaw), 0f));
            }
            else
            {
                world.Player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }
    }
}
