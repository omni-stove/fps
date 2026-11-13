using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 陣営ごとの出現点。<see cref="ModeRunnerBase"/> は倒れた人を、自分の陣営のこの点のうち敵からいちばん離れた所
    /// (<see cref="SideSpawnSelector"/>) に戻す。位置は Transform の位置 (足元)、向きは Transform の向き。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SideSpawnPoint : MonoBehaviour
    {
        [SerializeField, Min(0), Tooltip("この点から出る陣営の ID")]
        private int _sideId;

        /// <summary>この点から出る陣営。</summary>
        public Side Side
        {
            get => new Side(Mathf.Max(0, _sideId));
            set => _sideId = value.Id;
        }

        /// <summary>出現する足元の位置。</summary>
        public Vector3 Position => transform.position;

        /// <summary>出現したときの向き。</summary>
        public Quaternion Rotation => transform.rotation;

        /// <summary>シーンにある有効な出現点をすべて返す (順番は決まっていない)。</summary>
        public static SideSpawnPoint[] FindAll()
        {
            return FindObjectsByType<SideSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.HSVToRGB(Mathf.Repeat(_sideId * 0.37f, 1f), 0.8f, 1f);
            Gizmos.DrawWireSphere(transform.position + (Vector3.up * 0.1f), 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward);
        }
    }
}
