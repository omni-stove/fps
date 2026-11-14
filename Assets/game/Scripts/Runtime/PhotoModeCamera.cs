using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // カメラの借り方・戻し方・壁で止める動かし方は死亡カメラの自由カメラと同じなので、DeathCamView を共有して二重に持たない
    public sealed class PhotoModeCamera
    {
        public const string LogTag = "[PhotoMode]";

        private readonly Component _target;
        private readonly PhotoModeSpec _spec;
        private readonly DeathCamView _view;

        private Vector3 _center;
        private float _savedFieldOfView;
        private float _fieldOfView;
        private float _roll;
        private bool _active;

        public PhotoModeCamera(Component target, PhotoModeSpec spec)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            _target = target;
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));

            // ポーズ画面が止めている共有の Move・Next・Previous を DeathCamView が有効にすると、ポーズを閉じて戻したものを
            // 抜けるときに止め直してしまう。入力は PhotoModeView が複製したアクションから渡す
            _view = new DeathCamView(target, "give the photo mode a player that has a camera under its body", false);
            _view.Preempted += HandlePreempted;
        }

        public Component Target => _target;

        public PhotoModeSpec Spec => _spec;

        public bool IsActive => _active;

        public Camera Camera => _active ? _view.Camera : null;

        public Vector3 Center => _center;

        public float FieldOfView => _fieldOfView;

        public float Roll => _roll;

        public bool Begin()
        {
            if (_active)
            {
                return true;
            }

            if (_target == null)
            {
                Debug.LogWarning(LogTag + " the player to photograph is gone");
                return false;
            }

            var eye = PlayerDeathCamera.EyeOf(_target.gameObject);
            if (!_view.TryBegin(eye, _target.transform.eulerAngles.y))
            {
                Debug.LogWarning(LogTag + " " + _target.name + "'s camera is in use (the death camera); try again after it ends", _target);
                return false;
            }

            var camera = _view.Camera;
            if (camera == null)
            {
                _view.Restore(true);
                return false;
            }

            _active = true;
            _center = eye;

            // DeathCamView.Begin で覗き込みが止まり、視野角が腰だめに戻ってから覚える
            _savedFieldOfView = camera.fieldOfView;
            _fieldOfView = _spec.ClampFieldOfView(_savedFieldOfView);
            _roll = 0f;
            camera.fieldOfView = _fieldOfView;

            _view.LimitFree(eye, _spec.Radius);
            _view.Frame(null, _spec.StartHeight, _spec.StartBackDistance);
            Debug.Log(LogTag + " began on " + _target.name + " around " + DeathCamView.FormatPosition(eye)
                + " radius=" + _spec.Radius.ToString("F1", CultureInfo.InvariantCulture)
                + " fov=" + _fieldOfView.ToString("F0", CultureInfo.InvariantCulture), _target);
            return true;
        }

        public void Advance(Vector2 move, Vector2 lookDegrees, int zoomInSteps, float rollInput, bool resetLens, float deltaSeconds)
        {
            if (!_active)
            {
                return;
            }

            var camera = _view.Camera;
            if (camera == null)
            {
                return;
            }

            var seconds = float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ? 0f : Mathf.Max(0f, deltaSeconds);
            if (resetLens)
            {
                _fieldOfView = _spec.ClampFieldOfView(_savedFieldOfView);
                _roll = 0f;
                Debug.Log(LogTag + " lens reset fov=" + _fieldOfView.ToString("F0", CultureInfo.InvariantCulture), _target);
            }
            else
            {
                var fieldOfView = _spec.StepFieldOfView(_fieldOfView, zoomInSteps);
                if (!Mathf.Approximately(fieldOfView, _fieldOfView))
                {
                    _fieldOfView = fieldOfView;
                    Debug.Log(LogTag + " fov=" + _fieldOfView.ToString("F0", CultureInfo.InvariantCulture), _target);
                }

                _roll = _spec.StepRoll(_roll, rollInput, seconds);
            }

            camera.fieldOfView = _fieldOfView;
            _view.FreeRoll = _roll;
            _view.MoveFree(move, lookDegrees, _spec.MoveSpeed, seconds);
        }

        public void End()
        {
            if (!_active)
            {
                return;
            }

            _active = false;
            var camera = _view.Camera;
            if (camera != null)
            {
                camera.fieldOfView = _savedFieldOfView;
            }

            _view.Restore(true);
            if (_target != null)
            {
                Debug.Log(LogTag + " ended on " + _target.name, _target);
            }
        }

        // オンラインでは倒されても時間が進んでいて、死亡カメラ (NetDeathCamera) が同じカメラを借りに来る
        private void HandlePreempted()
        {
            if (!_active)
            {
                return;
            }

            _active = false;
            var camera = _view.Camera;
            if (camera != null)
            {
                camera.fieldOfView = _savedFieldOfView;
            }

            Debug.Log(LogTag + " the death camera took over " + (_target != null ? _target.name : "the player") + "'s camera", _target);
        }
    }
}
