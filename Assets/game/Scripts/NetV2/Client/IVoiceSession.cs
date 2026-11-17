using System;
using System.Collections.Generic;

namespace Shooter.NetV2
{
    // Shooter.NetV2.Client は EOS の SDK を参照しないので、実装は Shooter.NetEos.EosVoiceSession に置き、IEosClientTransportFactory.CreateVoiceSession で作る。
    public interface IVoiceSession : IDisposable
    {
        event Action<IReadOnlyList<string>> SpeakersChanged;

        event Action<VoiceSessionState> StateChanged;

        VoiceSessionState State { get; }

        string LobbyId { get; }

        IReadOnlyList<string> Speakers { get; }

        bool WantsSending { get; }

        bool IsSending { get; }

        void Tick();

        void Join(string lobbyId, Action<string> done);

        void Leave();

        void SetSending(bool sending);

        void SetReceiving(string participantId, bool receiving);

        void SetVolume(float volume);
    }
}
