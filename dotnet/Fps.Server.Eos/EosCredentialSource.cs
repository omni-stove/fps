using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;

namespace Fps.Server.Eos
{
    /// <summary>
    /// Unity の <c>Fps.Net.EosCredentialSource</c> と同じく、環境変数と <see cref="EosCredentials.LocalFileName"/> から読む。
    /// どちらを優先するかは <see cref="EosCredentials.Resolve"/> に任せ、Unity のサーバーと同じ設定で動くようにする。
    /// </summary>
    internal static class EosCredentialSource
    {
        /// <param name="environment">環境変数の名前から値を返す。</param>
        /// <param name="searchRoots">ローカルファイルを探すフォルダ。最初に見つかった 1 つだけを読む。</param>
        /// <param name="credentials">揃ったときの認証情報。欠けていれば null。</param>
        /// <param name="missing">見つからなかった変数の名前。</param>
        /// <param name="error">ローカルファイルがあるのに読めないときの理由。</param>
        /// <returns>ローカルファイルが壊れていなければ true (認証情報が欠けていても true)。</returns>
        public static bool TryLoad(
            Func<string, string> environment,
            IReadOnlyList<string> searchRoots,
            out EosCredentials credentials,
            out IReadOnlyList<string> missing,
            out string error)
        {
            ArgumentNullException.ThrowIfNull(environment);
            ArgumentNullException.ThrowIfNull(searchRoots);

            credentials = null;
            missing = EosCredentials.VariableNames;
            IReadOnlyDictionary<string, string> localFile = null;

            foreach (var root in searchRoots)
            {
                var path = Path.Combine(root, EosCredentials.LocalFileName);
                if (!File.Exists(path))
                {
                    continue;
                }

                string text;
                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
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
    }
}
