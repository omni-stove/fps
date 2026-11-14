using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Editor
{
    // 射撃場のルールは Melee を Off のまま配る (ほかの手順の前提を変えない) ので、Off で振れないことを確かめた後に、
    // この手順の間だけプレイヤーと敵を On にする。
    public sealed class MeleeSmokeStep : ITpsActionSmokeStep
    {
        public const float NearDistance = 1.5f;
        public const float FarDistance = 5f;

        private enum Phase
        {
            RefuseWhileOff,
            Near,
            Far,
            Behind,
            Enemy,
            Done,
        }

        private readonly List<string> _lines = new List<string>();
        private Phase _phase;
        private bool _placed;
        private bool _swung;
        private int _mark;
        private BootstrapWorld _world;
        private EnemyMelee _enemy;
        private DamageReport? _playerHit;

        public int Order => 500;

        public string Name => "melee";

        public float TimeoutSeconds => 30f;

        public void Reset()
        {
            _lines.Clear();
            _phase = Phase.RefuseWhileOff;
            _placed = false;
            _swung = false;
            _mark = 0;
            _world = null;
            _enemy = null;
            _playerHit = null;
        }

        public void OnLog(string condition)
        {
            if (condition != null && condition.StartsWith(MeleeLog.LogTag, System.StringComparison.Ordinal))
            {
                _lines.Add(condition);
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            _world = world;
            var melee = world.Player.GetComponent<MeleeController>();
            if (melee == null || !melee.IsInitialized)
            {
                problem = $"the player has no initialized MeleeController; is {nameof(MeleeActionInstaller)} registered? (rules: {PlayerActions.FormatLog(world.Rules.Actions)})";
                return false;
            }

            var playerId = world.PlayerDamageable.Id;
            switch (_phase)
            {
                case Phase.RefuseWhileOff:
                    problem = RefuseWhileOff(world, melee, playerId);
                    return false;
                case Phase.Near:
                    problem = SwingAt(world, melee, playerId, MeleeRangeFixture.DummyPosition + new Vector3(0f, 0f, -NearDistance), true, Phase.Far);
                    return false;
                case Phase.Far:
                    problem = SwingAt(world, melee, playerId, MeleeRangeFixture.DummyPosition + new Vector3(0f, 0f, -FarDistance), false, Phase.Behind);
                    return false;
                case Phase.Behind:
                    problem = SwingAt(world, melee, playerId, MeleeRangeFixture.DummyPosition + new Vector3(0f, 0f, NearDistance), false, Phase.Enemy);
                    return false;
                case Phase.Enemy:
                    problem = WaitForEnemy(world, playerId);
                    return false;
                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            if (_world != null)
            {
                var melee = _world.Player != null ? _world.Player.GetComponent<MeleeController>() : null;
                if (melee != null)
                {
                    melee.Configure(_world.Rules);
                }

                if (_world.PlayerDamageable != null)
                {
                    _world.PlayerDamageable.Damaged -= OnPlayerDamaged;
                }
            }

            // 残すと、この後の手順の間もプレイヤーを殴り続ける。
            if (_enemy != null)
            {
                _enemy.gameObject.SetActive(false);
            }
        }

        private string RefuseWhileOff(BootstrapWorld world, MeleeController melee, string playerId)
        {
            if (world.Rules.Actions.Melee != Melee.Off)
            {
                return $"the TPS range rules must keep melee Off so this step can check it; got {PlayerActions.FormatLog(world.Rules.Actions)}";
            }

            var mark = _lines.Count;
            if (melee.TrySwing())
            {
                return "the player swung while the melee rule was Off";
            }

            if (!SawSince(mark, MeleeLog.Refused(playerId, MeleeRefusal.Off)))
            {
                return $"refusing the swing logged no '{MeleeLog.Refused(playerId, MeleeRefusal.Off)}' line";
            }

            melee.Configure(WithMelee(world.Rules));
            _phase = Phase.Near;
            return null;
        }

        private string SwingAt(BootstrapWorld world, MeleeController melee, string playerId, Vector3 ground, bool expectHit, Phase next)
        {
            if (!_placed)
            {
                Teleport(world.Controller, ground + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f));
                FaceForward(world);
                _placed = true;
                return null;
            }

            if (!_swung)
            {
                // 移した直後は接地が前の位置のままで、前の振りの待ち時間も残っているので、両方が済むまで待つ。
                if (!world.Mover.IsGrounded || !melee.Swing.CanStart)
                {
                    return null;
                }

                _mark = _lines.Count;
                if (!melee.TrySwing())
                {
                    return $"the player could not swing at {Format(ground)} (refusal {melee.CheckSwing()})";
                }

                if (world.Weapon.PullTrigger(true, true))
                {
                    return "the weapon fired while swinging";
                }

                _swung = true;
                return null;
            }

            if (melee.IsSwinging)
            {
                return world.Weapon.PullTrigger(true, true) ? "the weapon fired while swinging" : null;
            }

            var hit = SawPrefixSince(_mark, MeleeLog.HitPrefix(playerId, MeleeRangeFixture.DummyId));
            var miss = SawSince(_mark, MeleeLog.Miss(playerId));
            if (expectHit && !hit)
            {
                return $"swinging {NearDistance.ToString("0.##", CultureInfo.InvariantCulture)}m in front of the dummy did not hit it (lines: {string.Join(" | ", Since(_mark))})";
            }

            if (!expectHit && (hit || !miss))
            {
                return $"swinging from {Format(ground)} should miss the dummy (lines: {string.Join(" | ", Since(_mark))})";
            }

            _placed = false;
            _swung = false;
            _phase = next;
            return null;
        }

        private string WaitForEnemy(BootstrapWorld world, string playerId)
        {
            if (_enemy == null)
            {
                var root = GameObject.Find(MeleeEnemyRangeFixture.RootName);
                var enemyTransform = root != null ? root.transform.Find(MeleeEnemyRangeFixture.EnemyName) : null;
                if (enemyTransform == null)
                {
                    return $"the scene has no '{MeleeEnemyRangeFixture.RootName}/{MeleeEnemyRangeFixture.EnemyName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
                }

                if (!enemyTransform.gameObject.activeInHierarchy)
                {
                    return $"'{MeleeEnemyRangeFixture.EnemyName}' was not activated; did {nameof(NavMeshBakeActivator)} bake its floor?";
                }

                var agent = enemyTransform.GetComponent<EnemyAgent>();
                var enemy = enemyTransform.GetComponent<EnemyMelee>();
                if (agent == null || enemy == null)
                {
                    return $"'{MeleeEnemyRangeFixture.EnemyName}' has no EnemyAgent or EnemyMelee";
                }

                if (!agent.IsInitialized)
                {
                    return null;
                }

                _enemy = enemy;
                _enemy.Configure(WithMelee(world.Rules));
                world.PlayerDamageable.Damaged += OnPlayerDamaged;
                _mark = _lines.Count;
                Teleport(world.Controller, MeleeEnemyRangeFixture.PlayerGround + new Vector3(0f, GameBootstrap.PlayerSpawn.y, 0f));
                return null;
            }

            if (!SawPrefixSince(_mark, MeleeLog.HitPrefix(MeleeEnemyRangeFixture.EnemyId, playerId)) || !_playerHit.HasValue)
            {
                return null;
            }

            var report = _playerHit.Value;
            if (report.Kind != DamageKind.Melee || !report.Direction.HasValue)
            {
                return $"the enemy's hit reached the player as kind={report.Kind} direction={(report.Direction.HasValue ? Format(report.Direction.Value) : "none")} (expected Melee with a direction)";
            }

            world.PlayerDamageable.Damaged -= OnPlayerDamaged;
            _enemy.gameObject.SetActive(false);
            _phase = Phase.Done;
            return null;
        }

        private void OnPlayerDamaged(Damageable _, DamageReport report)
        {
            if (report.Kind == DamageKind.Melee)
            {
                _playerHit = report;
            }
        }

        private static GameRules WithMelee(GameRules rules)
        {
            return rules.WithActions(rules.Actions.WithMelee(Melee.On));
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

        // FirstPersonLook が自分の角度を持っているので、Transform を直接回すと次の視点操作で戻る。
        private static void FaceForward(BootstrapWorld world)
        {
            var look = world.Look;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, 0f), 0f));
            }
            else
            {
                world.Player.transform.rotation = Quaternion.identity;
            }
        }

        private static string Format(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", v.x, v.y, v.z);
        }
    }
}
