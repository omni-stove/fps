using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;

namespace Shooter.Server.Eos
{
    // どちらを優先するかは EosCredentials.Resolve に任せ、Unity のサーバーと同じ設定で動くようにする。
    internal static class EosCredentialSource
    {
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
