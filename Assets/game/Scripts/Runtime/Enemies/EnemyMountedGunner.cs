using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // EnemyAgent の Update が目標を選び直し、乗りに行く判断 (MountedGunEnemyBehavior) を済ませた後に、同じフレームで乗る・撃つ。
    // 乗っている間の EnemyAgent は判断を進めず座席に留まるだけなので、銃座の入力はここが出す。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAgent))]
    [DefaultExecutionOrder(10)]
    public sealed class EnemyMountedGunner : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("戦っている間に、この距離 (m) の内にある空いた固定銃座へ乗りに行く。0 なら乗りに行かない")]
        private float _seekRadius = MountedGunUseSpec.DefaultSeekRadius;

        [SerializeField, Min(0.01f), Tooltip("目標が見えないか銃を向けられない間がこれだけ続いたら降りる (秒)")]
        private float _giveUpSeconds = MountedGunUseSpec.DefaultGiveUpSeconds;

        [SerializeField, Min(0f), Tooltip("降りてから、また乗りに行けるまでの秒数")]
        private float _remountDelaySeconds = MountedGunUseSpec.DefaultRemountDelaySeconds;

        private EnemyAgent _agent;
        private MountedGunEnemyBehavior _behavior;
        private EnemyBrain _attachedBrain;
        private MountedGun _candidate;
        private MountedGun _manned;
        private bool _wasSeeking;

        public MountedGunEnemyBehavior Behavior => EnsureBehavior();

        public MountedGunUseSpec Spec => new MountedGunUseSpec(_seekRadius, _giveUpSeconds, _remountDelaySeconds);

        public MountedGun Candidate => _candidate;

        public MountedGun Manned => _manned;

        public bool IsAttached => _attachedBrain != null;

        private string Id => _agent != null ? _agent.Id : name;

        public void ApplySpec(MountedGunUseSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _seekRadius = spec.SeekRadius;
            _giveUpSeconds = spec.GiveUpSeconds;
            _remountDelaySeconds = spec.RemountDelaySeconds;
            EnsureBehavior().ApplySpec(spec);
        }

        private void OnValidate()
        {
            _behavior?.ApplySpec(Spec);
        }

        private void OnDisable()
        {
            Detach();
        }

        private void OnDestroy()
        {
            Detach();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (_agent == null)
            {
                _agent = GetComponent<EnemyAgent>();
            }

            if (!TryAttach())
            {
                return;
            }

            if (_attachedBrain.IsDead)
            {
                SetCandidate(null);
                return;
            }

            var gun = RiddenGun();
            if (gun != null)
            {
                _manned = gun;
                Man(gun, deltaSeconds);
                return;
            }

            if (_manned != null)
            {
                // 倒れた・押し出されたなど、自分で降りたのでなくても、すぐ同じ銃座へ戻らせない。
                _manned = null;
                _behavior.NoteLeft();
            }

            if (_agent.IsRiding)
            {
                SetCandidate(null);
                return;
            }

            LogSeekStart();
            SetCandidate(FindCandidate());
            TryBoardCandidate();
        }

        private MountedGun RiddenGun()
        {
            var seat = _agent.Seat;
            var vehicle = seat != null ? seat.Vehicle : null;
            var gun = vehicle != null ? vehicle.GetComponent<MountedGun>() : null;
            return gun != null && gun.IsReady ? gun : null;
        }

        private MountedGun FindCandidate()
        {
            var target = _agent.PerceivedTarget;
            if (target == null || target.IsDead)
            {
                return null;
            }

            MountedGun best = null;
            var bestDistance = float.PositiveInfinity;
            var self = _agent.GroundPosition;
            foreach (var gun in MountedGun.Active)
            {
                // 目標へ向けられない銃座に乗っても撃てず、降りるまで立ち尽くすだけになる。
                if (!gun.IsFree || !gun.CanAimAt(target.AimPoint))
                {
                    continue;
                }

                var distance = HorizontalDistance(self, gun.BoardPoint);
                if (distance < bestDistance)
                {
                    best = gun;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void SetCandidate(MountedGun gun)
        {
            _candidate = gun;
            EnsureBehavior().SetCandidate(gun != null ? gun.BoardPoint.ToNumerics() : (System.Numerics.Vector3?)null);
        }

        // 判断は EnemyAgent の Update で進むので、乗りに行き始めたことはその後のここで知る。
        private void LogSeekStart()
        {
            var seeking = _behavior.IsSeeking && _candidate != null;
            if (seeking && !_wasSeeking)
            {
                Debug.Log(MountedGunLog.Seek(Id, _candidate.name, HorizontalDistance(_agent.GroundPosition, _candidate.BoardPoint)), this);
            }

            _wasSeeking = seeking;
        }

        private void TryBoardCandidate()
        {
            var gun = _candidate;
            if (gun == null || !_behavior.IsSeeking)
            {
                return;
            }

            // 乗る距離は乗り物と同じ測り方で見る。届かないうちに頼むと、断られたログが毎フレーム出る。
            if (gun.Seat.DistanceToEnter(_agent.transform.position) > gun.Vehicle.Spec.EnterRange)
            {
                return;
            }

            if (_agent.TryBoard(gun.Vehicle, gun.Seat.Index))
            {
                _manned = gun;
                _wasSeeking = false;
                SetCandidate(null);
                Debug.Log(MountedGunLog.Man(Id, gun.name), this);
            }
        }

        private void Man(MountedGun gun, float deltaSeconds)
        {
            var mount = gun.Mount;
            var target = _agent.PerceivedTarget;
            var hasTarget = target != null && !target.IsDead;
            var muzzle = mount.Muzzle.position;
            var aimPoint = hasTarget ? target.AimPoint : Vector3.zero;
            var distance = hasTarget ? Vector3.Distance(muzzle, aimPoint) : 0f;

            // 盾は銃手の目の前にあるので、銃手の目からでは前が見えない。銃口から見て、銃座そのものは遮りに数えない。
            var visible = hasTarget
                && distance <= _agent.SightRange
                && distance > 1e-3f
                && Hitscan.HasLineOfSight(muzzle, target, distance + 0.01f, gun.Vehicle.transform)
                && !EnemySenses.IsVisionBlocked(muzzle, aimPoint);
            var canAim = hasTarget && gun.CanAimAt(aimPoint);
            var pointing = canAim && gun.AimOffsetDegrees(aimPoint) <= _attachedBrain.Spec.FireToleranceDegrees;
            // 目標がいないのは射角の外ではないので、降りる理由が OutOfArc にならないよう届くものとして渡す。
            var order = _behavior.Man(deltaSeconds, visible, !hasTarget || canAim, pointing);

            var aim = canAim ? (aimPoint - mount.transform.position).normalized : Vector3.zero;
            _agent.Seat.SubmitInput(_agent, new VehicleSeatInput(Vector2.zero, 0f, false, order.Fire, false, false, muzzle, aim), deltaSeconds);

            if (order.Leave.HasValue && _agent.TryLeaveVehicle())
            {
                Debug.Log(MountedGunLog.Leave(Id, gun.name, order.Leave.Value), this);
                _manned = null;
                _behavior.NoteLeft();
            }
        }

        // EnemyAgent は Awake で判断を作るが、同じ GameObject の Awake の順は決まらないので、使うときに付ける。
        private bool TryAttach()
        {
            var brain = _agent != null ? _agent.Brain : null;
            if (brain == null)
            {
                return false;
            }

            if (ReferenceEquals(brain, _attachedBrain))
            {
                return true;
            }

            Detach();
            brain.AddBehavior(EnsureBehavior());
            brain.StateChanged += OnBrainStateChanged;
            _attachedBrain = brain;
            return true;
        }

        private void Detach()
        {
            if (_attachedBrain == null)
            {
                return;
            }

            _attachedBrain.RemoveBehavior(_behavior);
            _attachedBrain.StateChanged -= OnBrainStateChanged;
            _attachedBrain = null;
        }

        // 倒れて生き返ったら、前の命の待ち時間や行き先を持ち越さない。
        private void OnBrainStateChanged(EnemyState previous, EnemyState next)
        {
            if (next == EnemyState.Dead)
            {
                _behavior.Reset();
                _candidate = null;
                _manned = null;
                _wasSeeking = false;
            }
        }

        private MountedGunEnemyBehavior EnsureBehavior()
        {
            return _behavior ??= new MountedGunEnemyBehavior(Spec);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
