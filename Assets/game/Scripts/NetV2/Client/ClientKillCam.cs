using System;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 再生の状態は Status に出すだけにして GameSignals へは出さないのは、このアセンブリが Unity の Runtime を参照できないため。
    public sealed class ClientKillCam
    {
        private readonly int _localPlayerId;
        private readonly float _speed;
        private readonly ClientPlayerNames _names;
        private KillCamPlayback _playback;
        private int _killedBy = -1;
        private string _killedWith = string.Empty;
        private int _newestTick = -1;
        private bool _seenDead;

        public ClientKillCam(int localPlayerId, float speed = 1f, ClientPlayerNames names = null)
        {
            if (!KillCamPlayback.IsValidSpeed(speed))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must be positive and finite");
            }

            _localPlayerId = localPlayerId;
            _speed = speed;
            _names = names ?? new ClientPlayerNames();
        }

        public KillCamStatus? Status { get; private set; }

        public bool IsPlaying => _playback != null;

        public int? KillerId { get; private set; }

        // クリップより先に同じキルの Killed が届くので、倒した武器はそこから覚えておく
        public void Apply(KilledMessage killed)
        {
            if (killed == null)
            {
                throw new ArgumentNullException(nameof(killed));
            }

            if (killed.VictimId != _localPlayerId)
            {
                return;
            }

            _killedBy = killed.KillerId;
            _killedWith = killed.WeaponId ?? string.Empty;
        }

        public bool Apply(KillCamClipMessage clip)
        {
            if (clip == null)
            {
                throw new ArgumentNullException(nameof(clip));
            }

            if (clip.VictimId != _localPlayerId)
            {
                return false;
            }

            var playback = new KillCamPlayback(clip.ToClip());
            if (playback.IsFinished)
            {
                return Stop();
            }

            var weaponId = _killedBy == clip.KillerId ? _killedWith : string.Empty;
            _playback = playback;
            _seenDead = false;
            KillerId = clip.KillerId;
            return SetStatus(new KillCamStatus(_names.NameOf(clip.KillerId), weaponId, playback.Clip.Duration, 0f, _speed));
        }

        // 送り直しの遅れで前の tick のスナップショット (まだ生きている) が後から届いても止めないよう、倒れた姿を一度見てから生き返ったときに止める
        public bool Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (snapshot.Tick <= _newestTick)
            {
                return false;
            }

            _newestTick = snapshot.Tick;
            if (_playback == null)
            {
                return false;
            }

            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (row.PlayerId != _localPlayerId)
                {
                    continue;
                }

                var dead = row.Health <= 0 && !row.Downed.IsDowned;
                if (dead)
                {
                    _seenDead = true;
                    return false;
                }

                return _seenDead && Stop();
            }

            return false;
        }

        public bool Update(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be zero or more and finite");
            }

            if (_playback == null)
            {
                return false;
            }

            if (_playback.Advance(deltaSeconds, _speed))
            {
                return Stop();
            }

            return SetStatus(Status.Value.At(_playback.Elapsed));
        }

        public bool TryGetView(out Vector3 eye, out float yaw)
        {
            if (_playback == null)
            {
                eye = default;
                yaw = 0f;
                return false;
            }

            var pose = _playback.Pose;
            eye = pose.Eye;
            yaw = pose.Yaw;
            return true;
        }

        public bool Stop()
        {
            _playback = null;
            _seenDead = false;
            KillerId = null;
            return SetStatus(null);
        }

        public bool Reset()
        {
            _killedBy = -1;
            _killedWith = string.Empty;
            _newestTick = -1;
            return Stop();
        }

        private bool SetStatus(KillCamStatus? status)
        {
            if (Status == status)
            {
                return false;
            }

            Status = status;
            return true;
        }
    }
}
