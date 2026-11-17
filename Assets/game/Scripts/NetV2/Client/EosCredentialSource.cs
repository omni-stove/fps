using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;

namespace Shooter.NetV2
{
    // 認証情報の値そのものはログに出さない。
    public static class EosCredentialSource
    {
        public static IReadOnlyList<string> CandidatePaths(params string[] directories)
        {
            if (directories == null)
            {
                throw new ArgumentNullException(nameof(directories));
            }

            var paths = new List<string>();
            foreach (var directory in directories)
            {
                if (string.IsNullOrEmpty(directory))
                {
                    continue;
                }

                var path = Path.GetFullPath(Path.Combine(directory, EosCredentials.LocalFileName));
                if (!paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

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
    }
}
