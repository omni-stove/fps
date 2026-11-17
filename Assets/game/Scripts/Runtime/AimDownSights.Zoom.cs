using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public sealed partial class AimDownSights
    {
        public const string CycleMagnificationActionPath = InputActionPaths.CycleMagnification;

        // シーンに置いたまま再コンパイルすると、Unity が private の値も保存し直し、null の InputAction を空のアクションとして戻してしまう (再生でシーンを読み直さないので、そのまま使われる)。
        [NonSerialized]
        private InputAction _cycleMagnificationAction;
        private bool _enabledCycleMagnificationAction;
        private int _zoomLevelIndex;
        private string _zoomWeaponId;
        private float _zoomLevelFrom;
        private float _zoomLevelBlend = 1f;

        public InputAction CycleMagnificationAction => _cycleMagnificationAction;

        public int ZoomLevelIndex => _zoomLevelIndex;

        public int ZoomLevelCount => ZoomLevels(WeaponSource?.Invoke())?.Count ?? 1;

        public float? Magnification
        {
            get
            {
                if (!IsAiming || !IsInitialized)
                {
                    return null;
                }

                var levels = ZoomLevels(WeaponSource?.Invoke());
                if (levels == null || levels.Count < 2)
                {
                    return null;
                }

                return 1f / AdsZoom.SensitivityMultiplier(_hipFieldOfView, levels[Math.Min(_zoomLevelIndex, levels.Count - 1)]);
            }
        }

        public bool CycleZoomLevel()
        {
            if (!IsAiming)
            {
                return false;
            }

            var spec = WeaponSource?.Invoke();
            var levels = ZoomLevels(spec);
            if (levels == null || levels.Count < 2)
            {
                return false;
            }

            FollowZoomWeapon(spec);
            var next = (Math.Min(_zoomLevelIndex, levels.Count - 1) + 1) % levels.Count;
            MoveToZoomLevel(next);
            Debug.Log(FormatZoomLog(next, levels.Count, levels[next]), this);
            return true;
        }

        public static string FormatZoomLog(int index, int count, float fieldOfView)
        {
            return $"{LogTag} zoom={index + 1}/{count} fov={fieldOfView.ToString("0.##", CultureInfo.InvariantCulture)}";
        }

        private float ScopeFieldOfView()
        {
            var spec = WeaponSource?.Invoke();
            var levels = ZoomLevels(spec);
            if (levels == null)
            {
                return _fallbackFieldOfView;
            }

            FollowZoomWeapon(spec);
            var target = levels[Math.Min(_zoomLevelIndex, levels.Count - 1)];
            return _zoomLevelBlend >= 1f ? target : AdsZoom.Interpolate(_zoomLevelFrom, target, _zoomLevelBlend);
        }

        private void StepZoomLevel(float deltaSeconds)
        {
            if (_zoomLevelBlend >= 1f)
            {
                return;
            }

            _zoomLevelBlend = _transitionSeconds <= 0f ? 1f : Math.Min(1f, _zoomLevelBlend + (deltaSeconds / _transitionSeconds));
        }

        // 腰だめへ戻る間に最初の段へも寄せるのは、段を即座に戻すと戻りの途中で視野角が跳ぶから。
        private void ReturnToFirstZoomLevel()
        {
            if (_zoomLevelIndex == 0)
            {
                return;
            }

            MoveToZoomLevel(0);
        }

        private void SnapToFirstZoomLevel()
        {
            _zoomLevelIndex = 0;
            _zoomLevelBlend = 1f;
        }

        private void MoveToZoomLevel(int index)
        {
            _zoomLevelFrom = ScopeFieldOfView();
            _zoomLevelIndex = index;
            _zoomLevelBlend = 0f;
        }

        // WeaponSource は呼ぶたびに Spec を作り直してよいので、同じ武器かは参照でなく Id で見る。
        private void FollowZoomWeapon(WeaponSpec spec)
        {
            var id = spec?.Id;
            if (id == _zoomWeaponId)
            {
                return;
            }

            _zoomWeaponId = id;
            SnapToFirstZoomLevel();
        }

        private static IReadOnlyList<float> ZoomLevels(WeaponSpec spec)
        {
            return spec?.Aim.ZoomLevels;
        }

        private void BindCycleMagnificationAction(InputAction action)
        {
            if (_isLive)
            {
                ReleaseCycleMagnificationAction();
            }

            _cycleMagnificationAction = action;
            if (_isLive)
            {
                AcquireCycleMagnificationAction();
            }
        }

        private void ReadCycleMagnification()
        {
            if (_cycleMagnificationAction != null && _cycleMagnificationAction.WasPressedThisFrame())
            {
                CycleZoomLevel();
            }
        }

        private InputAction FindCycleMagnificationAction(InputActionAsset actions)
        {
            var action = actions.FindAction(CycleMagnificationActionPath);
            if (action == null)
            {
                Debug.LogWarning($"{LogTag} Input Actions '{actions.name}' に '{CycleMagnificationActionPath}' が無いので、倍率は入力で切り替わりません。", this);
            }

            return action;
        }

        private void AcquireCycleMagnificationAction()
        {
            // Aim と同じく共有のアクションなので、自分が有効にしたときだけ後で無効に戻す。
            if (_cycleMagnificationAction != null && !_cycleMagnificationAction.enabled)
            {
                _cycleMagnificationAction.Enable();
                _enabledCycleMagnificationAction = true;
            }
        }

        private void ReleaseCycleMagnificationAction()
        {
            if (_enabledCycleMagnificationAction && _cycleMagnificationAction != null)
            {
                _cycleMagnificationAction.Disable();
            }

            _enabledCycleMagnificationAction = false;
        }
    }
}
