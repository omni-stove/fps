using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

// Ladder と同じく、ルールの型と名前がぶつかっても Shooter.Runtime の中から名前だけで指せるよう名前空間を分ける。
namespace Shooter.Runtime.Traversal
{
    // 位置を箱の底の真ん中にするのは、床に置いた位置からそのまま深さを測れるようにするため。
    // 回転と拡大を見ないのは、WaterBox が軸にそろった箱だけを受けるため (見た目に合わせて回すと、判定だけがずれて気付けない)。
    [DisallowMultipleComponent]
    public sealed class Water : MonoBehaviour
    {
        public static readonly Vector3 DefaultSize = new Vector3(10f, 3f, 10f);

        public const float DefaultSurfaceHeight = 2.5f;

        private static readonly List<Water> _enabled = new List<Water>();

        [SerializeField, Tooltip("水の箱の幅・高さ・奥行き (m)。この位置を底の真ん中とし、回転と拡大は見ない")]
        private Vector3 _size = DefaultSize;

        [SerializeField, Min(0f), Tooltip("箱の底から水面までの高さ (m)。箱の高さ以下にする")]
        private float _surfaceHeight = DefaultSurfaceHeight;

        private bool _warnedInvalid;

        public static IReadOnlyList<Water> Enabled => _enabled;

        public Vector3 Size
        {
            get => _size;
            set
            {
                _size = value;
                _warnedInvalid = false;
            }
        }

        public float SurfaceHeight
        {
            get => _surfaceHeight;
            set
            {
                _surfaceHeight = value;
                _warnedInvalid = false;
            }
        }

        public bool IsRegistered => _enabled.Contains(this);

        public WaterBox Box
        {
            get
            {
                if (_size.x < 0f || _size.y < 0f || _size.z < 0f)
                {
                    throw new ArgumentException($"size {_size} must not be negative", nameof(Size));
                }

                var bottom = transform.position;
                var half = new Vector3(_size.x / 2f, 0f, _size.z / 2f);
                var min = bottom - half;
                var max = bottom + half + new Vector3(0f, _size.y, 0f);
                return new WaterBox(min.ToNumerics(), max.ToNumerics(), bottom.y + _surfaceHeight);
            }
        }

        // 値を打ち込んでいる途中の不正な水で毎フレームエラーを積まないよう、知らせるのは値を変えるまで 1 度だけにする。
        public bool TryGetBox(out WaterBox box)
        {
            try
            {
                box = Box;
                return true;
            }
            catch (ArgumentException e)
            {
                if (!_warnedInvalid)
                {
                    _warnedInvalid = true;
                    Debug.LogError(WaterLog.Invalid(name, e.Message), this);
                }

                box = default;
                return false;
            }
        }

        // Edit Mode では OnEnable が走らないので、テストが直接呼べるよう公開する。
        public void Register()
        {
            if (!_enabled.Contains(this))
            {
                _enabled.Add(this);
            }
        }

        public void Unregister()
        {
            _enabled.Remove(this);
        }

        private void OnEnable()
        {
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnDrawGizmosSelected()
        {
            var bottom = transform.position;
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 1f);
            Gizmos.DrawWireCube(bottom + new Vector3(0f, _size.y / 2f, 0f), _size);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
            Gizmos.DrawCube(bottom + new Vector3(0f, _surfaceHeight, 0f), new Vector3(_size.x, 0f, _size.z));
        }
    }
}
