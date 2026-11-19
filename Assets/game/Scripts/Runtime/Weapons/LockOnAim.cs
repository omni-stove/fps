using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Shooter.Runtime
{
    public sealed class LockOnAim : MonoBehaviour
    {
        public const string LogTag = "[Combat]";

        private static readonly List<LockOnAim> _active = new List<LockOnAim>();

        private readonly LockOnTracker _tracker = new LockOnTracker();
        private readonly List<LockOnCandidate> _candidates = new List<LockOnCandidate>();
        private AudioSource _audio;
        private int? _loggedTarget;
        private bool _loggedLocked;

        public Func<RoundSpec> RoundSource { get; set; }

        // 音は WeaponDefinition の側にしか無く RoundSpec に載らないので、弾の値とは別に引く。
        public Func<HomingSettings> LookSource { get; set; }

        public Func<bool> AimingSource { get; set; }

        public Func<Ray> AimSource { get; set; }

        public ILockOnTargets Targets { get; set; }

        // 敵の捕捉 (LockOnTurret) が、この画面のプレイヤーの照準の捕捉表示を書き換えないよう切れるようにする。
        public bool PublishesToHud { get; set; } = true;

        // Targets の Id は集め方ごとの番号で場の物を指さないので、狙われた側が自分かどうかを確かめるには Id を体に引き直す関数が要る。
        public Func<int, Damageable> TargetLookup { get; set; }

        // 狙われた側が毎フレーム場を探し直さずに済むよう、有効な LockOnAim をここに集めておく。
        public static IReadOnlyList<LockOnAim> Active => _active;

        public LockOnTracker Tracker => _tracker;

        public float? Progress => _tracker.TargetId.HasValue ? _tracker.Progress : (float?)null;

        public int? TargetId => _tracker.TargetId;

        public int? LockedTargetId => _tracker.LockedTargetId;

        public LockOnWarningStage Stage =>
            !_tracker.TargetId.HasValue ? LockOnWarningStage.None : _tracker.IsLocked ? LockOnWarningStage.Locked : LockOnWarningStage.Tracking;

        public Damageable Target
        {
            get
            {
                var id = _tracker.TargetId;
                return id.HasValue && TargetLookup != null ? TargetLookup(id.Value) : null;
            }
        }

        public static LockOnAim Attach(GameObject player, WeaponController weapon, AimDownSights ads)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
            }

            var lockOn = player.AddComponent<LockOnAim>();
            lockOn.RoundSource = () => weapon.Weapon?.AmmoTypes.Round;
            lockOn.LookSource = () => weapon.Definition != null ? weapon.Definition.Homing : null;
            lockOn.AimingSource = () => ads != null && ads.IsAiming;
            lockOn.AimSource = () =>
            {
                weapon.ResolveShot(out var origin, out var direction);
                return new Ray(origin, direction);
            };
            var targets = new HitboxLockOnTargets(player.transform, weapon.HitMask);
            lockOn.Targets = targets;
            lockOn.TargetLookup = targets.Find;
            weapon.LockOn = lockOn;
            return lockOn;
        }

        // 撃った後に相手が動いても追えるよう、Id ではなく相手の今の位置を返し続ける関数を渡す。
        public Func<NVector3?> FollowLocked()
        {
            var id = LockedTargetId;
            return id.HasValue && Targets != null ? Targets.Follow(id.Value) : null;
        }

        public ILockOnDecoy DecoyOfLocked()
        {
            var id = LockedTargetId;
            return id.HasValue && Targets != null ? Targets.Decoy(id.Value) : null;
        }

        public void ShowOn(HudModel model)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var progress = Progress;
            if (progress.HasValue)
            {
                model.SetLockOn(progress.Value);
            }
            else
            {
                model.ClearLockOn();
            }
        }

        // 付けるのはこの画面のプレイヤーだけなので、出している HUD の値 (オフラインもネット対戦も GameSignals.HudModel) に書く。
        private void ShowOnHud()
        {
            if (!PublishesToHud)
            {
                return;
            }

            var model = GameSignals.HudModel.Value;
            if (model != null)
            {
                ShowOn(model);
            }
        }

        public void Tick(float deltaSeconds)
        {
            var round = RoundSource?.Invoke();
            var homing = round?.Homing;
            if (homing == null || Targets == null || AimSource == null || AimingSource == null || !AimingSource())
            {
                _tracker.Clear();
            }
            else
            {
                var aim = AimSource();
                var eye = aim.origin.ToNumerics();
                var forward = aim.direction.ToNumerics();
                _candidates.Clear();
                Targets.Collect(eye, forward, round.Range, _candidates);
                _tracker.Update(homing, deltaSeconds, true, eye, forward, _candidates);
            }

            Report();
            ShowOnHud();
            PlaySound(LookSource?.Invoke());
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnEnable()
        {
            if (!_active.Contains(this))
            {
                _active.Add(this);
            }
        }

        private void OnDisable()
        {
            _active.Remove(this);
            _tracker.Clear();
            Report();
            ShowOnHud();
            if (_audio != null)
            {
                _audio.Stop();
            }
        }

        private void Report()
        {
            var target = _tracker.TargetId;
            var locked = _tracker.IsLocked;
            if (target == _loggedTarget && locked == _loggedLocked)
            {
                return;
            }

            _loggedTarget = target;
            _loggedLocked = locked;
            var state = !target.HasValue ? "lost" : locked ? "locked" : "tracking";
            Debug.Log($"{LogTag} lock-on {state} target={(target.HasValue ? target.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none")}", this);
        }

        private void PlaySound(HomingSettings look)
        {
            var clip = look == null || !_tracker.TargetId.HasValue ? null : _tracker.IsLocked ? look.LockedSound : look.LockingSound;
            if (clip == null)
            {
                if (_audio != null && _audio.isPlaying)
                {
                    _audio.Stop();
                }

                return;
            }

            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.loop = true;
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f;
            }

            if (_audio.clip != clip || !_audio.isPlaying)
            {
                _audio.clip = clip;
                _audio.Play();
            }
        }
    }
}
