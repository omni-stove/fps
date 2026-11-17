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

        public VehicleCamera(ViewRig rig)
        {
            _rig = rig;
        }

        public bool IsEngaged => _engaged;

        public VehicleCameraView View => _view;

        public Camera Camera => _engaged ? _camera : null;

        public void Begin(VehicleCameraView view)
        {
            if (_engaged)
            {
                SetView(view);
                return;
            }

            _engaged = true;
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
                _savedMode = _rig.Mode;
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
            if (_camera != null)
            {
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

        public static Vector3 ChaseOffset(Quaternion viewRotation, float distance)
        {
            return viewRotation * (Vector3.back * distance);
        }
    }
}
