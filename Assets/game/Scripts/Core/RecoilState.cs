using System;

namespace Fps.Core
{
    /// <summary>
    /// 撃つたびに跳ねる視線のずれ (反動) と、その戻り。1 人の射手に 1 つ持つ。
    /// <see cref="Kick"/> で武器の <see cref="WeaponSpec.RecoilPitchDegrees"/> を上へ、<see cref="WeaponSpec.RecoilYawDegrees"/> までを左右へ
    /// (左右と量は種で決める) 足し、最後に撃ってから <see cref="RecoveryDelaySeconds"/> 経つと、最後に撃った武器の
    /// <see cref="WeaponSpec.RecoilRecoveryPerSecond"/> の速さでずれを 0 へまっすぐ戻す。時間は <see cref="Tick"/> で進む。
    /// 視線の向き自体は持たず、見る側 (カメラ) が向きにずれを足して使う。
    /// </summary>
    public sealed class RecoilState
    {
        /// <summary>生成時に指定しなかったときの <see cref="RecoveryDelaySeconds"/>。</summary>
        public const float DefaultRecoveryDelaySeconds = 0.15f;

        /// <summary>たまったずれの上下・左右それぞれの上限 (度)。戻らない武器で撃ち続けても視線が裏返らないようにする。</summary>
        public const float MaxOffsetDegrees = 90f;

        private float _recoveryPerSecond;
        private float _secondsSinceKick;

        /// <param name="recoveryDelaySeconds">最後に撃ってから戻り始めるまでの秒数。0 以上の有限値。</param>
        /// <exception cref="ArgumentOutOfRangeException">recoveryDelaySeconds が負か有限でないとき。</exception>
        public RecoilState(float recoveryDelaySeconds = DefaultRecoveryDelaySeconds)
        {
            if (!(recoveryDelaySeconds >= 0f) || float.IsInfinity(recoveryDelaySeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(recoveryDelaySeconds), recoveryDelaySeconds, "recoveryDelaySeconds must be a non-negative finite number");
            }

            RecoveryDelaySeconds = recoveryDelaySeconds;
            _secondsSinceKick = recoveryDelaySeconds;
        }

        /// <summary>最後に撃ってから戻り始めるまでの秒数。</summary>
        public float RecoveryDelaySeconds { get; }

        /// <summary>視線を上へずらす角度 (度)。正で上。0 以上 <see cref="MaxOffsetDegrees"/> 以下。</summary>
        public float PitchUpDegrees { get; private set; }

        /// <summary>視線を左右へずらす角度 (度)。正で右。-<see cref="MaxOffsetDegrees"/> 以上 <see cref="MaxOffsetDegrees"/> 以下。</summary>
        public float YawDegrees { get; private set; }

        /// <summary>ずれが無いか。</summary>
        public bool IsAtRest => PitchUpDegrees == 0f && YawDegrees == 0f;

        /// <summary>
        /// 1 発分の反動を足す。上へは spec の <see cref="WeaponSpec.RecoilPitchDegrees"/>、左右へは
        /// ±<see cref="WeaponSpec.RecoilYawDegrees"/> の間で種 (playerId・shotIndex) から決めた量。
        /// 同じ種なら必ず同じ量を足す。戻りの待ちをやり直し、戻る速さをこの武器のものにする。
        /// </summary>
        /// <param name="spec">撃った武器。</param>
        /// <param name="playerId">撃ったプレイヤーの Id。</param>
        /// <param name="shotIndex">そのプレイヤーの発射番号。</param>
        /// <exception cref="ArgumentNullException">spec が null のとき。</exception>
        public void Kick(WeaponSpec spec, int playerId, int shotIndex)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var yaw = YawKick(spec, playerId, shotIndex);
            PitchUpDegrees = Math.Min(MaxOffsetDegrees, PitchUpDegrees + spec.RecoilPitchDegrees);
            YawDegrees = Math.Max(-MaxOffsetDegrees, Math.Min(MaxOffsetDegrees, YawDegrees + yaw));
            _recoveryPerSecond = spec.RecoilRecoveryPerSecond;
            _secondsSinceKick = 0f;
        }

        /// <summary>
        /// 種 (playerId・shotIndex) から決まる、1 発分の左右の反動 (度)。正で右。±<see cref="WeaponSpec.RecoilYawDegrees"/> の間。
        /// </summary>
        /// <exception cref="ArgumentNullException">spec が null のとき。</exception>
        public static float YawKick(WeaponSpec spec, int playerId, int shotIndex)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (spec.RecoilYawDegrees == 0f)
            {
                return 0f;
            }

            var unit = SpreadPattern.Sample(playerId, shotIndex, SpreadPattern.RecoilYawStream);
            return (float)(((unit * 2.0) - 1.0) * spec.RecoilYawDegrees);
        }

        /// <summary>
        /// 時間を deltaSeconds 進める。最後に撃ってから <see cref="RecoveryDelaySeconds"/> を過ぎた分だけ、ずれを 0 へまっすぐ戻す
        /// (上下と左右が同時に 0 になる)。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">deltaSeconds が負か有限でないとき。</exception>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            var waiting = Math.Max(0f, RecoveryDelaySeconds - _secondsSinceKick);
            var recovering = Math.Max(0f, deltaSeconds - waiting);
            _secondsSinceKick = Math.Min(RecoveryDelaySeconds, _secondsSinceKick + deltaSeconds);
            if (recovering <= 0f || IsAtRest || _recoveryPerSecond <= 0f)
            {
                return;
            }

            var length = Math.Sqrt((PitchUpDegrees * (double)PitchUpDegrees) + (YawDegrees * (double)YawDegrees));
            var step = recovering * (double)_recoveryPerSecond;
            if (step >= length)
            {
                Reset();
                return;
            }

            var scale = (length - step) / length;
            PitchUpDegrees = (float)(PitchUpDegrees * scale);
            YawDegrees = (float)(YawDegrees * scale);
        }

        /// <summary>ずれを 0 にし、戻りの待ちも済ませる (持ち替え・復活など、反動を引き継がないとき)。</summary>
        public void Reset()
        {
            PitchUpDegrees = 0f;
            YawDegrees = 0f;
            _secondsSinceKick = RecoveryDelaySeconds;
        }
    }
}
