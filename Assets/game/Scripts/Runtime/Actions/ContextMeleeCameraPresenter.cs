using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // 技の仮の演出。アニメや音は持たず、技の間だけ視野角を狭めてカメラを寄せる。ゲームは IContextMeleePresenter を実装したものに差し替える。
    // 覗き込み (AimDownSights) なども視野角を書くので、自分の後にほかが書いた値を新しい元の値として覚え、それに倍率を掛ける。
    // 元の値を始めた時点で固定すると、技の間に覗き込みが腰だめへ戻り終えた分を、終えたときに古い値で上書きしてしまう。
    [DisallowMultipleComponent]
    public sealed class ContextMeleeCameraPresenter : MonoBehaviour, IContextMeleePresenter
    {
        public const float DefaultZoomFactor = 0.8f;
        public const float DefaultBlendSeconds = 0.15f;

        [SerializeField, Range(0.1f, 1f), Tooltip("技の間に視野角へ掛ける倍率。小さいほど寄る")]
        private float _zoomFactor = DefaultZoomFactor;

        [SerializeField, Min(0f), Tooltip("寄せる・戻すのにかける秒数")]
        private float _blendSeconds = DefaultBlendSeconds;

        [SerializeField, Tooltip("寄せるカメラ。空なら子の Camera")]
        private Camera _camera;

        private bool _active;
        private float _weight;
        private bool _written;
        private float _baseFieldOfView;
        private float _lastWritten;

        public bool IsActive => _active;

        public float Weight => _weight;

        public float ZoomFactor
        {
            get => _zoomFactor;
            set => _zoomFactor = Mathf.Clamp(value, 0.1f, 1f);
        }

        public Camera Camera
        {
            get => _camera != null ? _camera : (_camera = GetComponentInChildren<Camera>(true));
            set => _camera = value;
        }

        public void OnStarted(GameObject attacker, Damageable target, ContextMeleeMove move)
        {
            _active = true;
        }

        public void OnResolved(GameObject attacker, Damageable target, ContextMeleeMove move, int damage, bool killed)
        {
            _active = false;
        }

        public void OnInterrupted(GameObject attacker, Damageable target, ContextMeleeMove move, ContextMeleeInterruption reason)
        {
            _active = false;
        }

        // Edit Mode では LateUpdate が走らないので、テストからも進められるよう分けておく。
        public void Step(float deltaSeconds)
        {
            var camera = Camera;
            if (camera == null)
            {
                return;
            }

            var seconds = float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) ? 0f : Mathf.Max(0f, deltaSeconds);
            var goal = _active ? 1f : 0f;
            _weight = _blendSeconds > 0f ? Mathf.MoveTowards(_weight, goal, seconds / _blendSeconds) : goal;

            if (!_written)
            {
                if (_weight <= 0f)
                {
                    return;
                }

                _baseFieldOfView = camera.fieldOfView;
            }
            else if (!Mathf.Approximately(camera.fieldOfView, _lastWritten))
            {
                _baseFieldOfView = camera.fieldOfView;
            }

            if (_weight <= 0f)
            {
                Restore(camera);
                return;
            }

            _lastWritten = _baseFieldOfView * Mathf.Lerp(1f, _zoomFactor, _weight);
            camera.fieldOfView = _lastWritten;
            _written = true;
        }

        private void LateUpdate()
        {
            Step(Time.deltaTime);
        }

        private void OnDisable()
        {
            _active = false;
            _weight = 0f;
            var camera = Camera;
            if (camera != null)
            {
                Restore(camera);
            }
        }

        private void Restore(Camera camera)
        {
            if (!_written)
            {
                return;
            }

            if (Mathf.Approximately(camera.fieldOfView, _lastWritten))
            {
                camera.fieldOfView = _baseFieldOfView;
            }

            _written = false;
        }
    }
}
