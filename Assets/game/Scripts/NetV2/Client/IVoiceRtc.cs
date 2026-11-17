using System;

namespace Shooter.NetV2
{
    // VoiceSession を SDK 抜きで動かし、テストで偽の RTC に差し替えられるよう、SDK の呼び出しをここに分ける。
    public interface IVoiceRtc : IDisposable
    {
        event Action<bool, string> ConnectionChanged;

        event Action<string, bool> ParticipantChanged;

        event Action<string, bool> SpeakingChanged;

        void Tick();

        void JoinRoom(string lobbyId, Action<string> done);

        void LeaveRoom();

        void UpdateSending(bool enabled, Action<string> done);

        void UpdateReceiving(string participantId, bool enabled, Action<string> done);

        void UpdateReceivingVolume(float volume, Action<string> done);
    }
}
