using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// キルカム (<see cref="DeathCamera.KillCam"/>) の演出。再生の速さと、再生に合わせた見せ方を受け持つ。
    /// カメラを動かすのはキルカムの側 (<see cref="KillCamReplay"/>) で、枠と文字は UI が <see cref="GameSignals.KillCam"/> から出す。
    /// <see cref="KillCamPresentations.Current"/> に入れると差し替えられる。
    /// </summary>
    public interface IKillCamPresentation
    {
        /// <summary>
        /// 再生の速さ (1 で記録どおり、0.5 で半分の速さ)。再生を始めるときに 1 度だけ読む。
        /// 0 より大きい有限値でなければ、警告を出して 1 倍で再生する。
        /// </summary>
        float PlaybackSpeed { get; }

        /// <summary>再生を始めた。</summary>
        void Begin(KillCamShot shot);

        /// <summary>再生が進んだ。progress は 0 から 1。</summary>
        void Progress(float progress);

        /// <summary>再生を終えた。completed は最後まで再生したか (リスポーンなどで途中でやめたら false)。</summary>
        void End(bool completed);
    }

    /// <summary><see cref="IKillCamPresentation.Begin"/> に渡す、1 回の再生の情報。</summary>
    public readonly struct KillCamShot
    {
        public KillCamShot(GameObject viewer, Camera camera, string killerName, string weaponId, float durationSeconds)
        {
            Viewer = viewer;
            Camera = camera;
            KillerName = killerName ?? string.Empty;
            WeaponId = weaponId ?? string.Empty;
            DurationSeconds = durationSeconds;
        }

        /// <summary>倒されて再生を見ているプレイヤー。</summary>
        public GameObject Viewer { get; }

        /// <summary>再生を映しているカメラ。見つからなければ null。</summary>
        public Camera Camera { get; }

        /// <summary>倒した相手の名前。null にならない。</summary>
        public string KillerName { get; }

        public string WeaponId { get; }

        /// <summary>再生するクリップの長さ (秒、速さを掛ける前)。</summary>
        public float DurationSeconds { get; }
    }
}
