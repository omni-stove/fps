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

        /// <summary>SDK の処理とコールバックを進める。毎フレーム呼ぶ。</summary>
        void Tick();

        /// <summary>Device ID でログインする。成功したら自分の ProductUserId を渡す。</summary>
        void Login(Action<string, string> done);

        /// <summary>ロビーを作り、自分がオーナーとして入る。</summary>
        void CreateLobby(string bucketId, int maxMembers, Action<LobbyInfo, string> done);

        /// <summary><paramref name="bucketId"/> の公開ロビーを探す。見つからなければ空の一覧を渡す。</summary>
        void SearchLobbies(string bucketId, int maxResults, Action<IReadOnlyList<LobbyInfo>, string> done);

        /// <summary>ID を指定してロビーに入る。</summary>
        void JoinLobby(string lobbyId, Action<LobbyInfo, string> done);

        /// <summary>ロビーから出る。<paramref name="destroy"/> ならオーナーとしてロビーごと閉じる。成功なら null を渡す。</summary>
        void LeaveLobby(string lobbyId, bool destroy, Action<string> done);
    }
}
