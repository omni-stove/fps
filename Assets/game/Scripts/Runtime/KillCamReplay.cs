using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// キルカム (<see cref="DeathCamera.KillCam"/>) の再生役。倒した相手の倒す直前の数秒 (<see cref="KillCamClip"/>) を、
    /// その時々の相手の目から肩越し (<see cref="DeathCamView.FrameAlly"/>) にカメラを置いて再生する。
    /// 記録が無ければ再生せずに終わり、持ち主 (オフラインの <see cref="PlayerDeathCamera"/>・ネット対戦の NetDeathCamera) はすぐ倒した相手を映す (<see cref="DeathCamera.Killer"/>)。
    /// 演出 (速さ・枠・文字) は始めるときの <see cref="KillCamPresentations.Current"/> に任せ、演出が例外を投げてもログに出して再生は続ける。
    /// 再生はカメラだけのもので、リスポーンまでの待ちは止めない。
    /// </summary>
    public sealed class KillCamReplay
    {
        private readonly Component _owner;

        private KillCamPlayback _playback;
        private IKillCamPresentation _presentation;
        private string _killerName = string.Empty;
        private Transform _killerRoot;
        private float _speed = 1f;

        /// <param name="owner">倒されて再生を見るプレイヤー。ログの名前と、演出に渡す見ている人に使う。</param>
        /// <exception cref="ArgumentNullException">owner が null。</exception>
        public KillCamReplay(Component owner)
        {
            _owner = owner != null ? owner : throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>いま再生しているか。</summary>
        public bool IsPlaying => _playback != null && !_playback.IsFinished;

        /// <summary>再生しているクリップの先頭からの位置 (秒)。再生していなければ 0。</summary>
        public float Elapsed => IsPlaying ? _playback.Elapsed : 0f;

        /// <summary>再生の速さ。始めるときに演出から読んだ値。</summary>
        public float Speed => _speed;

        /// <summary>
        /// clip の再生を始め、カメラをクリップの先頭へ置く。clip が空なら再生せずに false (記録が無いログを出す)。
        /// 再生中に呼ぶと前の再生を途中でやめてから始める。
        /// </summary>
        /// <param name="clip">倒した相手の倒す直前の数秒。null は空とみなす。</param>
        /// <param name="killerName">倒した相手の名前 (ログ用)。</param>
        /// <param name="caption">演出に出させる見出し。</param>
        /// <param name="killerRoot">倒した相手の本体。カメラを壁で縮める判定でその体を除く。無ければ null。</param>
        /// <param name="view">借りているカメラ。</param>
        public bool Begin(KillCamClip clip, string killerName, LocalizedText caption, Transform killerRoot, DeathCamView view)
        {
            Stop("restarted");
            _killerName = killerName ?? string.Empty;
            _killerRoot = killerRoot;
            _playback = new KillCamPlayback(clip);
            if (_playback.IsFinished)
            {
                _playback = null;
                Debug.Log(KillCamLog.NoRecord(_owner.name, _killerName), _owner);
                return false;
            }

            _presentation = KillCamPresentations.Current;
            _speed = ReadSpeed(_presentation);
            Debug.Log(KillCamLog.Replaying(_owner.name, _killerName, _playback.Clip.Duration, _speed), _owner);
            var shot = new KillCamShot(_owner.gameObject, view != null ? view.Camera : null, caption, _killerName, _playback.Clip.Duration);
            Notify(p => p.Begin(shot));
            Frame(view);
            return true;
        }

        /// <summary>
        /// 再生を deltaSeconds 秒 (速さを掛ける前) 進めてカメラを置く。まだ再生中なら true。
        /// この呼び出しで最後に着いたら、演出を閉じて終わったログを出し false (呼び出し側はこのフレームから倒した相手を映す)。
        /// 再生していなければ何もせず false。
        /// </summary>
        public bool Advance(DeathCamView view, float deltaSeconds)
        {
            if (!IsPlaying)
            {
                return false;
            }

            var delta = deltaSeconds >= 0f && !float.IsInfinity(deltaSeconds) ? deltaSeconds : 0f;
            if (_playback.Advance(delta, _speed))
            {
                Notify(p => p.Progress(1f));
                Notify(p => p.End(true));
                Debug.Log(KillCamLog.Finished(_owner.name, _killerName), _owner);
                Clear();
                return false;
            }

            Frame(view);
            var progress = _playback.Progress;
            Notify(p => p.Progress(progress));
            return true;
        }

        /// <summary>再生中なら途中でやめ、演出を閉じて reason を添えたログを出す。再生していなければ何もしない。</summary>
        public void Stop(string reason)
        {
            if (!IsPlaying)
            {
                Clear();
                return;
            }

            Debug.Log(KillCamLog.Stopped(_owner.name, _killerName, reason), _owner);
            Notify(p => p.End(false));
            Clear();
        }

        private void Frame(DeathCamView view)
        {
            if (view == null)
            {
                return;
            }

            var pose = _playback.Pose;
            // 再生中に相手が破棄されたら、破棄済みの Transform でなく null を渡して判定に使わせない
            var root = _killerRoot != null ? _killerRoot : null;
            view.FrameAlly(pose.Eye.ToUnity(), pose.Yaw, root);
        }

        private float ReadSpeed(IKillCamPresentation presentation)
        {
            float speed;
            try
            {
                speed = presentation.PlaybackSpeed;
            }
            catch (Exception e)
            {
                Debug.LogException(e, _owner);
                return 1f;
            }

            if (KillCamPlayback.IsValidSpeed(speed))
            {
                return speed;
            }

            Debug.LogWarning(KillCamLog.InvalidSpeed(presentation.GetType().Name, speed), _owner);
            return 1f;
        }

        // 演出は差し替えられるので、例外で観戦のカメラ (と戻し) を止めない
        private void Notify(Action<IKillCamPresentation> call)
        {
            if (_presentation == null)
            {
                return;
            }

            try
            {
                call(_presentation);
            }
            catch (Exception e)
            {
                Debug.LogException(e, _owner);
            }
        }

        private void Clear()
        {
            _playback = null;
            _presentation = null;
            _killerRoot = null;
            _speed = 1f;
        }
    }
}
