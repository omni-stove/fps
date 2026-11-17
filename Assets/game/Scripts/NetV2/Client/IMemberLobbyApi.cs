using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.NetV2
{
    /// <summary>
    /// メンバー用ロビーを呼ぶ側 (EOS の Lobby インターフェース)。<see cref="MemberLobbies"/> を SDK 抜きで動かし、
    /// テストで偽の Lobby インターフェースに差し替えられるよう、SDK の呼び出しを 1 つずつここに分ける。
    /// </summary>
    /// <remarks>
    /// 完了のコールバックは要求ごとに 1 回だけ、Tick の中か呼び出しの中で呼ぶ。成功なら null、失敗なら理由を渡す。
    /// ロビーが無い・もう入っていないときの理由は <see cref="LobbyBackendErrors.NotFound"/> にする。
    /// 使う側が捨てられた後も、出ていく要求 (<see cref="LeaveLobby"/>) は受け付ける。
    /// </remarks>
    public interface IMemberLobbyApi
    {
        /// <summary>入っているロビーで、メンバーの属性が変わった。1 つ目はロビー、2 つ目はメンバー。</summary>
        event Action<string, string> MemberUpdateReceived;

        /// <summary>
        /// 公開の検索に出さず、ID で入れるロビーを作って入る。オーナーが出たら残った人に渡す。
        /// 成功なら (ロビーの ID, null)。
        /// </summary>
        void CreateLobby(string bucketId, int maxMembers, Action<string, string> done);

        /// <summary>ロビーの公開の属性を足す・置き換える。</summary>
        void UpdateLobbyAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done);

        /// <summary>ロビーでの自分のメンバーの公開の属性を足す・置き換える。</summary>
        void UpdateMemberAttributes(string lobbyId, IReadOnlyList<LobbyAttribute> attributes, Action<string> done);

        /// <summary>ID でロビーに入る。</summary>
        void JoinLobby(string lobbyId, Action<string> done);

        void LeaveLobby(string lobbyId, Action<string> done);

        /// <summary>入っているロビーの今の中身を写す。読めなければ null と理由。</summary>
        LobbyInfo CopyLobby(string lobbyId, out string reason);

        /// <summary>入っているロビーのメンバー <paramref name="userId"/> の属性を写す。読めなければ null と理由。</summary>
        IReadOnlyList<LobbyAttribute> CopyMemberAttributes(string lobbyId, string userId, out string reason);
    }
}
