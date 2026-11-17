using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Editor
{
    // batchmode には入力が無いので、近接の入力の代わりに MeleeController.TryMelee を使う。
    // 射撃場のルールは Melee を Off のまま配り、発見ゲージの手順 (StealthSmokeStep) は Stealth を Off にしたまま終えるので、この手順の間だけどちらも On にする。
    public sealed class TakedownSmokeStep : ITpsActionSmokeStep
    {
        public const float LingerSeconds = 1f;

        private const float _levelTolerance = 1e-4f;

        private enum Phase
        {
            Setup,
            Place,
            WaitReady,
            Aware,
            Takedown,
            WaitResolved,
            Linger,
            Done,
        }

        private readonly List<string> _lines = new List<string>();
        private Phase _phase;
        private BootstrapWorld _world;
        private MeleeController _melee;
        private ContextMeleeAction _context;
        private StealthTarget _target;
        private EnemyAgent _victim;
        private EnemyStealth _victimStealth;
        private Damageable _victimBody;
        private EnemyAgent _sentry;
        private EnemyStealth _sentryStealth;
        private int _mark;
        private float _sentryLevel;
        private float _resolvedAt;

        public int Order => 520;

        public string Name => "takedown";

        public float TimeoutSeconds => 40f;

        public void Reset()
        {
            _lines.Clear();
            _phase = Phase.Setup;
            _world = null;
            _melee = null;
            _context = null;
            _target = null;
            _victim = null;
            _victimStealth = null;
            _victimBody = null;
            _sentry = null;
            _sentryStealth = null;
            _mark = 0;
            _sentryLevel = 0f;
            _resolvedAt = 0f;
        }

        public void OnLog(string condition)
        {
            if (condition != null
                && (condition.StartsWith(ContextMeleeLog.LogTag, StringComparison.Ordinal)
                    || condition.StartsWith(SentryLogPrefix, StringComparison.Ordinal)))
            {
                _lines.Add(condition);
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            _world = world;
            switch (_phase)
            {
                case Phase.Setup:
                    problem = Setup(world);
                    return false;
                case Phase.Place:
                    problem = Place();
                    return false;
                case Phase.WaitReady:
                    WaitReady(world);
                    return false;
                case Phase.Aware:
                    problem = PressBehindAnAwareEnemy(world);
                    return false;
                case Phase.Takedown:
                    problem = StartTakedown(world);
                    return false;
                case Phase.WaitResolved:
                    problem = WaitResolved(world);
                    return false;
                case Phase.Linger:
                    problem = Linger(world);
                    return _phase == Phase.Done;
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

            if (_context != null)
            {
                _context.Configure(_world.Rules);
            }

            if (_melee != null)
            {
                _melee.Configure(_world.Rules);
            }

            if (_target != null)
            {
                _target.Rule = _world.Rules.Actions.Stealth;
            }

            Sleep(_sentry);
        }

        private static string SentryLogPrefix => $"{EnemyStealth.LogTag} id={StealthRangeFixture.SentryId} ";

        private string Setup(BootstrapWorld world)
        {
            var area = GameObject.Find(StealthRangeFixture.AreaName);
            if (area == null)
            {
                return $"the scene has no '{StealthRangeFixture.AreaName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            _victim = FindEnemy(area, StealthRangeFixture.EnemyId);
            _sentry = FindEnemy(area, StealthRangeFixture.SentryId);
            if (_victim == null || _sentry == null)
            {
                return $"'{StealthRangeFixture.AreaName}' has no '{StealthRangeFixture.EnemyName}' and '{StealthRangeFixture.SentryName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            if (!_victim.gameObject.activeInHierarchy || !_victim.IsInitialized)
            {
                return $"'{StealthRangeFixture.EnemyName}' was not activated after the NavMesh was built ({RuntimeNavMeshArea.LogTag} log)";
            }

            _victimStealth = _victim.GetComponent<EnemyStealth>();
            _victimBody = _victim.GetComponent<Damageable>();
            if (_victimStealth == null || _victimBody == null || _sentry.GetComponent<EnemyStealth>() == null)
            {
                return "the stealth enemies have no EnemyStealth or Damageable";
            }

            _melee = world.Player.GetComponent<MeleeController>();
            _context = world.Player.GetComponent<ContextMeleeAction>();
            _target = world.Player.GetComponent<StealthTarget>();
            if (_melee == null || !_melee.IsInitialized || _context == null || _target == null)
            {
                return $"the player has no initialized MeleeController with a ContextMeleeAction and a StealthTarget; are {nameof(MeleeActionInstaller)}, {nameof(ContextMeleeActionInstaller)} and {nameof(StealthActionInstaller)} registered?";
            }

            var rules = world.Rules.WithActions(world.Rules.Actions.WithMelee(Melee.On).WithStealth(Stealth.On));
            _melee.Configure(rules);
            _context.Configure(rules);
            _target.Rule = Stealth.On;
            if (!_context.enabled || _context.Selector.Find(ContextMeleeMove.TakedownId) == null)
            {
                return $"turning melee on did not enable the context melee with the takedown (enabled={_context.enabled} moves={_context.Selector.Moves.Count})";
            }

            // 見張りは Awake で EnemyAgent を、次の Start で EnemyStealth を初期化する。
            _sentry.gameObject.SetActive(true);
            Retreat(world);
            _phase = Phase.Place;
            return null;
        }

        private string Place()
        {
            if (!_sentry.IsInitialized)
            {
                return $"'{StealthRangeFixture.SentryName}' did not initialize when woken";
            }

            _victim.ResetTo(StealthRangeFixture.EnemyFeet, StealthRangeFixture.TakedownFacing);
            _sentry.ResetTo(StealthRangeFixture.SentryFeet, StealthRangeFixture.SentryFacing);
            _victimStealth.ResetAwareness();
            _phase = Phase.WaitReady;
            return null;
        }

        // 目標は EnemyAgent が Player タグから探し、ルールは EnemyStealth が目標から読むので、どちらも揃うまで待つ。
        private void WaitReady(BootstrapWorld world)
        {
            _sentryStealth ??= _sentry.GetComponent<EnemyStealth>();
            if (!IsWatching(_victim, _victimStealth, world) || !IsWatching(_sentry, _sentryStealth, world))
            {
                return;
            }

            _phase = Phase.Aware;
        }

        // 気づきに届いた敵には、背後からでも技を選ばない (ふつうの近接になる)。
        private string PressBehindAnAwareEnemy(BootstrapWorld world)
        {
            var awareness = _victimStealth.Awareness;
            Approach(world);
            awareness.Meter.Raise(awareness.Spec.NoticeThreshold);
            if (_victimStealth.Stage != AwarenessStage.Noticed)
            {
                return $"raising the meter to the notice threshold left the enemy {_victimStealth.Stage}";
            }

            _mark = _lines.Count;
            var began = _context.TryBegin();
            _victimStealth.ResetAwareness();
            Retreat(world);
            if (began || _context.IsPerforming)
            {
                return $"an enemy that had noticed the player got a context move from behind (lines: {string.Join(" | ", Since(_mark))})";
            }

            var none = FindPrefixSince(_mark, $"{ContextMeleeLog.LogTag} none id={world.PlayerDamageable.Id} target={_victimBody.Id} ");
            var conditions = none != null ? Token(none, " conditions=").Split('|') : Array.Empty<string>();
            if (Array.IndexOf(conditions, nameof(ContextMeleeConditions.TargetBehind)) < 0
                || Array.IndexOf(conditions, nameof(ContextMeleeConditions.TargetUnaware)) >= 0)
            {
                return $"pressing melee behind an enemy that had noticed the player did not log 'none' with TargetBehind and without TargetUnaware (lines: {string.Join(" | ", Since(_mark))})";
            }

            _phase = Phase.Takedown;
            return null;
        }

        private string StartTakedown(BootstrapWorld world)
        {
            if (!IsWatching(_victim, _victimStealth, world) || _victim.IsInterrupted || !_melee.Swing.CanStart)
            {
                return null;
            }

            if (_sentryStealth.Stage != AwarenessStage.Unaware)
            {
                return $"the sentry was {_sentryStealth.Stage} before the takedown ({string.Join(" | ", _lines)})";
            }

            _sentryLevel = _sentryStealth.Level;
            Approach(world);
            _mark = _lines.Count;
            if (!_melee.TryMelee())
            {
                return $"pressing melee behind an unaware enemy did nothing (refusal {_melee.CheckSwing()})";
            }

            var started = ContextMeleeLog.Started(world.PlayerDamageable.Id, _victimBody.Id, ContextMeleeMove.TakedownId);
            if (!_context.IsPerforming || !SawSince(_mark, started))
            {
                return $"pressing melee behind an unaware enemy did not start the takedown (lines: {string.Join(" | ", Since(_mark))})";
            }

            _phase = Phase.WaitResolved;
            return null;
        }

        private string WaitResolved(BootstrapWorld world)
        {
            var problem = SentryProblem();
            if (problem != null || _context.IsPerforming)
            {
                return problem;
            }

            var resolved = ContextMeleeLog.ResolvedPrefix(world.PlayerDamageable.Id, _victimBody.Id, ContextMeleeMove.TakedownId);
            var line = FindPrefixSince(_mark, resolved);
            if (line == null)
            {
                return $"the takedown ended without a '{resolved}' line (lines: {string.Join(" | ", Since(_mark))})";
            }

            if (!line.EndsWith(" killed=true", StringComparison.Ordinal) || !_victimBody.IsDead)
            {
                return $"the takedown did not kill the enemy ('{line}', dead={_victimBody.IsDead})";
            }

            if (world.Mover.IsControlHandedOff)
            {
                return "the player's controls were not handed back after the takedown";
            }

            _resolvedAt = Time.time;
            _phase = Phase.Linger;
            return null;
        }

        // 倒した直後だけでなく、少し待っても見張りが気づかないままか。
        private string Linger(BootstrapWorld world)
        {
            var problem = SentryProblem();
            if (problem != null || Time.time - _resolvedAt < LingerSeconds)
            {
                return problem;
            }

            Sleep(_sentry);
            Retreat(world);
            _phase = Phase.Done;
            return null;
        }

        private string SentryProblem()
        {
            var witnessed = FindPrefixSince(_mark, ContextMeleeLog.WitnessedPrefix(_world.PlayerDamageable.Id));
            if (witnessed != null)
            {
                return $"an enemy saw a takedown that nobody was looking at ('{witnessed}')";
            }

            var sentryLine = FindPrefixSince(_mark, SentryLogPrefix);
            if (sentryLine != null || _sentryStealth.Stage != AwarenessStage.Unaware || _sentryStealth.Level > _sentryLevel + _levelTolerance)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "the nearby sentry became aware of the silent takedown (stage={0} level={1:0.###} before={2:0.###} line='{3}')",
                    _sentryStealth.Stage,
                    _sentryStealth.Level,
                    _sentryLevel,
                    sentryLine ?? "none");
            }

            return null;
        }

        private static bool IsWatching(EnemyAgent enemy, EnemyStealth stealth, BootstrapWorld world)
        {
            return stealth != null
                && stealth.Awareness != null
                && stealth.Rule == Stealth.On
                && enemy.Target == world.PlayerDamageable
                && ContextMeleeSelector.IsUnaware(enemy.State, stealth.Stage);
        }

        public static EnemyAgent FindEnemy(GameObject area, string id)
        {
            foreach (var enemy in area.GetComponentsInChildren<EnemyAgent>(true))
            {
                if (string.Equals(enemy.Id, id, StringComparison.Ordinal))
                {
                    return enemy;
                }
            }

            return null;
        }

        private static void Sleep(EnemyAgent enemy)
        {
            if (enemy != null)
            {
                enemy.gameObject.SetActive(false);
            }
        }

        private void Approach(BootstrapWorld world)
        {
            Teleport(world, StealthRangeFixture.TakedownStand);
            Face(world, _victim.transform.position);
        }

        // 囲いの外 (出現位置) へ戻し、待つ間に敵から見えないようにする。
        private static void Retreat(BootstrapWorld world)
        {
            Teleport(world, Vector3.zero);
        }

        private bool SawSince(int mark, string line)
        {
            for (var i = mark; i < _lines.Count; i++)
            {
                if (string.Equals(_lines[i], line, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private string FindPrefixSince(int mark, string prefix)
        {
            for (var i = mark; i < _lines.Count; i++)
            {
                if (_lines[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return _lines[i];
                }
            }

            return null;
        }

        private IEnumerable<string> Since(int mark)
        {
            for (var i = mark; i < _lines.Count; i++)
            {
                yield return _lines[i];
            }
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
