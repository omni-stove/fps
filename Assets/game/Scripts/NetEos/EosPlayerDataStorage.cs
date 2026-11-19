using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Epic.OnlineServices;
using Epic.OnlineServices.PlayerDataStorage;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetEos
{
    // ロビー・パーティと同じ EOS のユーザー (EosPlatform.LocalUserId) を使う。起動時のログインは AccountSignIn が行い、
    // Epic アカウントで入っていれば端末をまたいで同じクラウドを読み書きする。
    internal sealed class EosPlayerDataStorage : ICloudSaveBackend
    {
        // EOS_PLAYERDATASTORAGE_FILENAME_MAX_LENGTH_BYTES。名前は ASCII だけなので文字数で比べてよい。
        private const int _maxFileNameLength = 64;

        // EOS が 1 回の呼び戻しで受け渡す量の上限 (EOS_PLAYERDATASTORAGE_FILE_MAX_SIZE_BYTES より十分小さく、セーブ 1 つが数回で済む大きさ)。
        private const uint _chunkLengthBytes = 16 * 1024;

        // BOM を付けず、読み込みでは不正なバイト列を黙って置き換えずに失敗にする (FileSaveStorage と同じ)。
        private static readonly Encoding _encoding = new UTF8Encoding(false, true);

        private readonly IEosClientTransportFactory _factory;
        private bool _credentialsRead;
        private bool _checked;

        public EosPlayerDataStorage(IEosClientTransportFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public string SignedInAccount
        {
            get
            {
                var user = EosPlatform.LocalUserId;
                return user != null && EosPlatform.HasEncryptionKey ? user.ToString() : null;
            }
        }

        public int MaxFileNameLength => _maxFileNameLength;

        public void Tick()
        {
            CheckSettings();
            if (EosPlatform.Current != null)
            {
                EosPlatform.Tick();
            }
        }

        // ログインは AccountSignIn に任せ、ここでは手元だけになる理由をログに 1 回だけ出す。
        private void CheckSettings()
        {
            if (_checked)
            {
                return;
            }

            if (!_credentialsRead)
            {
                _credentialsRead = true;
                if (!_factory.TryLoadCredentials(out var credentials, out var missing, out var loadError))
                {
                    TurnOff("cannot load EOS credentials: " + loadError);
                    return;
                }

                if (credentials == null)
                {
                    TurnOff("no EOS credentials; missing " + string.Join(", ", missing ?? Array.Empty<string>()));
                    return;
                }

                if (!credentials.HasEncryptionKey)
                {
                    TurnOff(EosCredentials.EncryptionKeyVariable + " is missing or not "
                        + EosCredentials.EncryptionKeyLength + " hex characters");
                    return;
                }
            }

            // 鍵はプラットフォームを作るときにしか渡せないので、鍵より先に別の部品が作っていれば読み書きできない。
            if (EosPlatform.Current != null && !EosPlatform.HasEncryptionKey)
            {
                TurnOff("the EOS platform was created without " + EosCredentials.EncryptionKeyVariable);
                return;
            }

            if (EosPlatform.Current != null)
            {
                _checked = true;
            }
        }

        // 設定が足りないのはやり直しても直らないので、この起動の間は手元だけにする。
        private void TurnOff(string reason)
        {
            _checked = true;
            Debug.Log(CloudSaveLog.Off(reason));
        }

        public void List(Action<IReadOnlyList<string>, string> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (!TryBegin(out var storage, out var user, out var unavailable))
            {
                done(null, unavailable);
                return;
            }

            var options = new QueryFileListOptions { LocalUserId = user };
            storage.QueryFileList(ref options, null, (ref QueryFileListCallbackInfo info) =>
            {
                // まだ 1 つも書いていないユーザーは NotFound で返る。
                if (info.ResultCode == Result.NotFound)
                {
                    done(Array.Empty<string>(), null);
                    return;
                }

                if (info.ResultCode != Result.Success)
                {
                    done(null, "listing EOS player data files failed: " + info.ResultCode);
                    return;
                }

                var names = new List<string>((int)info.FileCount);
                for (uint i = 0; i < info.FileCount; i++)
                {
                    var copyOptions = new CopyFileMetadataAtIndexOptions { LocalUserId = user, Index = i };
                    var copied = storage.CopyFileMetadataAtIndex(ref copyOptions, out var metadata);
                    if (copied != Result.Success || !metadata.HasValue)
                    {
                        done(null, "reading EOS player data file metadata failed: " + copied);
                        return;
                    }

                    string name = metadata.Value.Filename;
                    if (!string.IsNullOrEmpty(name))
                    {
                        names.Add(name);
                    }
                }

                done(names, null);
            });
        }

        public void Read(string fileName, Action<string, string> done)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("fileName must not be empty", nameof(fileName));
            }

            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (!TryBegin(out var storage, out var user, out var unavailable))
            {
                done(null, unavailable);
                return;
            }

            var buffer = new MemoryStream();
            var transfer = new Transfer();
            var options = new ReadFileOptions
            {
                LocalUserId = user,
                Filename = fileName,
                ReadChunkLengthBytes = _chunkLengthBytes,
                ReadFileDataCallback = (ref ReadFileDataCallbackInfo data) =>
                {
                    var chunk = data.DataChunk;
                    if (chunk.Array != null && chunk.Count > 0)
                    {
                        buffer.Write(chunk.Array, chunk.Offset, chunk.Count);
                    }

                    return ReadResult.ContinueReading;
                },
            };

            // 始められなかったときも完了の呼び戻しが理由付きで返るので、戻り値が null でもここでは done を呼ばない。
            transfer.Request = storage.ReadFile(ref options, null, (ref ReadFileCallbackInfo info) =>
            {
                transfer.Release();
                if (info.ResultCode == Result.NotFound)
                {
                    done(null, null);
                    return;
                }

                if (info.ResultCode != Result.Success)
                {
                    done(null, "reading EOS player data file '" + fileName + "' failed: " + info.ResultCode);
                    return;
                }

                string text;
                try
                {
                    text = _encoding.GetString(buffer.ToArray());
                }
                catch (DecoderFallbackException e)
                {
                    done(null, "EOS player data file '" + fileName + "' is not UTF-8: " + e.Message);
                    return;
                }

                done(text, null);
            });
        }

        public void Write(string fileName, string text, Action<string> done)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("fileName must not be empty", nameof(fileName));
            }

            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (!TryBegin(out var storage, out var user, out var unavailable))
            {
                done(unavailable);
                return;
            }

            var bytes = _encoding.GetBytes(text);
            var offset = 0;
            var transfer = new Transfer();
            var options = new WriteFileOptions
            {
                LocalUserId = user,
                Filename = fileName,
                ChunkLengthBytes = _chunkLengthBytes,
                WriteFileDataCallback = (ref WriteFileDataCallbackInfo data, out ArraySegment<byte> outDataBuffer) =>
                {
                    var length = Math.Min((int)data.DataBufferLengthBytes, bytes.Length - offset);
                    outDataBuffer = new ArraySegment<byte>(bytes, offset, length);
                    offset += length;
                    return offset >= bytes.Length ? WriteResult.CompleteRequest : WriteResult.ContinueWriting;
                },
            };

            transfer.Request = storage.WriteFile(ref options, null, (ref WriteFileCallbackInfo info) =>
            {
                transfer.Release();
                if (info.ResultCode != Result.Success)
                {
                    done("writing EOS player data file '" + fileName + "' failed: " + info.ResultCode);
                    return;
                }

                done(null);
            });
        }

        private static bool TryBegin(out PlayerDataStorageInterface storage, out ProductUserId user, out string unavailable)
        {
            storage = null;
            user = EosPlatform.LocalUserId;
            var platform = EosPlatform.Current;
            if (platform == null || user == null)
            {
                unavailable = "not signed in to EOS";
                return false;
            }

            if (!EosPlatform.HasEncryptionKey)
            {
                unavailable = "the EOS platform was created without " + EosCredentials.EncryptionKeyVariable;
                return false;
            }

            storage = platform.GetPlayerDataStorageInterface();
            if (storage == null)
            {
                unavailable = "EOS Player Data Storage interface is unavailable";
                return false;
            }

            unavailable = null;
            return true;
        }

        // 呼び戻しは Tick の中で返るので、ReadFile・WriteFile が返した要求はそれより先にここへ入っている。
        private sealed class Transfer
        {
            public PlayerDataStorageFileTransferRequest Request { get; set; }

            public void Release()
            {
                Request?.Release();
                Request = null;
            }
        }
    }
}
