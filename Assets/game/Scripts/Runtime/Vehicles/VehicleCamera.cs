using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class VehicleCamera
    {
        private readonly ViewRig _rig;

        private Camera _camera;
        private Vector3 _savedLocalPosition;
        private Quaternion _savedLocalRotation;
        private ViewMode _savedMode;
        private bool _rigWasEnabled;
        private bool _engaged;
        private VehicleCameraView _view;

        private float _extraFieldOfView;
        private float _baseFieldOfView;
        private float _lastWrittenFieldOfView;
        private bool _wroteFieldOfView;
        private Vehicle _speedSource;
        private Rigidbody _speedBody;
        private Vector3 _lastSourcePosition;
        private bool _hasLastSourcePosition;

        public const float MaxFieldOfView = 179f;

        // 指数で寄せると 0 に着かないので、これより小さくなったら元の画角へ戻し切る。
        public const float FieldOfViewSettleDegrees = 0.01f;

        public VehicleCamera(ViewRig rig)
        {
            _rig = rig;
        }

        public bool IsEngaged => _engaged;

        public VehicleCameraView View => _view;

        public Camera Camera => _engaged ? _camera : null;

        public float ExtraFieldOfView => _extraFieldOfView;

        public void Begin(VehicleCameraView view)
        {
            if (_engaged)
            {
                SetView(view);
                return;
            }

            _engaged = true;
            _extraFieldOfView = 0f;
            _wroteFieldOfView = false;
            ForgetSpeedSource();
            _camera = _rig != null ? _rig.Camera : null;
            if (_camera != null)
            {
                _savedLocalPosition = _camera.transform.localPosition;
                _savedLocalRotation = _camera.transform.localRotation;
            }

            if (_rig != null)
            {
                // カメラはこちらが置くので、ViewRig の LateUpdate に目からのオフセットへ戻させない。
                _rigWasEnabled = _rig.enabled;
                // エモートが一人称から借りた三人称を、降りた後の戻り先として覚えないため。
                var emote = _rig.GetComponentInParent<EmoteController>();
                _savedMode = emote != null && emote.ChangedView ? ViewMode.FirstPerson : _rig.Mode;
                _rig.enabled = false;
            }

            SetView(view);
        }

        public void SetView(VehicleCameraView view)
        {
            if (!_engaged)
            {
                return;
            }

            _view = view;
            if (_rig == null)
            {
                return;
            }

            _rig.Mode = view == VehicleCameraView.Seat ? ViewMode.FirstPerson : ViewMode.ThirdPerson;
            _rig.ApplyBodyVisibility(true);
        }

        public void Place(VehicleSeat seat)
        {
            if (!_engaged || _camera == null || seat == null || seat.Vehicle == null)
            {
                return;
            }

            var eye = _rig.Eye != null ? _rig.Eye : _rig.transform;
            var rotation = eye.rotation;
            var cameraTransform = _camera.transform;
            if (_view == VehicleCameraView.Chase)
            {
                var vehicle = seat.Vehicle;
                var focus = vehicle.ChaseFocus;
                var offset = ChaseOffset(rotation, vehicle.Spec.ChaseDistance);
                cameraTransform.SetPositionAndRotation(_rig.ResolveBoomPosition(focus, offset, vehicle.transform), rotation);
                return;
            }

            if (seat.ViewPoint != null)
            {
                cameraTransform.SetPositionAndRotation(seat.ViewPoint.position, rotation);
                return;
            }

            if (cameraTransform.parent == eye)
            {
                cameraTransform.localPosition = Vector3.zero;
                cameraTransform.localRotation = Quaternion.identity;
            }
            else
            {
                cameraTransform.SetPositionAndRotation(eye.position, rotation);
            }
        }

        public void Restore()
        {
            if (!_engaged)
            {
                return;
            }

            _engaged = false;
            _extraFieldOfView = 0f;
            ForgetSpeedSource();
            if (_camera != null)
            {
                RestoreFieldOfView(_camera);
                _camera.transform.localPosition = _savedLocalPosition;
                _camera.transform.localRotation = _savedLocalRotation;
            }

            if (_rig != null)
            {
                _rig.Mode = _savedMode;
                _rig.enabled = _rigWasEnabled;
                _rig.ApplyBodyVisibility(_rig.enabled);
                if (_rig.enabled && _rig.IsInitialized)
                {
                    _rig.UpdateCameraPosition();
                }
            }

            _camera = null;
        }

        // Place と分けるのは、乗った瞬間や座席を移ったときの Place で時間を進めないため。LateUpdate から 1 フレームに 1 回呼ぶ。
        public void StepFieldOfView(Vehicle vehicle, float deltaSeconds)
        {
            if (!_engaged || vehicle == null)
            {
                return;
            }

            StepFieldOfView(vehicle.Spec.SpeedFieldOfView, ForwardSpeedOf(vehicle, deltaSeconds), deltaSeconds);
        }

        public void StepFieldOfView(VehicleSpeedFieldOfViewSpec spec, float forwardSpeed, float deltaSeconds)
        {
            if (spec == null)
            {
                throw new System.ArgumentNullException(nameof(spec));
            }

            if (!_engaged || _camera == null)
            {
                return;
            }

            var goal = spec.ExtraDegreesAt(forwardSpeed);
            _extraFieldOfView = spec.Blend(_extraFieldOfView, goal, deltaSeconds);
            if (goal <= 0f && _extraFieldOfView < FieldOfViewSettleDegrees)
            {
                _extraFieldOfView = 0f;
            }

            // 元の画角を乗ったときに一度だけ取らないのは、乗っている間に設定などで変えた画角を、戻すときに消さないため。
            if (!_wroteFieldOfView || !Mathf.Approximately(_camera.fieldOfView, _lastWrittenFieldOfView))
            {
                if (_extraFieldOfView <= 0f)
                {
                    _wroteFieldOfView = false;
                    return;
                }

                _baseFieldOfView = _camera.fieldOfView;
            }

            if (_extraFieldOfView <= 0f)
            {
                RestoreFieldOfView(_camera);
                return;
            }

            _lastWrittenFieldOfView = Mathf.Min(_baseFieldOfView + _extraFieldOfView, Mathf.Max(_baseFieldOfView, MaxFieldOfView));
            _camera.fieldOfView = _lastWrittenFieldOfView;
            _wroteFieldOfView = true;
        }

        // 剛体の速度だけにしないのは、剛体が無いか kinematic で位置だけ動かす乗り物 (通信の複製など) では速度が 0 のままのため。
        private float ForwardSpeedOf(Vehicle vehicle, float deltaSeconds)
        {
            if (!ReferenceEquals(_speedSource, vehicle))
            {
                _speedSource = vehicle;
                _speedBody = vehicle.GetComponent<Rigidbody>();
                _hasLastSourcePosition = false;
            }

            var transform = vehicle.transform;
            var position = transform.position;
            var speed = 0f;
            if (_speedBody != null && !_speedBody.isKinematic)
            {
                speed = Vector3.Dot(_speedBody.linearVelocity, transform.forward);
            }
            else if (_hasLastSourcePosition && deltaSeconds > 0f && !float.IsInfinity(deltaSeconds))
            {
                speed = Vector3.Dot(position - _lastSourcePosition, transform.forward) / deltaSeconds;
            }

            _lastSourcePosition = position;
            _hasLastSourcePosition = true;
            return speed;
        }

        private void ForgetSpeedSource()
        {
            _speedSource = null;
            _speedBody = null;
            _hasLastSourcePosition = false;
        }

        private void RestoreFieldOfView(Camera camera)
        {
            if (!_wroteFieldOfView)
            {
                return;
            }

            // 無条件に戻さないのは、後から別の部品が書いた画角を上書きしないため。
            if (Mathf.Approximately(camera.fieldOfView, _lastWrittenFieldOfView))
            {
                camera.fieldOfView = _baseFieldOfView;
            }

            _wroteFieldOfView = false;
        }

        public static Vector3 ChaseOffset(Quaternion viewRotation, float distance)
        {
            return viewRotation * (Vector3.back * distance);
        }
    }
}
