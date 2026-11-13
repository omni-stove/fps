using System;

namespace Fps.Core
{
    /// <summary>
    /// 出現点に置いた拾える物が、拾われてから再び出るまでの時間。時間は <see cref="Tick"/> で外から進める。
    /// 最初は出ている (<see cref="IsAvailable"/>)。
    /// </summary>
    public sealed class PickupRespawn
    {
        private float _remaining;

        /// <param name="respawnSeconds">拾われてから再び出るまでの秒数。0 以上の有限値。0 なら拾われたら二度と出ない。</param>
        public PickupRespawn(float respawnSeconds)
        {
            if (!(respawnSeconds >= 0f) || float.IsInfinity(respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(respawnSeconds), respawnSeconds, "respawnSeconds must be a non-negative finite number");
            }

            RespawnSeconds = respawnSeconds;
            IsAvailable = true;
        }

        public float RespawnSeconds { get; }

        /// <summary>再び出るなら true (<see cref="RespawnSeconds"/> が 0 より大きい)。</summary>
        public bool Respawns => RespawnSeconds > 0f;

        /// <summary>今出ていて拾えるなら true。</summary>
        public bool IsAvailable { get; private set; }

        /// <summary>再び出るまでの残り秒数。出ているときと、もう出ないときは null。</summary>
        public float? RemainingSeconds => !IsAvailable && Respawns ? _remaining : (float?)null;

        /// <summary>拾われたことにする。出ていなければ何もせず false。</summary>
        public bool Collect()
        {
            if (!IsAvailable)
            {
                return false;
            }

            IsAvailable = false;
            _remaining = RespawnSeconds;
            return true;
        }

        /// <summary>時間を deltaSeconds 進め、この呼び出しで再び出たら true。</summary>
        /// <param name="deltaSeconds">0 以上の有限値。</param>
        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (IsAvailable || !Respawns)
            {
                return false;
            }

            _remaining -= deltaSeconds;
            if (_remaining > 0f)
            {
                return false;
            }

            _remaining = 0f;
            IsAvailable = true;
            return true;
        }
    }
}
