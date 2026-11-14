using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    public sealed class StealthSmokeStep : ITpsActionSmokeStep
    {
        // ゲージが 1 フレームで気づきまで飛んでいないことを確かめる下限。遠くからしゃがんで近づけば、これより長くかかる。
        public const float MinNoticeSeconds = 0.25f;

        public const float MinLevelDrop = 0.02f;

        public const float MinInvestigateProgress = 1f;

        public const float EngageDistance = 6f;

        public const float OffCheckDistance = 8f;

        // Editor のフレームの間隔のばらつきを見込んだ「見えた瞬間」の上限。
        public const float MaxOffEngageSeconds = 1f;

        private readonly List<(AwarenessStage Stage, Stealth Rule)> _stages = new List<(AwarenessStage, Stealth)>();

        private Phase _phase;
        private double _startedAt;
        private double _offPlacedAt;
        private int _offIndex;
        private EnemyAgent _enemy;
        private EnemyStealth _stealth;
        private StealthTarget _target;
        private Stealth _originalRule;
        private PlayerController _mover;
        private ScriptedMoveFilter _filter;
        private Vector3 _alertSpot;
        private float _alertDistance;
        private float _peakLevel;
        private bool _sawDrop;
        private bool _sawInvestigate;

        private enum Phase
        {
            Setup,
            Approach,
            Hide,
            Engage,
            SwitchOff,
            OffSighted,
        }

        public int Order => 100;

        public string Name => "stealth";

        public float TimeoutSeconds => 60f;

        public void Reset()
        {
            _stages.Clear();
            _phase = Phase.Setup;
            _startedAt = -1d;
            _offPlacedAt = -1d;
            _offIndex = 0;
            _enemy = null;
            _stealth = null;
            _target = null;
            _mover = null;
            _filter = null;
            _alertSpot = Vector3.zero;
            _alertDistance = 0f;
            _peakLevel = 0f;
            _sawDrop = false;
            _sawInvestigate = false;
        }

        public void OnLog(string condition)
        {
            var prefix = EnemyStealth.StageLogPrefix(StealthRangeFixture.EnemyId);
            if (condition == null || !condition.StartsWith(prefix, StringComparison.Ordinal))
            {
                return;
            }

            if (Enum.TryParse(Token(condition, prefix), out AwarenessStage stage)
                && Enum.TryParse(Token(condition, " rule="), out Stealth rule))
            {
                _stages.Add((stage, rule));
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            var now = EditorApplication.timeSinceStartup;
            switch (_phase)
            {
                case Phase.Setup:
                    problem = Setup(world, now);
                    return false;
                case Phase.Approach:
                    problem = Approach(world);
                    return false;
                case Phase.Hide:
                    problem = Hide(world);
                    return false;
                case Phase.Engage:
                    problem = Engage(world);
                    return false;
                case Phase.SwitchOff:
                    problem = SwitchOff(world, now);
                    return false;
                default:
                    return OffSighted(now, out problem);
            }
        }

        public void Cleanup()
        {
            if (_mover != null && _filter != null && ReferenceEquals(_mover.MoveFilter, _filter))
            {
                _mover.MoveFilter = _filter.Inner;
            }

            if (_target != null)
            {
                _target.Rule = _originalRule;
            }

            // 終わった後も撃たれ続けないよう、敵を止める。
            if (_enemy != null)
            {
                _enemy.gameObject.SetActive(false);
            }

            _filter = null;
        }

        private string Setup(BootstrapWorld world, double now)
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
            if (_stealth == null || _stealth.Awareness == null)
            {
                return $"'{StealthRangeFixture.EnemyName}' has no initialized EnemyStealth";
            }

            _target = world.Player.GetComponent<StealthTarget>();
            if (_target == null || _target.Rule != Stealth.On)
            {
                return $"the player has no StealthTarget with stealth On; the TPS range rules must turn stealth on (rerun {GameRulesAssetBuilder.MenuPath}; got {PlayerActions.FormatLog(world.Rules.Actions)})";
            }

            _originalRule = _target.Rule;

            // 目標は EnemyAgent が Player タグから探すので、見つかるまで待つ。
            if (_enemy.Target != world.PlayerDamageable)
            {
                return null;
            }

            _mover = world.Mover;
            _filter = new ScriptedMoveFilter(_mover.MoveFilter) { Move = Vector2.up, Crouch = true };
            _mover.MoveFilter = _filter;

            _enemy.ResetTo(StealthRangeFixture.EnemyFeet, StealthRangeFixture.EnemyFacing);
            _stealth.ResetAwareness();
            Teleport(world.Controller, StealthRangeFixture.ApproachStart + (Vector3.up * GameBootstrap.PlayerSpawn.y));
            FaceTowards(world, StealthRangeFixture.EnemyFeet);

            _stages.Clear();
            _startedAt = now;
            _phase = Phase.Approach;
            return null;
        }

        private string Approach(BootstrapWorld world)
        {
            var noticed = IndexOf(AwarenessStage.Noticed, 0);
            var alerted = IndexOf(AwarenessStage.Alerted, 0);
            if (IndexOf(AwarenessStage.Engaged, 0) >= 0)
            {
                return $"the enemy engaged while the player sneaked up ({Describe()})";
            }

            if (noticed >= 0 && _startedAt >= 0d)
            {
                var seconds = EditorApplication.timeSinceStartup - _startedAt;
                if (seconds < MinNoticeSeconds)
                {
                    return $"the enemy noticed the crouching player after only {seconds.ToString("0.###", CultureInfo.InvariantCulture)}s; the meter should fill slowly from afar";
                }

                _startedAt = -1d;
            }

            if (alerted < 0)
            {
                return null;
            }

            if (noticed < 0 || noticed > alerted)
            {
                return $"the enemy became alerted without noticing first ({Describe()})";
            }

            if (!world.Mover.IsCrouched)
            {
                return "the player was not crouching while sneaking up";
            }

            var lastSeen = _stealth.Awareness.LastSeen;
            if (!lastSeen.HasValue)
            {
                return "the alerted enemy has no last seen position";
            }

            _alertSpot = lastSeen.Value.ToUnity();
            _alertDistance = HorizontalDistance(_enemy.GroundPosition, _alertSpot);
            _peakLevel = _stealth.Level;

            // 囲いの外 (出現位置) へ移して、確実に見えなくする。
            _filter.Move = Vector2.zero;
            Teleport(world.Controller, GameBootstrap.PlayerSpawn);
            _phase = Phase.Hide;
            return null;
        }

        private string Hide(BootstrapWorld world)
        {
            if (IndexOf(AwarenessStage.Engaged, 0) >= 0)
            {
                return $"the enemy engaged while the player was out of sight ({Describe()})";
            }

            _peakLevel = Mathf.Max(_peakLevel, _stealth.Level);
            if (_stealth.Level <= _peakLevel - MinLevelDrop)
            {
                _sawDrop = true;
            }

            var remaining = HorizontalDistance(_enemy.GroundPosition, _alertSpot);
            if (_stealth.Stage == AwarenessStage.Alerted
                && (_alertDistance - remaining >= MinInvestigateProgress || remaining <= EnemyBrain.ArrivalDistance + 0.5f))
            {
                _sawInvestigate = true;
            }

            if (!_sawDrop || !_sawInvestigate)
            {
                return null;
            }

            var forward = _enemy.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : StealthRangeFixture.EnemyFacing * Vector3.forward;
            var feet = ClampToArea(_enemy.GroundPosition + (forward * EngageDistance));
            _filter.Crouch = false;
            Teleport(world.Controller, feet + (Vector3.up * GameBootstrap.PlayerSpawn.y));
            FaceTowards(world, _enemy.GroundPosition);
            _phase = Phase.Engage;
            return null;
        }

        private string Engage(BootstrapWorld world)
        {
            var noticed = IndexOf(AwarenessStage.Noticed, 0);
            var alerted = IndexOf(AwarenessStage.Alerted, 0);
            var engaged = IndexOf(AwarenessStage.Engaged, 0);
            if (engaged < 0)
            {
                return null;
            }

            if (!(noticed >= 0 && noticed < alerted && alerted < engaged))
            {
                return $"the stages were not logged as noticed, alerted, then engaged ({Describe()})";
            }

            if (_stages[engaged].Rule != Stealth.On)
            {
                return $"the enemy engaged under stealth {_stages[engaged].Rule}";
            }

            _target.Rule = Stealth.Off;
            Teleport(world.Controller, GameBootstrap.PlayerSpawn);
            _enemy.ResetTo(StealthRangeFixture.EnemyFeet, StealthRangeFixture.EnemyFacing);
            _stealth.ResetAwareness();
            _phase = Phase.SwitchOff;
            return null;
        }

        // EnemyStealth は相手のルールを自分の Update で読むので、Off が効いてから見せる。
        private string SwitchOff(BootstrapWorld world, double now)
        {
            if (_stealth.Rule != Stealth.Off || StealthAwareness.IsFighting(_enemy.State))
            {
                return null;
            }

            var feet = ClampToArea(StealthRangeFixture.EnemyFeet + (StealthRangeFixture.EnemyFacing * Vector3.forward * OffCheckDistance));
            Teleport(world.Controller, feet + (Vector3.up * GameBootstrap.PlayerSpawn.y));
            FaceTowards(world, StealthRangeFixture.EnemyFeet);
            _offIndex = _stages.Count;
            _offPlacedAt = now;
            _phase = Phase.OffSighted;
            return null;
        }

        private bool OffSighted(double now, out string problem)
        {
            problem = null;
            for (var i = _offIndex; i < _stages.Count; i++)
            {
                var (stage, rule) = _stages[i];
                if (stage == AwarenessStage.Noticed || stage == AwarenessStage.Alerted)
                {
                    problem = $"with stealth off the enemy went through {stage} ({Describe()})";
                    return false;
                }

                if (stage == AwarenessStage.Engaged)
                {
                    if (rule != Stealth.Off)
                    {
                        problem = $"the enemy engaged under stealth {rule} after the rule was turned off";
                        return false;
                    }

                    return true;
                }
            }

            if (now - _offPlacedAt > MaxOffEngageSeconds)
            {
                problem = $"with stealth off the enemy did not engage within {MaxOffEngageSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s of seeing the player ({Describe()})";
            }

            return false;
        }

        private int IndexOf(AwarenessStage stage, int from)
        {
            for (var i = from; i < _stages.Count; i++)
            {
                if (_stages[i].Stage == stage)
                {
                    return i;
                }
            }

            return -1;
        }

        private string Describe()
        {
            var parts = new List<string>(_stages.Count);
            foreach (var (stage, rule) in _stages)
            {
                parts.Add($"{stage}/{rule}");
            }

            var level = _stealth != null ? _stealth.Level.ToString("0.00", CultureInfo.InvariantCulture) : "?";
            return $"stages {string.Join(" > ", parts)}, level {level}";
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

        private static Vector3 ClampToArea(Vector3 feet)
        {
            var half = StealthRangeFixture.AreaSize / 2f;
            var center = StealthRangeFixture.AreaCenter;
            return new Vector3(
                Mathf.Clamp(feet.x, center.x - half.x + 1f, center.x + half.x - 1f),
                0f,
                Mathf.Clamp(feet.z, center.z - half.z + 1f, center.z + half.z - 1f));
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
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, yaw), 0f));
            }
            else
            {
                world.Player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        // 入力の代わりに、決めた移動としゃがみを押し続ける。先に差し込まれていたもの (カバー) は後から呼び、その制限を上書きしない。
        private sealed class ScriptedMoveFilter : IPlayerMoveFilter
        {
            public ScriptedMoveFilter(IPlayerMoveFilter inner)
            {
                Inner = inner;
            }

            public IPlayerMoveFilter Inner { get; }

            public Vector2 Move { get; set; }

            public bool Crouch { get; set; }

            public void Filter(ref Vector2 move, ref bool sprint, ref bool crouch, ref bool jumpPressed)
            {
                move = Move;
                sprint = false;
                crouch = Crouch;
                jumpPressed = false;
                Inner?.Filter(ref move, ref sprint, ref crouch, ref jumpPressed);
            }
        }
    }
}
