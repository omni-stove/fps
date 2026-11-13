using System;
using System.Collections.Generic;
using Fps.Core;

namespace Fps.Net
{
    /// <summary>
    /// ロビーのサービス (EOS の Connect と Lobby インターフェース) を呼ぶ側。Fps.Net は EOS の SDK を参照しないので、
    /// 実装は <c>Fps.NetEos.EosLobbyBackend</c> にあり、<see cref="NetTransportFactories.EosLobby"/> から作る。
    /// </summary>
    /// <remarks>
    /// 完了のコールバックは要求ごとに 1 回だけ、<see cref="Tick"/> の中か、すぐ失敗が分かったときは呼び出しの中で呼ぶ。
    /// 成功なら (結果, null)、失敗なら (null, 理由) を渡す。<see cref="IDisposable.Dispose"/> の後はコールバックを呼ばない。
    /// </remarks>
    public interface ILobbyBackend : IDisposable
    {
        /// <summary>参加中のロビーのメンバーが変わった。引数は最新の状態。</summary>
        event Action<LobbyInfo> MembersChanged;

        /// <summary>参加中のロビーから外れた (オーナーが閉じた・追い出された・接続が切れた)。引数はロビー ID と理由。</summary>
        event Action<string, string> Removed;

        /// <summary>
        /// ログインが切れた (認証の期限切れを更新できなかった・サービスから切断された)。引数は理由。
        /// 参加中のロビーがあれば、先に <see cref="Removed"/> を呼ぶ。この後は <see cref="Login"/> からやり直せる。
        /// </summary>
        event Action<string> LoggedOut;

        /// <summary>SDK の処理とコールバックを進める。毎フレーム呼ぶ。</summary>
        void Tick();

        /// <summary>
        /// Device ID でログインする。成功したら自分の ProductUserId を渡す。
        /// ログイン中・ログイン済みでは呼ばない。<see cref="LoggedOut"/> の後はまた呼んでよい。
        /// </summary>
        void Login(Action<string, string> done);

        /// <summary>ロビーを作り、自分がオーナーとして入る。属性を付けられなければロビーを閉じて失敗にする。</summary>
        /// <param name="bucketId">バケット ID。</param>
        /// <param name="maxMembers">最大人数。</param>
        /// <param name="attributes">公開する属性。無ければ空。</param>
        /// <param name="done">完了したときに呼ぶ。</param>
        void CreateLobby(string bucketId, int maxMembers, IReadOnlyList<LobbyAttribute> attributes, Action<LobbyInfo, string> done);

        /// <summary>オーナーとして参加中のロビーの属性を、同じキーは上書きして足す。成功なら null を渡す。</summary>
        void UpdateAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done);

        /// <summary><paramref name="bucketId"/> の公開ロビーを探す。見つからなければ空の一覧を渡す。</summary>
        void SearchLobbies(string bucketId, int maxResults, Action<IReadOnlyList<LobbyInfo>, string> done);

        /// <summary>ID を指定してロビーに入る。</summary>
        void JoinLobby(string lobbyId, Action<LobbyInfo, string> done);

        /// <summary>ロビーから出る。<paramref name="destroy"/> ならオーナーとしてロビーごと閉じる。成功なら null を渡す。</summary>
        void LeaveLobby(string lobbyId, bool destroy, Action<string> done);
    }
}
