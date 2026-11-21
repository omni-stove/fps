using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class EmoteSpec
    {
        public const float DefaultDurationSeconds = 3f;

        public const bool DefaultLoops = false;

        public const bool DefaultStopsOnMove = true;

        // 調整値の JSON に出さないのは、押し戻しや坂での小さなずれで止めないための遊びで、ゲームが動きとして選ぶ値ではないため。
        // オフラインの EmoteController と専用サーバーが同じ幅で「動いた」を決めるよう、ここに 1 つだけ置く。
        public const float MoveToleranceMeters = 0.05f;

        // エモートはゲームがアニメーションを用意して初めて意味を持つので、キットは空の一覧を既定にする。
        public static IReadOnlyList<EmoteSpec> DefaultEmotes { get; } = Array.Empty<EmoteSpec>();

        public EmoteSpec(
            string id,
            string displayNameKey = null,
            float durationSeconds = DefaultDurationSeconds,
            bool loops = DefaultLoops,
            bool stopsOnMove = DefaultStopsOnMove)
        {
            Id = SpecLists.Id(id, nameof(id));
            DisplayNameKey = SpecGuard.NameKey(displayNameKey);
            DurationSeconds = SpecGuard.PositiveFinite(durationSeconds, nameof(durationSeconds));
            Loops = loops;
            StopsOnMove = stopsOnMove;
        }

        public string Id { get; }

        public string DisplayNameKey { get; }

        // 繰り返すものではアニメーションの一巡の長さで、止めるまで続ける。繰り返さないものはこの秒数で終える。
        public float DurationSeconds { get; }

        public bool Loops { get; }

        public bool StopsOnMove { get; }

        public bool IsFinishedAt(float elapsedSeconds)
        {
            return !Loops && elapsedSeconds >= DurationSeconds;
        }

        public static IReadOnlyList<EmoteSpec> UniqueById(IReadOnlyList<EmoteSpec> emotes)
        {
            if (emotes == null)
            {
                throw new ArgumentNullException(nameof(emotes));
            }

            return SpecLists.UniqueById(emotes, e => e.Id, nameof(emotes));
        }

        public static EmoteSpec Find(IReadOnlyList<EmoteSpec> emotes, string id)
        {
            if (emotes == null)
            {
                throw new ArgumentNullException(nameof(emotes));
            }

            return SpecLists.FindById(emotes, e => e.Id, id);
        }
    }
}
