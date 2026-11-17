using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// キルカム (<see cref="DeathCamera.KillCam"/>) の再生役。倒した相手の倒す直前の数秒 (<see cref="KillCamClip"/>) を、
    /// その時々の相手の目から肩越し (<see cref="DeathCamView.FrameAlly"/>) にカメラを置いて再生する。
    /// 記録が無ければ再生せずに終わり、持ち主 (オフラインの <see cref="PlayerDeathCamera"/>・ネット対戦の NetDeathCamera) はすぐ倒した相手を映す (<see cref="DeathCamera.Killer"/>)。
    /// 再生の速さは持ち主の設定 (<see cref="PlayerDeathCamera.KillCamSpeed"/>) を始めるときに受ける。
    /// 再生している間は <see cref="GameSignals.KillCam"/> に今の再生 (<see cref="KillCamStatus"/>) を出し、終えたら消す。枠と文字は UI が決める。
    /// 再生はカメラだけのもので、リスポーンまでの待ちは止めない。
    /// </summary>
    public sealed class KillCamReplay
    {
        private readonly Component _owner;

        private KillCamPlayback _playback;
        private string _killerName = string.Empty;
        private KillCamStatus? _status;
        private Transform _killerRoot;
        private float _speed = 1f;

        /// <param name="owner">倒されて再生を見るプレイヤー。ログの名前に使う。</param>
        /// <exception cref="ArgumentNullException">owner が null。</exception>
        public KillCamReplay(Component owner)
        {
            _owner = owner != null ? owner : throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>いま再生しているか。</summary>
        public bool IsPlaying => _playback != null && !_playback.IsFinished;

        /// <summary>再生しているクリップの先頭からの位置 (秒)。再生していなければ 0。</summary>
        public float Elapsed => IsPlaying ? _playback.Elapsed : 0f;

        /// <summary>再生の速さ。始めるときに受けた値。</summary>
        public float Speed => _speed;

        /// <summary>
        /// clip の再生を始め、カメラをクリップの先頭へ置く。clip が空なら再生せずに false (記録が無いログを出す)。
        /// 再生中に呼ぶと前の再生を途中でやめてから始める。
        /// </summary>
        /// <param name="clip">倒した相手の倒す直前の数秒。null は空とみなす。</param>
        /// <param name="killerName">倒した相手の名前。</param>
        /// <param name="weaponId">倒した武器の Id。分からなければ空文字か null。</param>
        /// <param name="killerRoot">倒した相手の本体。カメラを壁で縮める判定でその体を除く。無ければ null。</param>
        /// <param name="view">借りているカメラ。</param>
        /// <param name="speed">再生の速さ (1 で記録どおり)。<see cref="KillCamPlayback.IsValidSpeed"/> でなければ警告を出して 1 倍で再生する。</param>
        public bool Begin(KillCamClip clip, string killerName, string weaponId, Transform killerRoot, DeathCamView view, float speed)
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

            _speed = ValidSpeed(speed);
            Debug.Log(KillCamLog.Replaying(_owner.name, _killerName, _playback.Clip.Duration, _speed), _owner);
            Frame(view);
            Publish(new KillCamStatus(_killerName, weaponId, _playback.Clip.Duration, 0f, _speed));
            return true;
        }

        /// <summary>
        /// 再生を deltaSeconds 秒 (速さを掛ける前) 進めてカメラを置く。まだ再生中なら true。
        /// この呼び出しで最後に着いたら、終わったログを出し false (呼び出し側はこのフレームから倒した相手を映す)。
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
                Publish(_status.Value.At(_status.Value.DurationSeconds));
                Debug.Log(KillCamLog.Finished(_owner.name, _killerName), _owner);
                Clear();
                return false;
            }

            Frame(view);
            Publish(_status.Value.At(_playback.Elapsed));
            return true;
        }

        /// <summary>再生中なら途中でやめ、reason を添えたログを出す。再生していなければ何もしない。</summary>
        public void Stop(string reason)
        {
            if (!IsPlaying)
            {
                Clear();
                return;
            }

            Debug.Log(KillCamLog.Stopped(_owner.name, _killerName, reason), _owner);
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

        private float ValidSpeed(float speed)
        {
            if (KillCamPlayback.IsValidSpeed(speed))
            {
                return speed;
            }

            Debug.LogWarning(KillCamLog.InvalidSpeed(_owner.name, speed), _owner);
            return 1f;
        }

        private void Publish(KillCamStatus status)
        {
            _status = status;
            GameSignals.KillCam.Publish(status);
        }

        private void Clear()
        {
            // 別の持ち主が後から出した再生まで消さないよう、自分が出した値のときだけ消す
            if (_status.HasValue)
            {
                GameSignals.KillCam.ClearIfCurrent(_status);
            }

            _status = null;
            _playback = null;
            _killerRoot = null;
            _speed = 1f;
        }
    }
}
