using System;

namespace Shooter.NetV2
{
    /// <summary>
    /// ロビーの RTC 部屋を呼ぶ側 (EOS の Lobby・RTC・RTC Audio インターフェース)。<see cref="VoiceSession"/> を SDK 抜きで動かし、
    /// テストで偽の RTC に差し替えられるよう、SDK の呼び出しをここに分ける。
    /// </summary>
    /// <remarks>
    /// 完了のコールバックは要求ごとに 1 回だけ、<see cref="Tick"/> の中か呼び出しの中で呼ぶ。成功なら null、失敗なら理由を渡す。
    /// 知らせ (event) は部屋に入っている間だけ起こす。
    /// </remarks>
    public interface IVoiceRtc : IDisposable
    {
        /// <summary>部屋につながった (true) か、切れた (false)。2 つ目は切れた理由 (つながったときは null)。</summary>
        event Action<bool, string> ConnectionChanged;

        /// <summary>自分以外の参加者が部屋に入った (true) か、出た (false)。1 つ目は参加者の ProductUserId。</summary>
        event Action<string, bool> ParticipantChanged;

        /// <summary>参加者 (自分を含む) が話し始めた (true) か、やめた (false)。</summary>
        event Action<string, bool> SpeakingChanged;

        void Tick();

        /// <summary>
        /// メンバーとして入っているロビー <paramref name="lobbyId"/> の RTC 部屋に、自分の声を送らないで入る。
        /// 入っている間は呼ばれない。つながったら <see cref="ConnectionChanged"/> を起こす。
        /// </summary>
        void JoinRoom(string lobbyId, Action<string> done);

        /// <summary>部屋から出る。入っていなければ何もしない。出た後は、出す前の要求のコールバックを呼ばなくてよい。</summary>
        void LeaveRoom();

        void UpdateSending(bool enabled, Action<string> done);

        void UpdateReceiving(string participantId, bool enabled, Action<string> done);

        /// <summary>受ける声すべての音量。0 で無音、1 でそのまま、<see cref="VoiceSession.MaxVolume"/> で最大。</summary>
        void UpdateReceivingVolume(float volume, Action<string> done);
    }
}
