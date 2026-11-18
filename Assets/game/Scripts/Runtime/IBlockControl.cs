using System;

namespace Shooter.Runtime
{
    // 画面が知っているのは試合の参加者 Id だけなので、それで引き、試合をまたいで覚える Id への引き直しは出す側が持つ。
    public interface IBlockControl
    {
        event Action Changed;

        bool IsBlocked(int participantId);

        bool IsMuted(int participantId);

        void SetBlocked(int participantId, bool blocked);

        void SetMuted(int participantId, bool muted);
    }
}
