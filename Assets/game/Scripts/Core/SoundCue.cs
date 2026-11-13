using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 聞いている人から見た音の合図 1 つ (種類・向き・距離)。<see cref="TryLocate"/> で、鳴った位置と聞いている人の位置と向きから作る。
    /// </summary>
    public readonly struct SoundCue
    {
        /// <summary>この距離 (m) 以上離れた音は、字幕に「遠く」と書く。</summary>
        public const float FarDistance = 40f;

        public SoundCue(SoundCueKind kind, SoundCueDirection direction, float distance)
        {
            Kind = kind;
            Direction = direction;
            Distance = distance;
        }

        public SoundCueKind Kind { get; }

        public SoundCueDirection Direction { get; }

        /// <summary>聞いている人からの距離 (m)。分からなければ NaN。</summary>
        public float Distance { get; }

        /// <summary>距離が分かっていて <see cref="FarDistance"/> 以上か。</summary>
        public bool IsFar => !float.IsNaN(Distance) && Distance >= FarDistance;

        /// <summary>
        /// listener の位置で listenerYaw (度、Unity の eulerAngles.y) を向いている人に、source で鳴った kind の音がどう聞こえるかを求める。
        /// source が null (鳴らした本人の耳元の音など) なら向きも距離も分からない合図にする。
        /// source が audibleDistance (m) より遠い (その音が聞こえない) か、値が有限でなければ false。
        /// 向きは <see cref="DamageDirection.TryScreenAngle"/> と同じく水平面で決め、決まらなければ <see cref="SoundCueDirection.None"/>。
        /// </summary>
        public static bool TryLocate(
            SoundCueKind kind,
            Vector3 listener,
            float listenerYaw,
            Vector3? source,
            float audibleDistance,
            out SoundCue cue)
        {
            if (!source.HasValue)
            {
                cue = new SoundCue(kind, SoundCueDirection.None, float.NaN);
                return true;
            }

            var distance = Vector3.Distance(listener, source.Value);
            if (float.IsNaN(distance) || float.IsInfinity(distance) || !(distance <= audibleDistance))
            {
                cue = default;
                return false;
            }

            var direction = DamageDirection.TryScreenAngle(listener, listenerYaw, source.Value, out var degrees)
                ? DirectionOf(degrees)
                : SoundCueDirection.None;
            cue = new SoundCue(kind, direction, distance);
            return true;
        }

        /// <summary>
        /// 画面の上から時計回りの角度 (度) を 8 方位にする。各方位は中心から ±22.5° の幅を持ち、境目は時計回りの次の方位に入れる。
        /// 有限でなければ <see cref="SoundCueDirection.None"/>。
        /// </summary>
        public static SoundCueDirection DirectionOf(float screenDegrees)
        {
            if (float.IsNaN(screenDegrees) || float.IsInfinity(screenDegrees))
            {
                return SoundCueDirection.None;
            }

            var wrapped = screenDegrees % 360f;
            if (wrapped < 0f)
            {
                wrapped += 360f;
            }

            var sector = (int)Math.Floor((wrapped + 22.5f) / 45f) % 8;
            return (SoundCueDirection)((int)SoundCueDirection.Front + sector);
        }
    }
}
