using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 座席に座っている間、プレイヤーのカメラを借りて置く部分。いつ借りて返すかは持ち主 (<see cref="PlayerVehicleRider"/>) が決める。
    /// <list type="bullet">
    /// <item><see cref="Begin"/> で <see cref="ViewRig"/> を止め (カメラはこちらが置く)、<see cref="Restore"/> でカメラの位置と ViewRig を元に戻す。</item>
    /// <item><see cref="VehicleCameraView.Chase"/>: 乗り物の注視点 (<see cref="Vehicle.ChaseFocus"/>) から、視点の向きの反対へ
    /// <see cref="VehicleSpec.ChaseDistance"/> 離して置く。間に壁があれば手前まで縮める (<see cref="ViewRig.ResolveBoomPosition(Vector3, Vector3, Transform)"/>、
    /// 中身は <see cref="CameraBoom.Length"/>)。乗り物自身では縮めない。体の向きは乗り物の向きに付いて回るので、視点を動かさなければ乗り物の後ろから追う。</item>
    /// <item><see cref="VehicleCameraView.Seat"/>: 座席の視点 (<see cref="VehicleSeat.ViewPoint"/>) があればそこ、無ければ目に重ねる。自分の体は影だけにする。</item>
    /// </list>
    /// どちらも向きは目 (視点操作の yaw と pitch) に合わせる。
    /// </summary>
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

        /// <param name="rig">カメラを持つプレイヤーの ViewRig。null ならカメラを動かさない (専用サーバーや敵)。</param>
        public VehicleCamera(ViewRig rig)
        {
            _rig = rig;
        }

        /// <summary><see cref="Begin"/> してから <see cref="Restore"/> するまでの間か。</summary>
        public bool IsEngaged => _engaged;

        /// <summary>今のカメラ。借りていなければ意味を持たない。</summary>
        public VehicleCameraView View => _view;

        /// <summary>借りているカメラ。借りていないか、カメラが無ければ null。</summary>
        public Camera Camera => _engaged ? _camera : null;

        /// <summary>カメラを借りて view にする。借りている間に呼ぶと view に切り替えるだけ。</summary>
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

        /// <summary>カメラを view に切り替え、自分の体の描き方を合わせる。借りていなければ何もしない。</summary>
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

        /// <summary>seat に座っている間のカメラを置く。借りていないか、カメラが無ければ何もしない。</summary>
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

        /// <summary>カメラの位置・ViewRig の視点と有効を元に戻す。借りていなければ何もしない。</summary>
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

        /// <summary>追うカメラの、注視点から見た位置 (ワールド)。視点の向きの反対へ distance 離す。</summary>
        public static Vector3 ChaseOffset(Quaternion viewRotation, float distance)
        {
            return viewRotation * (Vector3.back * distance);
        }
    }
}
