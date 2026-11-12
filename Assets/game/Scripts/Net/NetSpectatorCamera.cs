using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// シーンに置いた観戦用のカメラ (NetTest では見下ろしのカメラ)。自分のプレイヤーの一人称視点が無い間
    /// (専用サーバー以外のサーバーや接続前) だけ表示し、<see cref="NetPlayer"/> が一人称視点を有効にしたら止め、
    /// 一人称視点が無くなったら戻す。同じ GameObject の <see cref="AudioListener"/> も一緒に切り替える。
    /// 専用サーバーでは <see cref="HeadlessPresentation"/> が止めたカメラを再び有効にしない。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class NetSpectatorCamera : MonoBehaviour
    {
        private static readonly List<NetSpectatorCamera> _active = new List<NetSpectatorCamera>();
        private static int _localViewCount;

        private Camera _camera;
        private AudioListener _listener;

        /// <summary>いま有効な一人称視点の数。通常は自分のプレイヤーの分の 0 か 1。</summary>
        public static int LocalViewCount => _localViewCount;

        /// <summary>観戦用のカメラを表示すべきか (一人称視点が 1 つも無いか)。</summary>
        public static bool IsSpectating => _localViewCount == 0;

        /// <summary>一人称視点が有効になったことを知らせ、観戦用のカメラを止める。</summary>
        public static void AddLocalView()
        {
            _localViewCount++;
            ApplyAll();
        }

        /// <summary>一人称視点が無くなったことを知らせる。すべて無くなれば観戦用のカメラを戻す。</summary>
        public static void RemoveLocalView()
        {
            if (_localViewCount <= 0)
            {
                Debug.LogWarning("[Net] NetSpectatorCamera.RemoveLocalView called without a matching AddLocalView");
                return;
            }

            _localViewCount--;
            ApplyAll();
        }

        /// <summary>
        /// 観戦用のカメラを表示するか。一人称視点が無く、描画を止める専用サーバーでないときだけ。
        /// </summary>
        public static bool ShouldShow(int localViewCount, bool isDedicatedServer)
        {
            return localViewCount <= 0 && !NetStartup.ShouldDisablePresentation(isDedicatedServer);
        }

        // ドメインの再読み込みを切った Play Mode でも、前回の数え残しを持ち越さない
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _localViewCount = 0;
            _active.Clear();
        }

        private static void ApplyAll()
        {
            foreach (var spectator in _active)
            {
                if (spectator != null)
                {
                    spectator.Apply();
                }
            }
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _listener = GetComponent<AudioListener>();
        }

        private void OnEnable()
        {
            if (!_active.Contains(this))
            {
                _active.Add(this);
            }

            Apply();
        }

        private void OnDisable()
        {
            _active.Remove(this);
        }

        private void Apply()
        {
            var show = ShouldShow(_localViewCount, NetStartup.IsDedicatedServerBuild);
            if (_camera != null)
            {
                _camera.enabled = show;
            }

            if (_listener != null)
            {
                _listener.enabled = show;
            }
        }
    }
}
