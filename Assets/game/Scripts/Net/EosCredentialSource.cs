using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// EOS の認証情報を環境変数と <see cref="EosCredentials.LocalFileName"/> から集める。
    /// どちらを優先するか・揃っているかの判断は <see cref="EosCredentials.Resolve"/> に任せ、ここは読むだけにする。
    /// 値そのものはログに出さない。
    /// </summary>
    public static class EosCredentialSource
    {
        /// <summary>
        /// <see cref="EosCredentials.LocalFileName"/> を探す場所。作業ディレクトリ、次に実行ファイルの隣
        /// (エディタではプロジェクトのルート) の順。
        /// </summary>
        public static IReadOnlyList<string> CandidatePaths()
        {
            var paths = new List<string>();
            AddCandidate(paths, Directory.GetCurrentDirectory());
            AddCandidate(paths, Path.GetDirectoryName(Application.dataPath));
            return paths;
        }

        /// <summary>認証情報を読む。</summary>
        /// <param name="credentials">揃ったときの認証情報。欠けていれば null。</param>
        /// <param name="missing">見つからなかった変数の名前。</param>
        /// <param name="error">ローカルファイルがあるのに読めないときの理由。読めた・無かったときは null。</param>
        /// <returns>ローカルファイルが壊れていなければ true (認証情報が欠けていても true)。</returns>
        public static bool TryLoad(out EosCredentials credentials, out IReadOnlyList<string> missing, out string error)
        {
            return TryLoad(Environment.GetEnvironmentVariable, CandidatePaths(), out credentials, out missing, out error);
        }

        /// <param name="environment">環境変数の名前から値を返す。</param>
        /// <param name="candidatePaths">ローカルファイルを探す場所。最初に見つかった 1 つだけを読む。</param>
        /// <inheritdoc cref="TryLoad(out EosCredentials, out IReadOnlyList{string}, out string)"/>
        public static bool TryLoad(
            Func<string, string> environment,
            IReadOnlyList<string> candidatePaths,
            out EosCredentials credentials,
            out IReadOnlyList<string> missing,
            out string error)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            if (candidatePaths == null)
            {
                throw new ArgumentNullException(nameof(candidatePaths));
            }

            credentials = null;
            missing = EosCredentials.VariableNames;
            IReadOnlyDictionary<string, string> localFile = null;

            foreach (var path in candidatePaths)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch (IOException e)
                {
                    error = "cannot read " + path + ": " + e.Message;
                    return false;
                }
                catch (UnauthorizedAccessException e)
                {
                    error = "cannot read " + path + ": " + e.Message;
                    return false;
                }

                if (!FlatJsonObject.TryParse(text, out localFile, out var parseError))
                {
                    error = path + " is not a JSON object of strings: " + parseError;
                    return false;
                }

                break;
            }

            credentials = EosCredentials.Resolve(environment, localFile, out missing);
            error = null;
            return true;
        }

        private static void AddCandidate(List<string> paths, string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            var path = Path.GetFullPath(Path.Combine(directory, EosCredentials.LocalFileName));
            if (!paths.Contains(path))
            {
                paths.Add(path);
            }
        }
    }
}
