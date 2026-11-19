using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 復活させる人を選び、出す場所を決めるのはモードの側 (ModeRunnerBase) で、ここは置いた物として使える回数を数えることだけを持つ。
    // 撃たれて壊れるのは置ける物の土台 (Deployable) が受け持つ。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Deployable))]
    public sealed class SpawnBeacon : MonoBehaviour
    {
        public const string LogTag = BeaconLog.Tag;

        private static readonly List<SpawnBeacon> _armed = new List<SpawnBeacon>();

        private Deployable _deployable;
        private BeaconSpec _spec;
        private BeaconState _state;

        // 復活のたびにシーンを探さずに済むよう、使える間のものだけを置いた順に持つ。
        public static IReadOnlyList<SpawnBeacon> Armed => _armed;

        public bool IsArmed => _state != null && _state.Over == null && _deployable != null && !_deployable.IsRemoved;

        public Deployable Deployable => _deployable;

        public Damageable Damageable => _deployable != null ? _deployable.Damageable : null;

        public GameObject Owner => _deployable != null ? _deployable.Owner : null;

        public Side? Side => _deployable != null ? _deployable.Side : null;

        public BeaconSpec Spec => _spec;

        public int UsesLeft => _state != null ? _state.UsesLeft : 0;

        public Pose SpawnPose
        {
            get
            {
                var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-6f)
                {
                    forward = Vector3.forward;
                }

                forward.Normalize();
                var distance = _spec != null ? _spec.SpawnDistance : BeaconSpec.DefaultSpawnDistance;
                return new Pose(transform.position + (forward * distance), Quaternion.LookRotation(forward, Vector3.up));
            }
        }

        public void Arm(BeaconSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (_state != null)
            {
                throw new InvalidOperationException($"beacon '{name}' is already armed");
            }

            var deployable = GetComponent<Deployable>();
            if (deployable == null || deployable.Damageable == null)
            {
                throw new InvalidOperationException($"beacon '{name}' must be armed after its Deployable is initialized");
            }

            _deployable = deployable;
            _spec = spec;
            _state = new BeaconState(spec);

            // Edit Mode では OnDisable / OnDestroy が呼ばれないので、消えたことは Deployable から受けて一覧から外す。
            deployable.Removed += OnRemoved;
            Register();
            Debug.Log(BeaconLog.Armed(deployable.OwnerId, deployable.KindId, deployable.Serial, spec.Uses, deployable.Side), this);
        }

        // 陣営の無いモード (個人戦) では分隊が無いので、置いた本人だけが使える。
        public bool CanBeUsedBy(GameObject user)
        {
            if (!IsArmed || user == null)
            {
                return false;
            }

            var owner = _deployable.Owner;
            if (owner != null && user.transform.IsChildOf(owner.transform))
            {
                return true;
            }

            var side = _deployable.Side;
            if (!side.HasValue)
            {
                return false;
            }

            var member = SideMember.Of(user);
            return member != null && member.Side == side.Value;
        }

        public bool TryUse(GameObject user, string userId, out Pose spawn)
        {
            spawn = default;
            if (!CanBeUsedBy(user) || !_state.TryUse())
            {
                return false;
            }

            spawn = SpawnPose;
            Debug.Log(
                BeaconLog.Used(
                    _deployable.OwnerId, _deployable.KindId, _deployable.Serial, userId, spawn.position.ToNumerics(), _state.UsesLeft, _state.Uses),
                this);

            var over = _state.Over;
            if (over.HasValue)
            {
                _deployable.Remove(over.Value);
            }

            return true;
        }

        // 新しく置いた物ほど前線に近いとみなし、使える中でいちばん後に置いた物を選ぶ。
        public static bool TryFindFor(GameObject user, out SpawnBeacon beacon)
        {
            for (var i = _armed.Count - 1; i >= 0; i--)
            {
                var candidate = _armed[i];
                if (candidate != null && candidate.CanBeUsedBy(user))
                {
                    beacon = candidate;
                    return true;
                }
            }

            beacon = null;
            return false;
        }

        private void OnRemoved(Deployable deployable, DeployRemoval reason)
        {
            deployable.Removed -= OnRemoved;
            _armed.Remove(this);
        }

        private void Register()
        {
            _armed.RemoveAll(b => b == null);
            if (!_armed.Contains(this))
            {
                _armed.Add(this);
            }
        }

        private void OnEnable()
        {
            if (IsArmed)
            {
                Register();
            }
        }

        private void OnDisable()
        {
            _armed.Remove(this);
        }

        private void OnDestroy()
        {
            _armed.Remove(this);
        }
    }
}
