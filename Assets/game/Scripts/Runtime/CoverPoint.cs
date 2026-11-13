using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 敵が隠れに行く遮蔽点の印。この GameObject の位置が隠れるときに立つ足元、前 (+Z) が壁へ向かう向き
    /// (この向きから来る弾を壁が遮る)。壁の高さの区分 <see cref="Height"/> で、低い壁ならしゃがんで隠れ、
    /// 高い壁なら立ったまま隠れて <see cref="PeekPosition"/> へ横に出て覗く。
    /// シーンで共有する <see cref="CoverPointRegistry"/> が集めて <see cref="CoverPointSelector"/> に渡す。
    /// 動かない地形に置く前提で、Play 中に動かしても選ぶ位置は変わらない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoverPoint : MonoBehaviour
    {
        [SerializeField, Tooltip("壁の高さの区分。Low ならしゃがんで隠れ立って覗く、High なら立って隠れ PeekOffset へ出て覗く")]
        private CoverHeight _height = CoverHeight.Low;

        [SerializeField, Tooltip("覗くときに立つ位置。この GameObject のローカル座標でのずれ。0 ならその場で立ち上がる")]
        private Vector3 _peekOffset = Vector3.zero;

        public CoverHeight Height
        {
            get => _height;
            set => _height = value;
        }

        /// <summary>覗くときに立つ位置の、この GameObject のローカル座標でのずれ。</summary>
        public Vector3 PeekOffset
        {
            get => _peekOffset;
            set => _peekOffset = value;
        }

        /// <summary>隠れるときに立つ足元 (ワールド座標)。</summary>
        public Vector3 Position => transform.position;

        /// <summary>この点から壁へ向かう向き (ワールド座標)。</summary>
        public Vector3 Facing => transform.forward;

        /// <summary>覗くときに立つ足元 (ワールド座標)。</summary>
        public Vector3 PeekPosition => transform.position + (transform.rotation * _peekOffset);

        /// <summary>覗くときに立つ位置を、ワールド座標のずれで設定する (シーン生成スクリプト用)。</summary>
        public void SetPeekOffsetWorld(Vector3 worldOffset)
        {
            _peekOffset = Quaternion.Inverse(transform.rotation) * worldOffset;
        }

        /// <summary>
        /// 判断に渡す遮蔽点を作る。position は足元 (NavMesh に寄せた位置などを渡せる)。
        /// 向きに水平の成分が無い (真上・真下を向いている) か座標が有限でなければ false を返し、error に理由を入れる。
        /// </summary>
        public bool TryToCore(Vector3 position, out Fps.Core.CoverPoint point, out string error)
        {
            try
            {
                point = new Fps.Core.CoverPoint(position.ToNumerics(), Facing.ToNumerics(), _height);
                error = null;
                return true;
            }
            catch (ArgumentOutOfRangeException e)
            {
                point = default;
                error = e.Message;
                return false;
            }
        }

        private void OnDrawGizmos()
        {
            var position = transform.position;
            Gizmos.color = _height == CoverHeight.Low ? Color.cyan : Color.blue;
            Gizmos.DrawWireSphere(position + (Vector3.up * 0.1f), 0.25f);
            Gizmos.DrawLine(position + (Vector3.up * 0.1f), position + (Vector3.up * 0.1f) + Facing);

            var peek = PeekPosition;
            if ((peek - position).sqrMagnitude > 1e-6f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(position + (Vector3.up * 0.1f), peek + (Vector3.up * 0.1f));
                Gizmos.DrawWireSphere(peek + (Vector3.up * 0.1f), 0.15f);
            }
        }
    }
}
