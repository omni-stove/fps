using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // リポジトリは公開なので認証情報はコミットせず、環境変数か git 管理外の LocalFileName から読む。
    public sealed class EosCredentials
    {
        public const string ProductIdVariable = "EOS_PRODUCT_ID";
        public const string SandboxIdVariable = "EOS_SANDBOX_ID";
        public const string DeploymentIdVariable = "EOS_DEPLOYMENT_ID";
        public const string ClientIdVariable = "EOS_CLIENT_ID";
        public const string ClientSecretVariable = "EOS_CLIENT_SECRET";

        public const string LocalFileName = "eos.local.json";

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

        // ログに出ても漏れないよう、値は書かない。
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
