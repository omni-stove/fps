using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.NetV2
{
    /// <summary>
    /// クイックマッチがサーバーのロビーを探すのに使う、ロビーのサービス (EOS の Connect と Lobby インターフェース) の口。
    /// パーティ (メンバー用ロビー) と、試合のロビーにメンバーとして入る口も持つ。
    /// Shooter.NetV2.Client は EOS の SDK を参照しないので、実装は <c>Shooter.NetEos.EosLobbyBackend</c> にあり、
    /// <see cref="QuickMatchSearch"/> に渡す。
    /// </summary>
    /// <remarks>
    /// 完了のコールバックは要求ごとに 1 回だけ、<see cref="Tick"/> の中か、すぐ失敗が分かったときは呼び出しの中で呼ぶ。
    /// 成功なら (結果, null)、失敗なら (null, 理由) を渡す。結果の無い要求は、成功なら null、失敗なら理由を渡す。
    /// まだ使えない要求は理由に <see cref="LobbyBackendErrors.NotSupported"/> を渡す。
    /// <see cref="IDisposable.Dispose"/> の後はコールバックを呼ばない。
    /// </remarks>
    public interface ILobbyBackend : IDisposable
    {
        /// <summary>ログインが切れた (認証の期限切れを更新できなかった・サービスから切断された)。引数は理由。</summary>
        event Action<string> LoggedOut;

        /// <summary>SDK の処理とコールバックを進める。毎フレーム呼ぶ。</summary>
        void Tick();

        /// <summary>Device ID でログインする。成功したら自分の ProductUserId を渡す。ログイン中・ログイン済みでは呼ばない。</summary>
        void Login(Action<string, string> done);

        /// <summary><paramref name="bucketId"/> の公開ロビーを探す。見つからなければ空の一覧を渡す。</summary>
        void SearchLobbies(string bucketId, int maxResults, Action<IReadOnlyList<LobbyInfo>, string> done);

        /// <summary>
        /// 自分がオーナーのメンバー用ロビー (パーティ) を <paramref name="bucketId"/> に作って入る。成功したら作ったロビーを渡す。
        /// </summary>
        /// <param name="bucketId">ロビーを置くバケット。</param>
        /// <param name="maxMembers">入れる最大人数 (1 以上)。</param>
        /// <param name="attributes">ロビーに付ける公開の属性。無ければ空。</param>
        /// <param name="done">完了のコールバック。成功なら (ロビー, null)、失敗なら (null, 理由)。</param>
        void CreateMemberLobby(
            string bucketId, int maxMembers, IReadOnlyList<LobbyAttribute> attributes, Action<LobbyInfo, string> done);

        /// <summary>メンバー用ロビー <paramref name="lobbyId"/> に入る。成功したら入ったロビーを渡す。</summary>
        void JoinMemberLobby(string lobbyId, Action<LobbyInfo, string> done);

        /// <summary>メンバー用ロビー <paramref name="lobbyId"/> から出る。オーナーが出たときに誰に渡すかはサービスに任せる。</summary>
        void LeaveMemberLobby(string lobbyId, Action<string> done);

        /// <summary>
        /// 入っているメンバー用ロビー <paramref name="lobbyId"/> の今の状態 (オーナー・メンバー・属性) を読む。
        /// 自分がもうメンバーでない・ロビーが無くなったときは (null, null) を渡す。
        /// </summary>
        void ReadMemberLobby(string lobbyId, Action<LobbyInfo, string> done);

        /// <summary>
        /// ロビー <paramref name="lobbyId"/> での自分のメンバーの属性を <paramref name="attributes"/> で上書きする
        /// (同じキーは置き換え、無いキーはそのまま残す)。
        /// </summary>
        void SetMemberAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done);

        /// <summary>ロビー <paramref name="lobbyId"/> のメンバー <paramref name="userId"/> の属性を読む。無ければ空の一覧を渡す。</summary>
        void ReadMemberAttributes(string lobbyId, string userId, Action<IReadOnlyList<LobbyAttribute>, string> done);

        /// <summary>
        /// 試合 (サーバー) のロビー <paramref name="lobbyId"/> にメンバーとして入る。成功したら入ったロビーを渡す。
        /// 試合にはこれとは別に <see cref="ServerLobby.ConnectOptions"/> でつなぐ。
        /// ロビーのボイスの部屋には入らない。話すときは、入った後に <see cref="IVoiceSession.Join"/> で入る。
        /// </summary>
        void JoinMatchLobby(string lobbyId, Action<LobbyInfo, string> done);

        /// <summary>試合のロビー <paramref name="lobbyId"/> から出る。</summary>
        void LeaveMatchLobby(string lobbyId, Action<string> done);
    }
}
