using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.Leaderboards;
using Epic.OnlineServices.Platform;
using Shooter.Core;
using Shooter.NetV2;
using UnityEngine;
// SDK の名前空間に同名の型が増えても、こちらを指すようにする
using LeaderboardEntry = Shooter.NetV2.LeaderboardEntry;
using SdkLeaderboardRecord = Epic.OnlineServices.Leaderboards.LeaderboardRecord;

namespace Shooter.NetEos
{
    // EOS は QueryLeaderboardRanks の結果を手元に 1 つだけ持つので、問い合わせが終わったその場で上位と自分の行を写し取る。
    internal sealed class EosLeaderboardQuery : ILeaderboardBackend
    {
        private readonly EosCredentials _credentials;
        private readonly string _displayName;
        private PlatformInterface _platform;
        private LeaderboardsInterface _leaderboards;
        private EosDeviceLogin _login;
        private bool _disposed;

        public EosLeaderboardQuery(EosCredentials credentials, string displayName)
        {
            _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
            _displayName = string.IsNullOrWhiteSpace(displayName) ? "shooter-player" : displayName;
        }

        private bool PlatformAlive => _platform != null && ReferenceEquals(EosPlatform.Current, _platform);

        public void Tick()
        {
            if (!_disposed && PlatformAlive)
            {
                EosPlatform.Tick();
            }
        }

        public void Login(Action<string, string> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            ThrowIfDisposed();
            if (_login != null)
            {
                throw new InvalidOperationException("already logging in");
            }

            var platform = EosPlatform.Acquire(_credentials, out var error);
            if (platform == null)
            {
                done(null, error);
                return;
            }

            var leaderboards = platform.GetLeaderboardsInterface();
            var connect = platform.GetConnectInterface();
            if (leaderboards == null || connect == null)
            {
                done(null, "EOS Leaderboards or Connect interface is unavailable");
                return;
            }

            _platform = platform;
            _leaderboards = leaderboards;

            // ロビーや通信路がもうログインしていれば、その人として引く
            var loggedIn = EosPlatform.LocalUserId;
            if (loggedIn != null)
            {
                done(loggedIn.ToString(), null);
                return;
            }

            _login = new EosDeviceLogin(connect, _displayName, SystemInfo.deviceModel);
            _login.Begin((userId, loginError) =>
            {
                _login = null;
                if (_disposed)
                {
                    return;
                }

                if (loginError != null || userId == null)
                {
                    done(null, loginError ?? "EOS login returned no user");
                    return;
                }

                done(userId.ToString(), null);
            });
        }

        public void QueryRanks(string boardId, int maxRows, string localUserId, Action<LeaderboardPage, string> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            ThrowIfDisposed();
            if (!PlatformAlive || _leaderboards == null)
            {
                done(null, "not logged in to EOS");
                return;
            }

            var local = ProductUserId.FromString(localUserId);
            if (local == null || !local.IsValid())
            {
                done(null, "'" + localUserId + "' is not a valid EOS product user id");
                return;
            }

            var options = new QueryLeaderboardRanksOptions { LeaderboardId = boardId, LocalUserId = local };
            _leaderboards.QueryLeaderboardRanks(ref options, null, (ref OnQueryLeaderboardRanksCompleteCallbackInfo info) =>
            {
                if (_disposed)
                {
                    return;
                }

                if (info.ResultCode != Result.Success)
                {
                    done(null, "querying EOS leaderboard " + boardId + " failed: " + info.ResultCode);
                    return;
                }

                if (!PlatformAlive)
                {
                    done(null, "the EOS platform was released");
                    return;
                }

                done(ReadPage(maxRows, local), null);
            });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _login?.Cancel();
            _login = null;
            _leaderboards = null;

            // プラットフォームはロビーや通信路と共有しているので、ここでは解放しない (EosPlatform がアプリの終了で解放する)
            _platform = null;
        }

        private LeaderboardPage ReadPage(int maxRows, ProductUserId local)
        {
            var countOptions = new GetLeaderboardRecordCountOptions();
            var count = _leaderboards.GetLeaderboardRecordCount(ref countOptions);
            var take = (int)Math.Min(count, (uint)Math.Max(0, maxRows));
            var top = new List<LeaderboardEntry>(take);
            for (uint i = 0; i < take; i++)
            {
                var indexOptions = new CopyLeaderboardRecordByIndexOptions { LeaderboardRecordIndex = i };
                if (_leaderboards.CopyLeaderboardRecordByIndex(ref indexOptions, out var record) == Result.Success && record.HasValue)
                {
                    top.Add(Entry(record.Value));
                }
            }

            // 上位の外でも、EOS が返した範囲 (ポータルで決めた件数) にいれば自分の順位が引ける
            LeaderboardEntry? mine = null;
            var userOptions = new CopyLeaderboardRecordByUserIdOptions { UserId = local };
            if (_leaderboards.CopyLeaderboardRecordByUserId(ref userOptions, out var own) == Result.Success && own.HasValue)
            {
                mine = Entry(own.Value);
            }

            return new LeaderboardPage(top, mine);
        }

        private static LeaderboardEntry Entry(SdkLeaderboardRecord record)
        {
            var rank = (int)Math.Min(record.Rank, (uint)int.MaxValue);
            return new LeaderboardEntry(rank, record.UserId?.ToString(), Text(record.UserDisplayName), record.Score);
        }

        private static string Text(Utf8String value)
        {
            // Utf8String の演算子を通さずに null を判定する
            if ((object)value == null)
            {
                return null;
            }

            var text = value.ToString();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(EosLeaderboardQuery));
            }
        }
    }
}
