using System;
using System.Collections.Generic;
using Fps.Core;

namespace Fps.NetV2
{
    /// <summary>
    /// クイックマッチがサーバーのロビーを探すのに使う、ロビーのサービス (EOS の Connect と Lobby インターフェース) の口。
    /// Fps.NetV2.Client は EOS の SDK を参照しないので、実装は <c>Fps.NetEos.EosLobbyBackend</c> にあり、
    /// <see cref="QuickMatchSearch"/> に渡す。
    /// </summary>
    /// <remarks>
    /// 完了のコールバックは要求ごとに 1 回だけ、<see cref="Tick"/> の中か、すぐ失敗が分かったときは呼び出しの中で呼ぶ。
    /// 成功なら (結果, null)、失敗なら (null, 理由) を渡す。<see cref="IDisposable.Dispose"/> の後はコールバックを呼ばない。
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
    }
}
