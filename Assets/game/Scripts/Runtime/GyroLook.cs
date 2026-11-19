using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using InputGyroscope = UnityEngine.InputSystem.Gyroscope;

namespace Shooter.Runtime
{
    // 部品にしないのは、視点の感度・覗き込みの倍率・反動と同じ順で回すよう FirstPersonLook の Update から読むため。
    public sealed class GyroLook
    {
        private readonly Func<InputGyroscope> _deviceSource;
        private readonly Func<InputGyroscope, Vector3> _reader;
        private InputGyroscope _device;
        private bool _enabledDevice;
        private bool _reportedMissing;

        public GyroLook()
            : this(() => InputGyroscope.current, ReadAngularVelocity)
        {
        }

        // 読み方を渡せるのは、テストで入力の状態を流さずに角速度を決めるため。
        public GyroLook(Func<InputGyroscope> deviceSource, Func<InputGyroscope, Vector3> reader)
        {
            _deviceSource = deviceSource ?? throw new ArgumentNullException(nameof(deviceSource));
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public InputGyroscope Device => _device;

        public bool EnabledDevice => _enabledDevice;

        public Vector2 ReadLookDegrees(ControlSettings settings, bool aiming, float scale, float deltaSeconds)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!(scale >= 0f) || float.IsInfinity(scale))
            {
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "gyro scale must be a non-negative finite number");
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "delta seconds must be a non-negative finite number");
            }

            // 切っている間はセンサーを止め、電池を使わない。
            if (!settings.GyroEnabled)
            {
                Release();
                return Vector2.zero;
            }

            var device = Acquire();
            if (device == null || !settings.IsGyroActive(aiming))
            {
                return Vector2.zero;
            }

            var angularVelocity = _reader(device);
            if (!IsFinite(angularVelocity))
            {
                return Vector2.zero;
            }

            return ToLookDegrees(angularVelocity, settings, scale, deltaSeconds);
        }

        // Input System の角速度はラジアン毎秒。右手系で上向きの軸まわりの正の回転は上から見て反時計回り (左を向く) なので、yaw は符号を反す。
        public static Vector2 ToLookDegrees(Vector3 angularVelocity, ControlSettings settings, float scale, float deltaSeconds)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var factor = Mathf.Rad2Deg * settings.GyroSensitivity * scale * deltaSeconds;
            var degrees = new Vector2(-angularVelocity.y * factor, angularVelocity.x * factor);
            degrees.y = settings.ApplyInvertGyroY(degrees.y);
            return degrees;
        }

        public void Release()
        {
            // 他の部品が有効にしたセンサーは止めない。
            if (_enabledDevice && _device != null && _device.added && _device.enabled)
            {
                InputSystem.DisableDevice(_device);
                Debug.Log(GyroLookLog.Released(_device.name));
            }

            _enabledDevice = false;
            _device = null;
        }

        private InputGyroscope Acquire()
        {
            var device = _deviceSource();
            if (device != null && !device.added)
            {
                device = null;
            }

            if (device != _device)
            {
                Release();
                _device = device;
            }

            if (device == null)
            {
                if (!_reportedMissing)
                {
                    Debug.Log(GyroLookLog.NoDevice());
                    _reportedMissing = true;
                }

                return null;
            }

            _reportedMissing = false;
            if (!device.enabled)
            {
                InputSystem.EnableDevice(device);
                _enabledDevice = true;
                Debug.Log(GyroLookLog.Enabled(device.name));
            }

            return device;
        }

        private static Vector3 ReadAngularVelocity(InputGyroscope device)
        {
            return device.angularVelocity.ReadValue();
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
