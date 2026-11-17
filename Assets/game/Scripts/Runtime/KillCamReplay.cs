using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class KillCamReplay
    {
        private readonly Component _owner;

        private KillCamPlayback _playback;
        private string _killerName = string.Empty;
        private KillCamStatus? _status;
        private Transform _killerRoot;
        private float _speed = 1f;

        public KillCamReplay(Component owner)
        {
            _owner = owner != null ? owner : throw new ArgumentNullException(nameof(owner));
        }

        public bool IsPlaying => _playback != null && !_playback.IsFinished;

        public float Elapsed => IsPlaying ? _playback.Elapsed : 0f;

        public float Speed => _speed;

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
