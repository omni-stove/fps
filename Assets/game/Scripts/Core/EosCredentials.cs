using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// EOS のプラットフォームを作るのに要る認証情報。リポジトリは公開なのでコミットせず、
    /// 環境変数 (<see cref="VariableNames"/>) か git 管理外の <see cref="LocalFileName"/> から読む。
    /// </summary>
    public sealed class EosCredentials
    {
        public const string ProductIdVariable = "EOS_PRODUCT_ID";
        public const string SandboxIdVariable = "EOS_SANDBOX_ID";
        public const string DeploymentIdVariable = "EOS_DEPLOYMENT_ID";
        public const string ClientIdVariable = "EOS_CLIENT_ID";
        public const string ClientSecretVariable = "EOS_CLIENT_SECRET";

        /// <summary>
        /// ローカル用の認証情報ファイル。キーは環境変数と同じ名前で、値はすべて文字列の JSON オブジェクト。
        /// .gitignore に入れてあり、コミットされない。
        /// </summary>
        public const string LocalFileName = "eos.local.json";

        /// <summary>認証情報を読む環境変数の名前。GitHub の Secrets にも同じ名前で登録する。</summary>
        public static IReadOnlyList<string> VariableNames { get; } = new[]
        {
            ProductIdVariable,
            SandboxIdVariable,
            DeploymentIdVariable,
            ClientIdVariable,
            ClientSecretVariable,
        };

        public string ProductId { get; }

        public string SandboxId { get; }

        public string DeploymentId { get; }

        public string ClientId { get; }

        public string ClientSecret { get; }

        public EosCredentials(string productId, string sandboxId, string deploymentId, string clientId, string clientSecret)
        {
            ProductId = Require(productId, nameof(productId));
            SandboxId = Require(sandboxId, nameof(sandboxId));
            DeploymentId = Require(deploymentId, nameof(deploymentId));
            ClientId = Require(clientId, nameof(clientId));
            ClientSecret = Require(clientSecret, nameof(clientSecret));
        }

        /// <summary>
        /// 環境変数とローカルファイルから認証情報を組み立てる。キーごとに環境変数を優先し、空白だけの値は無いものとみなす。
        /// </summary>
        /// <param name="environment">環境変数の名前から値を返す (無ければ null)。</param>
        /// <param name="localFile"><see cref="LocalFileName"/> の中身。ファイルが無ければ null。</param>
        /// <param name="missing">どこにも見つからなかった変数の名前。揃っていれば空。</param>
        /// <returns>5 つすべて揃えば認証情報、1 つでも欠ければ null。</returns>
        public static EosCredentials Resolve(
            Func<string, string> environment,
            IReadOnlyDictionary<string, string> localFile,
            out IReadOnlyList<string> missing)
        {
            if (environment == null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            var found = new Dictionary<string, string>(StringComparer.Ordinal);
            var absent = new List<string>();
            foreach (var name in VariableNames)
            {
                var value = Normalize(environment(name));
                if (value == null && localFile != null && localFile.TryGetValue(name, out var fromFile))
                {
                    value = Normalize(fromFile);
                }

                if (value == null)
                {
                    absent.Add(name);
                }
                else
                {
                    found.Add(name, value);
                }
            }

            missing = absent;
            if (absent.Count > 0)
            {
                return null;
            }

            return new EosCredentials(
                found[ProductIdVariable],
                found[SandboxIdVariable],
                found[DeploymentIdVariable],
                found[ClientIdVariable],
                found[ClientSecretVariable]);
        }

        /// <summary>ログに出ても漏れないよう、値は書かない。</summary>
        public override string ToString()
        {
            return "EosCredentials(values hidden)";
        }

        private static string Normalize(string value)
        {
            if (value == null)
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }

        private static string Require(string value, string paramName)
        {
            var normalized = Normalize(value);
            if (normalized == null)
            {
                throw new ArgumentException("must not be null or blank", paramName);
            }

            return normalized;
        }
    }
}
