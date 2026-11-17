using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.NetV2
{
    // Shooter.NetV2.Client は EOS の SDK を参照しないので、実装は Shooter.NetEos.EosLobbyBackend に置き、この口で QuickMatchSearch に渡す。
    public interface ILobbyBackend : IDisposable
    {
        event Action<string> LoggedOut;

        void Tick();

        void Login(Action<string, string> done);

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
