using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Editor
{
    // 怯みの手順 (StaggerSmokeStep) の敵を使い、怯んでいない敵にはふつうの近接、脚を撃って怯ませた敵には処刑技が出ることを確かめる。
    // batchmode には入力が無いので、近接の入力の代わりに MeleeController.TryMelee を、脚を撃つ代わりに脚の当たり判定への直接の当たりを使う。
    // 射撃場のルールは Melee を Off のまま配る (ほかの手順の前提を変えない) ので、この手順の間だけ On にする。
    // 敵は撃ってくるので、確かめる瞬間だけ近くへ移し、待つ間は敵の視界の外へ戻す。
    public sealed class ContextMeleeSmokeStep : ITpsActionSmokeStep
    {
        public const int LegShotDamage = StaggerSmokeStep.LegShotDamage;
        public const int ShotDamage = 1;
        public const int PlayerWound = 40;

        // 体の中心どうしの距離。敵の体の表面まで近接の届く距離 (MeleeSpec.DefaultReach) の内側に入る。
        public const float NearDistance = 1.6f;

        public const float RetrySeconds = 0.3f;

        private enum Phase
        {
            Setup,
            PlainMelee,
            WaitCalm,
            Interrupt,
            WaitInterrupted,
            WaitCalmAgain,
            Finisher,
            WaitResolved,
            Done,
        }

        private readonly List<string> _lines = new List<string>();
        private Phase _phase;
        private BootstrapWorld _world;
        private MeleeController _melee;
        private ContextMeleeAction _context;
        private EnemyAgent _enemy;
        private Damageable _enemyBody;
        private StaggerReaction _reaction;
        private Hitbox _legs;
        private int _mark;
        private float _retryAt;
        private int _expectedAmmo;

        public int Order => 510;

        public string Name => "context-melee";

        public float TimeoutSeconds => 40f;

        public void Reset()
        {
            _lines.Clear();
            _phase = Phase.Setup;
            _world = null;
            _melee = null;
            _context = null;
            _enemy = null;
            _enemyBody = null;
            _reaction = null;
            _legs = null;
            _mark = 0;
            _retryAt = 0f;
            _expectedAmmo = 0;
        }

        public void OnLog(string condition)
        {
            if (condition != null
                && (condition.StartsWith(ContextMeleeLog.LogTag, StringComparison.Ordinal) || condition.StartsWith(MeleeLog.LogTag, StringComparison.Ordinal)))
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
                case Phase.PlainMelee:
                    problem = PlainMelee(world);
                    return false;
                case Phase.WaitCalm:
                    WaitCalm(world, Phase.Interrupt);
                    return false;
                case Phase.Interrupt:
                    problem = StartFinisher(world, Phase.WaitInterrupted);
                    return false;
                case Phase.WaitInterrupted:
                    problem = ShootDuringFinisher(world);
                    return false;
                case Phase.WaitCalmAgain:
                    WaitCalm(world, Phase.Finisher);
                    return false;
                case Phase.Finisher:
                    problem = StartFinisher(world, Phase.WaitResolved);
                    return false;
                case Phase.WaitResolved:
                    problem = WaitResolved(world);
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
        }

        private string Setup(BootstrapWorld world)
        {
            if (world.Rules.Actions.Melee != Melee.Off)
            {
                return $"the TPS range rules must keep melee Off so this step can turn it on alone; got {PlayerActions.FormatLog(world.Rules.Actions)}";
            }

            if (StaggerReaction.ActiveRule != Stagger.On)
            {
                return $"the stagger rule did not reach the enemies (StaggerReaction.ActiveRule={StaggerReaction.ActiveRule}); rerun {GameRulesAssetBuilder.MenuPath}";
            }

            _melee = world.Player.GetComponent<MeleeController>();
            _context = world.Player.GetComponent<ContextMeleeAction>();
            if (_melee == null || !_melee.IsInitialized || _context == null)
            {
                return $"the player has no initialized MeleeController with a ContextMeleeAction; are {nameof(MeleeActionInstaller)} and {nameof(ContextMeleeActionInstaller)} registered?";
            }

            var root = GameObject.Find(StaggerRangeFixture.FixtureName);
            _enemy = root != null ? root.GetComponentInChildren<EnemyAgent>(true) : null;
            _enemyBody = _enemy != null ? _enemy.GetComponent<Damageable>() : null;
            _reaction = _enemy != null ? _enemy.GetComponent<StaggerReaction>() : null;
            var legs = _enemy != null ? _enemy.transform.Find(StaggerRangeFixture.LegsName) : null;
            _legs = legs != null ? legs.GetComponent<Hitbox>() : null;
            if (_enemy == null || _enemyBody == null || _reaction == null || _legs == null)
            {
                return $"the scene has no '{StaggerRangeFixture.FixtureName}' enemy with a StaggerReaction and a '{StaggerRangeFixture.LegsName}' hitbox; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            if (!_enemy.gameObject.activeInHierarchy || !_enemy.IsInitialized || !_reaction.IsBound || _enemyBody.IsDead)
            {
                return $"the stagger enemy is not running (active={_enemy.gameObject.activeInHierarchy} initialized={_enemy.IsInitialized} bound={_reaction.IsBound} dead={_enemyBody.IsDead})";
            }

            var rules = world.Rules.WithActions(world.Rules.Actions.WithMelee(Melee.On));
            _melee.Configure(rules);
            _context.Configure(rules);
            if (!_context.enabled || _context.Selector.Find(ContextMeleeMove.FinisherId) == null)
            {
                return $"turning melee on did not enable the context melee with the finisher (enabled={_context.enabled} moves={_context.Selector.Moves.Count})";
            }

            Retreat(world);
            _phase = Phase.PlainMelee;
            return null;
        }

        // 怯んでいない敵に近接を押すと、技は選ばれずふつうの近接が出る。
        private string PlainMelee(BootstrapWorld world)
        {
            if (!IsCalm())
            {
                return null;
            }

            Approach(world);
            _mark = _lines.Count;
            if (!_melee.TryMelee())
            {
                return $"pressing melee next to a calm enemy did nothing (refusal {_melee.CheckSwing()})";
            }

            if (_context.IsPerforming || SawPrefixSince(_mark, ContextMeleeLog.LogTag + " started "))
            {
                return $"a calm enemy got a context move instead of a plain melee (lines: {string.Join(" | ", Since(_mark))})";
            }

            var playerId = world.PlayerDamageable.Id;
            if (!SawPrefixSince(_mark, ContextMeleeLog.LogTag + " none id=" + playerId + " target=" + _enemyBody.Id + " ") || !SawSince(_mark, MeleeLog.Swing(playerId)))
            {
                return $"pressing melee next to a calm enemy logged no 'none' line followed by a plain swing (lines: {string.Join(" | ", Since(_mark))})";
            }

            _phase = Phase.WaitCalm;
            return null;
        }

        private void WaitCalm(BootstrapWorld world, Phase next)
        {
            if (_melee.IsSwinging)
            {
                return;
            }

            Retreat(world);
            if (IsCalm() && _melee.Swing.CanStart)
            {
                _retryAt = 0f;
                _phase = next;
            }
        }

        // 脚を撃って怯ませ、同じフレームで近接を押す。怯みの直後の効かない間なら、離れてから撃ち直す。
        private string StartFinisher(BootstrapWorld world, Phase next)
        {
            if (Time.time < _retryAt)
            {
                return null;
            }

            Approach(world);
            var shooter = world.Player.transform.position;
            _legs.ApplyHit(LegShotDamage, Vector3.Distance(shooter, _legs.transform.position), DamageFalloff.None, shooter, _legs.transform.position - shooter);
            if (!_reaction.IsStaggered)
            {
                Retreat(world);
                _retryAt = Time.time + RetrySeconds;
                return null;
            }

            if (next == Phase.WaitResolved)
            {
                // 回復した量を見分けられるよう、先に体力を減らしておく。予備弾は満タンなら増えないので、増やせる分を覚えておく。
                // 敵に撃たれて減っていても倒れないよう、1 は残す。
                var wound = Math.Min(PlayerWound, world.PlayerDamageable.Health.Current - 1);
                if (wound > 0)
                {
                    world.PlayerDamageable.TakeDamage(wound);
                }

                var weapon = world.Weapon.Weapon;
                _expectedAmmo = weapon != null ? Math.Min(_melee.Spec.FinisherAmmo, weapon.ReserveAmmoRoom) : 0;
            }

            _mark = _lines.Count;
            if (!_melee.TryMelee())
            {
                return $"pressing melee next to a staggered enemy did nothing (refusal {_melee.CheckSwing()} staggered={_reaction.Current})";
            }

            var playerId = world.PlayerDamageable.Id;
            if (!_context.IsPerforming || !SawSince(_mark, ContextMeleeLog.Started(playerId, _enemyBody.Id, ContextMeleeMove.FinisherId)))
            {
                return $"pressing melee next to a staggered enemy did not start the finisher (lines: {string.Join(" | ", Since(_mark))})";
            }

            if (_melee.IsSwinging)
            {
                return "the plain melee swung as well as the finisher";
            }

            if (!world.Mover.IsControlHandedOff || world.Weapon.PullTrigger(true, true))
            {
                return $"the finisher did not hold the player's controls (handedOff={world.Mover.IsControlHandedOff})";
            }

            if (!_enemy.IsInterrupted)
            {
                return "the finisher did not hold the enemy";
            }

            _phase = next;
            return null;
        }

        // 技の最中にほかから撃たれると、中断できる設定 (MeleeSpec.FinisherInterruptible の既定) なので止まる。
        private string ShootDuringFinisher(BootstrapWorld world)
        {
            var playerId = world.PlayerDamageable.Id;
            if (_context.IsPerforming)
            {
                if (!_melee.Spec.FinisherInterruptible)
                {
                    return "the player's finisher is not interruptible; this step checks the default (interruptible)";
                }

                // 撃たれて倒れると「撃たれた」でなく「倒れた」で止まるので、当てても倒れない体力にしておく。
                var health = world.PlayerDamageable.Health;
                if (health.Current <= ShotDamage)
                {
                    health.Heal(ShotDamage);
                }

                _mark = _lines.Count;
                var source = world.Player.transform.position - world.Player.transform.forward;
                world.PlayerDamageable.TakeDamage(ShotDamage, source, world.Player.transform.position - source, DamageKind.Bullet);
                if (_context.IsPerforming)
                {
                    return "being shot during the finisher did not interrupt it";
                }
            }

            var interrupted = ContextMeleeLog.Interrupted(playerId, _enemyBody.Id, ContextMeleeMove.FinisherId, ContextMeleeInterruption.Shot);
            if (!SawSince(_mark, interrupted))
            {
                return $"the finisher ended without '{interrupted}' (lines: {string.Join(" | ", Since(_mark))})";
            }

            if (_enemyBody.IsDead || world.Mover.IsControlHandedOff || _enemy.InterruptReason == ContextMeleeAction.InterruptReasonPrefix + ContextMeleeMove.FinisherId)
            {
                return $"the interrupted finisher left things held (enemyDead={_enemyBody.IsDead} handedOff={world.Mover.IsControlHandedOff} enemyHold={_enemy.InterruptReason})";
            }

            Retreat(world);
            _phase = Phase.WaitCalmAgain;
            return null;
        }

        private string WaitResolved(BootstrapWorld world)
        {
            var playerId = world.PlayerDamageable.Id;
            if (_context.IsPerforming)
            {
                return null;
            }

            var resolved = ContextMeleeLog.ResolvedPrefix(playerId, _enemyBody.Id, ContextMeleeMove.FinisherId);
            var line = FindPrefixSince(_mark, resolved);
            if (line == null)
            {
                return $"the finisher ended without a '{resolved}' line (lines: {string.Join(" | ", Since(_mark))})";
            }

            if (!line.EndsWith(" killed=true", StringComparison.Ordinal) || !_enemyBody.IsDead)
            {
                return $"the finisher did not kill the enemy ('{line}', dead={_enemyBody.IsDead})";
            }

            var reward = FindPrefixSince(_mark, ContextMeleeLog.RewardPrefix(playerId, ContextMeleeMove.FinisherId));
            var expected = ContextMeleeLog.Reward(playerId, ContextMeleeMove.FinisherId, 0, _expectedAmmo);
            var ammoSuffix = expected.Substring(expected.LastIndexOf(" ammo=", StringComparison.Ordinal));
            if (reward == null || reward.Contains(" health=0 ") || !reward.EndsWith(ammoSuffix, StringComparison.Ordinal))
            {
                return $"the finisher's kill gave no health or the wrong ammo (expected ammo={_expectedAmmo}, got '{reward ?? "no reward line"}')";
            }

            if (world.Mover.IsControlHandedOff)
            {
                return "the player's controls were not handed back after the finisher";
            }

            Retreat(world);
            _phase = Phase.Done;
            return null;
        }

        private bool IsCalm()
        {
            return !_reaction.IsStaggered && !_enemy.IsInterrupted;
        }

        private void Approach(BootstrapWorld world)
        {
            var feet = StaggerRangeFixture.EnemyFeet;
            var stand = new Vector3(feet.x, feet.y, feet.z - NearDistance);
            Teleport(world, stand);
            Face(world, _enemy.transform.position);
        }

        // 敵の視界 (StaggerRangeFixture.EnemySightRange) の外へ戻し、待つ間に撃たれないようにする。
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

        private bool SawPrefixSince(int mark, string prefix)
        {
            return FindPrefixSince(mark, prefix) != null;
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
