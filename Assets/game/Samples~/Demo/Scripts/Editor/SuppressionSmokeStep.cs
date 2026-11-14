using System;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Editor
{
    // 狙いは入力で合わせられないので、弾の始点と向きを WeaponController.ShotFilter の差し込みで決める。
    // プレイヤーへの制圧は、Bot (EnemyAgent) と同じ Hitscan.Fire で撃って確かめる (射撃場に Bot を置けないため。SuppressionRangeFixture を参照)。
    public sealed class SuppressionSmokeStep : ITpsActionSmokeStep
    {
        // 的の半径 (0.5) より外を通して当てず、ニアミスの半径 (既定 1.5) より内を通す。
        public const float TargetMissOffset = 0.8f;

        public const float PlayerMissOffset = GameBootstrap.PlayerRadius + 0.4f;

        public const float ShotDistance = 8f;

        public const int MaxTargetShots = 60;

        public const int MaxBotShots = 5;

        // 撃つ線の後ろに置き、自分の射撃の線にも Bot の射撃の線にも重ならないようにする。
        public static Vector3 PlayerStand => new Vector3(
            SuppressionRangeFixture.TargetGround.x, GameBootstrap.PlayerSpawn.y, SuppressionRangeFixture.TargetGround.z - ShotDistance - 2f);

        private Phase _phase;
        private string _playerId;
        private bool _sawTargetNearMiss;
        private bool _sawTargetSuppressed;
        private bool _sawTargetReleased;
        private bool _sawTargetCleared;
        private bool _sawPlayerNearMiss;
        private float _targetPeak;
        private int _targetShots;
        private int _botShots;
        private float _baseCone;
        private float _baseSpread;
        private bool _placed;
        private WeaponController _weapon;
        private AimFilter _filter;
        private SuppressionReceiver _player;

        private enum Phase
        {
            SuppressTarget,
            WaitTargetClear,
            SuppressPlayer,
            Done,
        }

        public int Order => 0;

        public string Name => "suppression";

        public float TimeoutSeconds => 30f;

        private static string TargetPrefix => $"{SuppressionLog.LogTag} id={SuppressionRangeFixture.TargetName} ";

        public void Reset()
        {
            _phase = Phase.SuppressTarget;
            _playerId = null;
            _sawTargetNearMiss = false;
            _sawTargetSuppressed = false;
            _sawTargetReleased = false;
            _sawTargetCleared = false;
            _sawPlayerNearMiss = false;
            _targetPeak = 0f;
            _targetShots = 0;
            _botShots = 0;
            _baseCone = 0f;
            _baseSpread = 0f;
            _placed = false;
            _weapon = null;
            _filter = null;
            _player = null;
        }

        public void OnLog(string condition)
        {
            if (condition.StartsWith(TargetPrefix, StringComparison.Ordinal))
            {
                var rest = condition.Substring(TargetPrefix.Length);
                _sawTargetNearMiss |= rest.StartsWith("near-miss ", StringComparison.Ordinal);
                _sawTargetSuppressed |= rest.StartsWith("suppressed=true", StringComparison.Ordinal);
                _sawTargetReleased |= rest.StartsWith("suppressed=false", StringComparison.Ordinal);
                _sawTargetCleared |= rest == "cleared";
                return;
            }

            if (_playerId != null && condition.StartsWith($"{SuppressionLog.LogTag} id={_playerId} near-miss ", StringComparison.Ordinal))
            {
                _sawPlayerNearMiss = true;
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            var targetObject = GameObject.Find(SuppressionRangeFixture.TargetName);
            var target = targetObject != null ? targetObject.GetComponent<SuppressionReceiver>() : null;
            if (target == null)
            {
                problem = $"the scene has no '{SuppressionRangeFixture.TargetName}' with a SuppressionReceiver; rerun {TpsRangeSceneBuilder.MenuPath}";
                return false;
            }

            if (!target.IsActive)
            {
                problem = $"'{SuppressionRangeFixture.TargetName}' does not take suppression (rule {target.Rule}); the TPS range rules must turn it on ({PlayerActions.FormatLog(world.Rules.Actions)})";
                return false;
            }

            _player = world.Player.GetComponent<SuppressionReceiver>();
            if (_player == null || !_player.IsActive || world.Player.GetComponent<PlayerSuppression>() == null)
            {
                problem = "the player has no active SuppressionReceiver and PlayerSuppression; SuppressionActionInstaller did not run with the rule on";
                return false;
            }

            _playerId = _player.Id;
            _weapon = world.Weapon;
            switch (_phase)
            {
                case Phase.SuppressTarget:
                    problem = SuppressTarget(world, target);
                    return false;
                case Phase.WaitTargetClear:
                    problem = WaitTargetClear(target);
                    return false;
                case Phase.SuppressPlayer:
                    problem = SuppressPlayer(world, targetObject.transform);
                    return _phase == Phase.Done;
                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            RemoveFilter();
            if (_player != null)
            {
                _player.ResetLevel();
            }
        }

        private string SuppressTarget(BootstrapWorld world, SuppressionReceiver target)
        {
            if (!_placed)
            {
                Teleport(world.Controller, PlayerStand);
                _placed = true;
            }

            if (_filter == null)
            {
                // 的の右を +Z へ抜ける線。的の中心と同じ高さで、的の手前 ShotDistance m から撃つ。
                var center = SuppressionRangeFixture.TargetCenter;
                var origin = center + (Vector3.right * TargetMissOffset) - (Vector3.forward * ShotDistance);
                _filter = new AimFilter(_weapon.ShotFilter, origin, Vector3.forward);
                _weapon.ShotFilter = _filter;
            }

            _targetPeak = Mathf.Max(_targetPeak, target.Level);
            if (target.IsSuppressed)
            {
                if (!_sawTargetNearMiss || !_sawTargetSuppressed)
                {
                    return $"the target was suppressed without logging near-miss and suppressed=true lines (near-miss={_sawTargetNearMiss} suppressed={_sawTargetSuppressed})";
                }

                RemoveFilter();
                _phase = Phase.WaitTargetClear;
                return null;
            }

            if (_targetShots >= MaxTargetShots)
            {
                return $"{MaxTargetShots.ToString(CultureInfo.InvariantCulture)} shots {TargetMissOffset.ToString("0.##", CultureInfo.InvariantCulture)}m beside the target did not suppress it (level {target.Level.ToString("0.##", CultureInfo.InvariantCulture)})";
            }

            var weapon = _weapon.Weapon;
            if (weapon == null)
            {
                return "the player has no weapon to fire";
            }

            if (weapon.IsReloading)
            {
                return null;
            }

            if (weapon.AmmoInMagazine == 0)
            {
                _weapon.StartReload();
                return null;
            }

            if (_weapon.PullTrigger(true, true))
            {
                _targetShots++;
            }

            return null;
        }

        private string WaitTargetClear(SuppressionReceiver target)
        {
            if (target.Level > _targetPeak)
            {
                return $"the target's level kept rising after the player stopped firing ({target.Level.ToString("0.##", CultureInfo.InvariantCulture)})";
            }

            if (target.Level > 0f)
            {
                return null;
            }

            if (target.IsSuppressed || !_sawTargetReleased || !_sawTargetCleared)
            {
                return $"the target's level drained without releasing it (suppressed={target.IsSuppressed} released log={_sawTargetReleased} cleared log={_sawTargetCleared})";
            }

            _phase = Phase.SuppressPlayer;
            return null;
        }

        private string SuppressPlayer(BootstrapWorld world, Transform bot)
        {
            var spec = _weapon.Weapon != null ? _weapon.Weapon.Spec : null;
            if (spec == null)
            {
                return "the player has no weapon to read the spread of";
            }

            if (_botShots == 0)
            {
                _baseCone = _weapon.CurrentConeDegrees(spec);
                _baseSpread = _weapon.SpreadFraction;
            }

            if (_player.Level > 0f && _sawPlayerNearMiss)
            {
                var cone = _weapon.CurrentConeDegrees(spec);
                var spread = _weapon.SpreadFraction;
                if (!(cone > _baseCone) || !(spread > _baseSpread))
                {
                    return $"being suppressed (level {_player.Level.ToString("0.##", CultureInfo.InvariantCulture)}) did not widen the player's aim (cone {_baseCone.ToString("0.##", CultureInfo.InvariantCulture)} -> {cone.ToString("0.##", CultureInfo.InvariantCulture)}, crosshair {_baseSpread.ToString("0.##", CultureInfo.InvariantCulture)} -> {spread.ToString("0.##", CultureInfo.InvariantCulture)})";
                }

                _phase = Phase.Done;
                return null;
            }

            if (_botShots >= MaxBotShots)
            {
                return $"{MaxBotShots.ToString(CultureInfo.InvariantCulture)} bot shots {PlayerMissOffset.ToString("0.##", CultureInfo.InvariantCulture)}m beside the player did not suppress them (level {_player.Level.ToString("0.##", CultureInfo.InvariantCulture)}, near-miss log={_sawPlayerNearMiss})";
            }

            // Bot (EnemyAgent) の射撃と同じ Hitscan.Fire で、的から撃ったことにしてプレイヤーの奥を +X へ抜ける。
            var damageable = world.Player.GetComponent<Damageable>();
            var center = damageable != null ? damageable.AimPoint : world.Player.transform.position;
            var origin = center + (Vector3.forward * PlayerMissOffset) - (Vector3.right * ShotDistance);
            Hitscan.Fire(origin, Vector3.right, spec.Range, 0, spec.Falloff, bot, 0f, spec.Id);
            _botShots++;
            return null;
        }

        private void RemoveFilter()
        {
            if (_filter == null)
            {
                return;
            }

            if (_weapon != null && ReferenceEquals(_weapon.ShotFilter, _filter))
            {
                _weapon.ShotFilter = _filter.Inner;
            }

            _filter = null;
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

        // 先に差し込まれていたもの (ブラインドファイア) を外さずに残し、smoke の後に戻せるようにする。
        private sealed class AimFilter : IShotFilter
        {
            private readonly Vector3 _origin;
            private readonly Vector3 _direction;

            public AimFilter(IShotFilter inner, Vector3 origin, Vector3 direction)
            {
                Inner = inner;
                _origin = origin;
                _direction = direction.normalized;
            }

            public IShotFilter Inner { get; }

            public void Filter(ref Vector3 origin, ref Vector3 direction)
            {
                Inner?.Filter(ref origin, ref direction);
                origin = _origin;
                direction = _direction;
            }
        }
    }
}
