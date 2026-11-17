using System;
using System.Collections.Generic;

namespace Shooter.NetV2
{
    /// <summary>
    /// 試合のボイスチャット。<see cref="ILobbyBackend.JoinMatchLobby"/> で入った試合のロビーの RTC 部屋で話す。
    /// Shooter.NetV2.Client は EOS の SDK を参照しないので、実装は <c>Shooter.NetEos.EosVoiceSession</c> にあり、
    /// <c>IEosClientTransportFactory.CreateVoiceSession</c> で作る。
    /// </summary>
    /// <remarks>
    /// 送る・受ける・音量は、望む値を覚えておき、部屋につながっている間にサービスへ反映する。
    /// つながる前に決めた値も、つながり直したときも、そのとき望んでいる値で反映し直す。
    /// 知らせは <see cref="Tick"/> の中か、呼び出しの中で起こす。<see cref="IDisposable.Dispose"/> の後は起こさない。
    /// </remarks>
    public interface IVoiceSession : IDisposable
    {
        /// <summary>話している人が変わった。引数は話している人の ProductUserId (受けるのを切った人は含まない)。</summary>
        event Action<IReadOnlyList<string>> SpeakersChanged;

        /// <summary><see cref="State"/> が変わった。</summary>
        event Action<VoiceSessionState> StateChanged;

        VoiceSessionState State { get; }

        /// <summary>入っている (入ろうとしている) 試合のロビー。入っていなければ null。</summary>
        string LobbyId { get; }

        /// <summary>話している人の ProductUserId。受けるのを切った人は含まない。</summary>
        IReadOnlyList<string> Speakers { get; }

        /// <summary>自分の声を送ることを望んでいるか (<see cref="SetSending"/> で決めた値)。</summary>
        bool WantsSending { get; }

        /// <summary>サービスが自分の声を送っているか。部屋につながっていなければ false。</summary>
        bool IsSending { get; }

        /// <summary>SDK の処理とコールバックを進める。毎フレーム呼ぶ。</summary>
        void Tick();

        /// <summary>
        /// 試合のロビー <paramref name="lobbyId"/> の RTC 部屋に入る。ロビーには先にメンバーとして入っておく。
        /// 部屋に入る要求が通れば null、通らなければ理由を 1 回だけ渡す。つながったかは <see cref="State"/> で分かる。
        /// 返事の前に <see cref="Leave"/> したら呼ばない。入っている間は呼ばない (先に <see cref="Leave"/> する)。
        /// </summary>
        void Join(string lobbyId, Action<string> done);

        /// <summary>部屋から出る。入っていなければ何もしない。送る・受ける・音量の望む値は次に入るときのために残す。</summary>
        void Leave();

        /// <summary>自分の声を送るか (押している間だけ話すなら、押したときに true、離したときに false)。</summary>
        void SetSending(bool sending);

        /// <summary>参加者 <paramref name="participantId"/> の声を受けるか。部屋にまだいない人にも決めておける。</summary>
        void SetReceiving(string participantId, bool receiving);

        /// <summary>受ける声の音量。0 で無音、1 でそのまま、<see cref="VoiceSession.MaxVolume"/> で最大。範囲の外は丸める。</summary>
        void SetVolume(float volume);
    }
}
