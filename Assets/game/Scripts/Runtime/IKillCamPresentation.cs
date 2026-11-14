using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// キルカム (<see cref="DeathCamera.KillCam"/>) の演出。再生の速さと、再生している間に出す枠と文字を受け持つ。
    /// カメラを動かすのはキルカムの側 (<see cref="KillCamReplay"/>) で、ここはその上に重ねる見せ方だけを決める。
    /// <see cref="KillCamPresentations.Current"/> に入れると差し替えられる。
    /// </summary>
    public interface IKillCamPresentation
    {
        /// <summary>
        /// 再生の速さ (1 で記録どおり、0.5 で半分の速さ)。再生を始めるときに 1 度だけ読む。
        /// 0 より大きい有限値でなければ、警告を出して 1 倍で再生する。
        /// </summary>
        float PlaybackSpeed { get; }

        /// <summary>再生を始めた。枠と文字を出す。</summary>
        void Begin(KillCamShot shot);

        /// <summary>再生が進んだ。progress は 0 から 1。</summary>
        void Progress(float progress);

        /// <summary>再生を終えた。completed は最後まで再生したか (リスポーンなどで途中でやめたら false)。枠と文字を消す。</summary>
        void End(bool completed);
    }

    /// <summary><see cref="IKillCamPresentation.Begin"/> に渡す、1 回の再生の情報。</summary>
    public readonly struct KillCamShot
    {
        public KillCamShot(GameObject viewer, Camera camera, LocalizedText caption, string killerName, float durationSeconds)
        {
            Viewer = viewer;
            Camera = camera;
            Caption = caption;
            KillerName = killerName ?? string.Empty;
            DurationSeconds = durationSeconds;
        }

        /// <summary>倒されて再生を見ているプレイヤー。</summary>
        public GameObject Viewer { get; }

        /// <summary>再生を映しているカメラ。見つからなければ null。</summary>
        public Camera Camera { get; }

        /// <summary>見出し (<see cref="KillCamText.Caption"/>)。</summary>
        public LocalizedText Caption { get; }

        /// <summary>倒した相手の名前 (ログ用)。null にならない。</summary>
        public string KillerName { get; }

        /// <summary>再生するクリップの長さ (秒、速さを掛ける前)。</summary>
        public float DurationSeconds { get; }
    }
}
