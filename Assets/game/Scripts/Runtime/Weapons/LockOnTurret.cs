using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 捕捉の進み方は LockOnAim (LockOnTracker) に任せ、ここは空にいる相手へ頭を向けることだけを持つ。
    // 捕捉を LockOnAim に通すのは、狙われた側の警告 (LockOnWarningPublisher) とフレア (VehicleCountermeasures) が、
    // プレイヤーの武器に捉えられたときと同じ道で効くようにするため。
    [DisallowMultipleComponent]
    public sealed class LockOnTurret : MonoBehaviour
    {
        [SerializeField, Tooltip("捕捉に使う武器。弾に Homing のある武器にする。射程・捕捉の秒・円錐はこの武器の値を使う")]
        private WeaponDefinition _weapon;

        [SerializeField, Tooltip("相手へ向ける部分。この前の向きで捕捉する。空なら自分を向ける")]
        private Transform _head;

        [SerializeField, Tooltip("相手を探し、見えるかを確かめるレイヤー")]
        private LayerMask _targetMask = Physics.DefaultRaycastLayers;

        [SerializeField, Min(0.01f), Tooltip("頭を回す速さ (度/秒)")]
        private float _turnDegreesPerSecond = LockOnTurretSpec.DefaultTurnDegreesPerSecond;

        [SerializeField, Tooltip("タレットの原点よりこの高さ (m) 以上にいる相手だけを狙う。地上の人や車を捉えないようにする")]
        private float _minTargetHeight = LockOnTurretSpec.DefaultMinTargetHeight;

        private readonly List<LockOnCandidate> _candidates = new List<LockOnCandidate>();

        private LockOnTurretSpec _spec;
        private LockOnAim _aim;
        private HitboxLockOnTargets _targets;
        private RoundSpec _round;
        private WeaponDefinition _roundSource;
        private bool _hasTarget;
        private Vector3 _targetPoint;
        private bool _warnedWeapon;

        public WeaponDefinition Weapon
        {
            get => _weapon;
            set
            {
                _weapon = value;
                _roundSource = null;
                _round = null;
                _warnedWeapon = false;
            }
        }

        public Transform Head
        {
            get => _head != null ? _head : transform;
            set => _head = value;
        }

        public LayerMask TargetMask
        {
            get => _targetMask;
            set
            {
                _targetMask = value;
                _targets = null;
                if (_aim != null)
                {
                    Wire(_aim);
                }
            }
        }

        public LockOnTurretSpec Spec => _spec ??= ReadSpec();

        public LockOnAim Aim => EnsureAim();

        public bool HasTarget => _hasTarget;

        public Vector3 TargetPoint => _targetPoint;

        private void Awake()
        {
            EnsureAim();
        }

        private void OnValidate()
        {
            _spec = null;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDisable()
        {
            _hasTarget = false;
        }

        public void Configure(LockOnTurretSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _turnDegreesPerSecond = spec.TurnDegreesPerSecond;
            _minTargetHeight = spec.MinTargetHeight;
        }

        // Update から呼ぶ。物理の進まない Edit Mode のテストからも 1 コマずつ進められるよう public にする。捕捉は LockOnAim.Tick が進める。
        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            EnsureAim();
            var round = Round();
            _hasTarget = round != null && TryChooseTarget(round, out _targetPoint);
            if (!_hasTarget)
            {
                return false;
            }

            var head = Head;
            var direction = _targetPoint - head.position;
            if (direction.sqrMagnitude > 1e-8f)
            {
                head.rotation = Quaternion.RotateTowards(
                    head.rotation, Quaternion.LookRotation(direction, Vector3.up), Spec.TurnDegreesPerSecond * deltaSeconds);
            }

            return true;
        }

        // 捕捉中の相手は外さず、それ以外は近い順に選ぶ。近い相手ほど頭を回す角が大きくても先に捉えて知らせたいため。
        private bool TryChooseTarget(RoundSpec round, out Vector3 point)
        {
            point = default;
            var head = Head;
            var eye = head.position;
            _candidates.Clear();
            _targets.Collect(eye.ToNumerics(), head.forward.ToNumerics(), round.Range, _candidates);

            var spec = Spec;
            var self = SideMember.Of(gameObject);
            var tracked = _aim.TargetId;
            var found = false;
            var bestDistance = float.PositiveInfinity;
            foreach (var candidate in _candidates)
            {
                var candidatePoint = candidate.Point.ToUnity();
                if (!spec.IsHighEnough(transform.position.y, candidatePoint.y) || IsAlly(self, candidate.Id))
                {
                    continue;
                }

                if (tracked.HasValue && candidate.Id == tracked.Value)
                {
                    point = candidatePoint;
                    return true;
                }

                var distance = (candidatePoint - eye).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    point = candidatePoint;
                    found = true;
                }
            }

            return found;
        }

        private bool IsAlly(SideMember self, int candidateId)
        {
            if (self == null)
            {
                return false;
            }

            var body = _targets.Find(candidateId);
            return body != null && self.IsAllyOf(SideMember.Of(body.gameObject));
        }

        private LockOnAim EnsureAim()
        {
            if (_aim != null)
            {
                return _aim;
            }

            _aim = GetComponent<LockOnAim>();
            if (_aim == null)
            {
                _aim = gameObject.AddComponent<LockOnAim>();
            }

            Wire(_aim);
            return _aim;
        }

        // 音は付けない。LockOnAim の音は撃つ人の耳に鳴らす音で、狙われた側に聞かせる音ではないため。
        private void Wire(LockOnAim aim)
        {
            _targets = new HitboxLockOnTargets(transform, _targetMask);
            aim.PublishesToHud = false;
            aim.RoundSource = Round;
            aim.LookSource = null;
            aim.AimingSource = () => _hasTarget && enabled;
            aim.AimSource = () => new Ray(Head.position, Head.forward);
            aim.Targets = _targets;
            aim.TargetLookup = _targets.Find;
        }

        private RoundSpec Round()
        {
            if (_weapon == null)
            {
                WarnWeapon(LockOnTurretLog.NoWeapon(name));
                return null;
            }

            if (ReferenceEquals(_roundSource, _weapon))
            {
                return _round;
            }

            _roundSource = _weapon;
            try
            {
                _round = _weapon.ToSpec().Round;
            }
            catch (ArgumentException e)
            {
                _round = null;
                WarnWeapon(LockOnTurretLog.InvalidWeapon(name, e.Message));
                return null;
            }

            if (_round.Homing == null)
            {
                WarnWeapon(LockOnTurretLog.NoHoming(name, _weapon.Id));
                _round = null;
            }

            return _round;
        }

        private void WarnWeapon(string message)
        {
            if (_warnedWeapon)
            {
                return;
            }

            _warnedWeapon = true;
            Debug.LogWarning(message, this);
        }

        private LockOnTurretSpec ReadSpec()
        {
            try
            {
                return new LockOnTurretSpec(_turnDegreesPerSecond, _minTargetHeight);
            }
            catch (ArgumentException e)
            {
                Debug.LogError(LockOnTurretLog.InvalidSettings(name, e.Message), this);
                return LockOnTurretSpec.Default;
            }
        }
    }
}
