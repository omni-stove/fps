using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    [DisallowMultipleComponent]
    public sealed class ViewmodelMotion : MonoBehaviour
    {
        public const string PivotName = "ViewmodelPivot";

        // 瞬間移動・復活・視点の切り替えで向きが飛んだフレームを、1 回の素早い振り向きとして遅れに入れないよう捨てる。
        public const float MaxTrackedDegreesPerSecond = 3600f;

        private readonly ViewmodelSway _sway = new ViewmodelSway();
        private ViewRig _rig;
        private AimDownSights _ads;
        private CharacterController _controller;
        private Transform _pivot;
        private bool _resolved;
        private bool _hasLastView;
        private float _lastYaw;
        private float _lastPitchUp;

        public ViewmodelSway Sway => _sway;

        public Transform Pivot => _pivot;

        public ViewmodelSwaySettings Settings
        {
            get => _sway.Settings;
            set => _sway.Settings = value;
        }

        public Func<float> HorizontalSpeedSource { get; set; }

        public Func<float> AdsProgressSource { get; set; }

        private void OnEnable()
        {
            if (NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                enabled = false;
                return;
            }

            // ClearSubscribers で外されていても戻るよう、有効になるたびに付け直す (二重には付かない)。
            CombatEvents.ShotFired -= HandleShotFired;
            CombatEvents.ShotFired += HandleShotFired;
        }

        private void OnDisable()
        {
            CombatEvents.ShotFired -= HandleShotFired;
            ResetMotion();
        }

        private void LateUpdate()
        {
            Step(Time.deltaTime);
        }

        public Transform EnsurePivot()
        {
            if (_pivot != null)
            {
                return _pivot;
            }

            Resolve();
            var root = _rig != null ? _rig.ViewmodelRoot : null;
            if (root == null)
            {
                return null;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (string.Equals(child.name, PivotName, StringComparison.Ordinal))
                {
                    _pivot = child;
                    return _pivot;
                }
            }

            var pivot = new GameObject(PivotName) { layer = root.gameObject.layer }.transform;
            pivot.SetParent(root, false);
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;
            _pivot = pivot;
            return _pivot;
        }

        public bool IsOwnShot(GameObject shooter)
        {
            return shooter != null && shooter.transform.IsChildOf(transform);
        }

        public void Kick()
        {
            _sway.Kick();
            Apply();
        }

        public void ResetMotion()
        {
            _sway.Reset();
            _hasLastView = false;
            Apply();
        }

        public void Step(float deltaSeconds)
        {
            if (_pivot == null)
            {
                return;
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            Resolve();
            ReadViewRates(deltaSeconds, out var yawRate, out var pitchRate);
            _sway.Tick(deltaSeconds, HorizontalSpeed(), yawRate, pitchRate, AdsProgress());
            Apply();
        }

        private void HandleShotFired(ShotFiredInfo shot)
        {
            if (IsOwnShot(shot.Shooter))
            {
                Kick();
            }
        }

        private void Resolve()
        {
            if (_resolved)
            {
                return;
            }

            _resolved = true;
            _rig = GetComponent<ViewRig>();
            _ads = GetComponent<AimDownSights>();
            _controller = GetComponent<CharacterController>();
        }

        private void ReadViewRates(float deltaSeconds, out float yawRate, out float pitchRate)
        {
            yawRate = 0f;
            pitchRate = 0f;
            var camera = _rig != null ? _rig.Camera : null;
            if (camera == null)
            {
                _hasLastView = false;
                return;
            }

            var euler = camera.transform.rotation.eulerAngles;
            var yaw = euler.y;
            var pitchUp = -Mathf.DeltaAngle(0f, euler.x);
            if (_hasLastView && deltaSeconds > 0f)
            {
                yawRate = Mathf.DeltaAngle(_lastYaw, yaw) / deltaSeconds;
                pitchRate = Mathf.DeltaAngle(_lastPitchUp, pitchUp) / deltaSeconds;
                if (Mathf.Abs(yawRate) > MaxTrackedDegreesPerSecond || Mathf.Abs(pitchRate) > MaxTrackedDegreesPerSecond)
                {
                    yawRate = 0f;
                    pitchRate = 0f;
                }
            }

            _lastYaw = yaw;
            _lastPitchUp = pitchUp;
            _hasLastView = true;
        }

        private float HorizontalSpeed()
        {
            float speed;
            if (HorizontalSpeedSource != null)
            {
                speed = HorizontalSpeedSource();
            }
            else if (_controller != null && _controller.isGrounded)
            {
                var velocity = _controller.velocity;
                speed = new Vector2(velocity.x, velocity.z).magnitude;
            }
            else
            {
                speed = 0f;
            }

            return speed >= 0f && !float.IsInfinity(speed) ? speed : 0f;
        }

        private float AdsProgress()
        {
            var progress = AdsProgressSource != null ? AdsProgressSource() : _ads != null ? _ads.Progress : 0f;
            return float.IsNaN(progress) || float.IsInfinity(progress) ? 0f : progress;
        }

        private void Apply()
        {
            if (_pivot == null)
            {
                return;
            }

            _pivot.localPosition = _sway.Position.ToUnity();
            _pivot.localRotation = Quaternion.Euler(-_sway.PitchUpDegrees, _sway.YawRightDegrees, 0f);
        }
    }
}
