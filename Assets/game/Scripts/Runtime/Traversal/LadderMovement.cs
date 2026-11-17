using System;
using Shooter.Core;
using UnityEngine;
using LadderRule = Shooter.Core.Ladder;

namespace Shooter.Runtime.Traversal
{
    [DisallowMultipleComponent]
    public sealed class LadderMovement : MonoBehaviour, IMovementOverride
    {
        // ほかの横取りより高くし、はしごにつかまっている間はこちらが先に選ばれるようにする。
        public const int MovementPriority = 200;

        public const float LandingLift = LadderClimb.LandingLift;

        public const float LandingDrop = LadderClimb.LandingDrop;

        private LadderRule _rule = LadderRule.Off;
        private PlayerController _mover;
        private WeaponController _weapon;
        private AimDownSights _ads;
        private Ladder _pendingLadder;
        private LadderEntry _pendingEntry;
        private Ladder _ladder;
        private LadderClimb _climb;
        private bool _placed;
        private float _regrabRemaining;

        public event Action<Ladder, LadderEntry> Grabbed;

        public event Action<Ladder, LadderOutcome> Left;

        public LadderRule Rule
        {
            get => _rule;
            set => _rule = value;
        }

        public PlayerController Mover => _mover;

        public WeaponController Weapon => _weapon;

        public bool IsClimbing => _climb != null;

        public Ladder CurrentLadder => _climb != null ? _ladder : null;

        public LadderClimb Climb => _climb;

        public float RegrabRemaining => _regrabRemaining;

        public int Priority => MovementPriority;

        public PlayerActionConditions Conditions => PlayerActionConditions.OnLadder;

        public bool IsActive
        {
            get
            {
                if (_climb != null)
                {
                    if (CanKeepClimbing())
                    {
                        return true;
                    }

                    Leave(LadderOutcome.Released);
                    return false;
                }

                _pendingLadder = null;
                if (_rule != LadderRule.On || _mover == null || !_mover.IsInitialized || !IsLive || _regrabRemaining > 0f)
                {
                    return false;
                }

                return FindLadder(out _pendingLadder, out _pendingEntry);
            }
        }

        public void Bind(PlayerController mover, WeaponController weapon, AimDownSights ads)
        {
            if (mover == null)
            {
                throw new ArgumentNullException(nameof(mover));
            }

            if (_mover != null && _mover != mover)
            {
                _mover.UnregisterMovementOverride(this);
            }

            if (_climb != null && _weapon != weapon)
            {
                ReleaseWeapon();
                if (weapon != null)
                {
                    weapon.Holster.Claim(this);
                }
            }

            _mover = mover;
            _weapon = weapon;
            _ads = ads;
            mover.RegisterMovementOverride(this);
        }

        public void AdvanceCooldown(float deltaSeconds)
        {
            if (deltaSeconds > 0f && !float.IsInfinity(deltaSeconds))
            {
                _regrabRemaining = Mathf.Max(0f, _regrabRemaining - deltaSeconds);
            }
        }

        public void Begin(PlayerController mover)
        {
            var ladder = _pendingLadder;
            _pendingLadder = null;
            if (ladder == null || !ladder.TryGetShape(out var span, out var spec))
            {
                return;
            }

            _ladder = ladder;
            _climb = new LadderClimb(span, spec, _pendingEntry, span.HeightOf(FeetOf(mover).ToNumerics()), mover.Motor.Spec);
            _placed = false;
            if (_weapon != null)
            {
                _weapon.Holster.Claim(this);
            }

            if (_ads != null)
            {
                _ads.Release();
            }

            Debug.Log(LadderLog.Grab(ladder.name, _climb.Entry, _climb.Height), this);
            Grabbed?.Invoke(ladder, _climb.Entry);
        }

        public MovementOverrideStep Step(PlayerController mover, MovementOverrideInput input, float deltaSeconds)
        {
            var climb = _climb;
            if (climb == null)
            {
                // Begin がつかまれなかった Tick。次の Tick で IsActive が false になり、ふつうの移動に戻る。
                return MovementOverrideStep.Move(Vector3.zero, Vector3.zero);
            }

            var span = climb.Span;
            var radius = RadiusOf(mover);

            // 置く前の接地は、つかまる前に立っていた床 (上の足場など) のものなので読まない。
            var outcome = climb.Step(input.Move.y, input.JumpPressed, input.IsGrounded && _placed, deltaSeconds);
            switch (outcome)
            {
                case LadderOutcome.ExitTop:
                    {
                        var feet = span.TopExit(radius).ToUnity() + (Vector3.up * LandingLift);
                        Leave(LadderOutcome.ExitTop);
                        return new MovementOverrideStep(Vector3.down * LandingDrop, Vector3.zero, RootFor(mover, feet), crouched: false);
                    }

                case LadderOutcome.ExitBottom:
                    {
                        var feet = climb.Feet(radius).ToUnity() + (Vector3.up * LandingLift);
                        Leave(LadderOutcome.ExitBottom);
                        return new MovementOverrideStep(Vector3.down * LandingDrop, Vector3.zero, RootFor(mover, feet), crouched: false);
                    }

                case LadderOutcome.JumpOff:
                    {
                        var velocity = span.JumpOffVelocity(climb.Spec.JumpOffSpeed).ToUnity();
                        Leave(LadderOutcome.JumpOff);
                        return new MovementOverrideStep(velocity * deltaSeconds, velocity, crouched: false);
                    }

                default:
                    {
                        var target = climb.Feet(radius).ToUnity();
                        var velocity = Vector3.up * climb.VerticalSpeed;
                        if (!_placed)
                        {
                            _placed = true;
                            return new MovementOverrideStep(
                                Vector3.zero, velocity, RootFor(mover, target), FacingYaw(span), crouched: false);
                        }

                        return new MovementOverrideStep(target - FeetOf(mover), velocity, crouched: false);
                    }
            }
        }

        public void End(PlayerController mover)
        {
            _pendingLadder = null;
            if (_climb != null)
            {
                Leave(LadderOutcome.Released);
            }
        }

        public static Vector3 FeetOf(PlayerController mover)
        {
            var controller = mover.Controller;
            var center = controller.transform.TransformPoint(controller.center);
            return center - (Vector3.up * (controller.height / 2f));
        }

        private static float RadiusOf(PlayerController mover)
        {
            return mover.Controller.radius;
        }

        // 足元と本体の差は姿勢を変えても保たれる (しゃがみは足元を残して縮む) ので、今の差をそのまま足す。
        private static Vector3 RootFor(PlayerController mover, Vector3 feet)
        {
            return feet + (mover.transform.position - FeetOf(mover));
        }

        private static float FacingYaw(LadderSpan span)
        {
            return Mathf.Atan2(span.Facing.X, span.Facing.Z) * Mathf.Rad2Deg;
        }

        // isActiveAndEnabled は Edit Mode では OnEnable が走らないため常に false になり、テストから動かせないので、enabled と階層の有効で見る。
        private bool IsLive => enabled && gameObject.activeInHierarchy;

        private bool CanKeepClimbing()
        {
            return _rule == LadderRule.On && _ladder != null && _ladder.IsRegistered && IsLive;
        }

        private bool FindLadder(out Ladder found, out LadderEntry entry)
        {
            var feet = FeetOf(_mover).ToNumerics();
            var velocity = _mover.Motor.Velocity;
            var radius = RadiusOf(_mover);
            var ladders = Ladder.Enabled;
            for (var i = 0; i < ladders.Count; i++)
            {
                var ladder = ladders[i];
                if (ladder == null || !ladder.IsRegistered || !ladder.TryGetShape(out var span, out var spec))
                {
                    continue;
                }

                entry = span.Probe(feet, velocity, radius, spec.GrabDistance, _mover.Motor.Spec);
                if (entry != LadderEntry.None)
                {
                    found = ladder;
                    return true;
                }
            }

            found = null;
            entry = LadderEntry.None;
            return false;
        }

        private void Leave(LadderOutcome reason)
        {
            var ladder = _ladder;
            var climb = _climb;
            _climb = null;
            _ladder = null;
            _placed = false;
            _regrabRemaining = climb != null ? climb.Spec.RegrabSeconds : 0f;
            ReleaseWeapon();
            Debug.Log(LadderLog.Leave(ladder != null ? ladder.name : null, reason, climb != null ? climb.Height : 0f), this);
            Left?.Invoke(ladder, reason);
        }

        private void ReleaseWeapon()
        {
            if (_weapon != null)
            {
                _weapon.Holster.Release(this);
            }
        }

        private void Update()
        {
            AdvanceCooldown(Time.deltaTime);
        }

        private void OnDisable()
        {
            if (_climb != null)
            {
                Leave(LadderOutcome.Released);
            }
        }

        private void OnDestroy()
        {
            if (_mover != null)
            {
                _mover.UnregisterMovementOverride(this);
            }
        }
    }
}
