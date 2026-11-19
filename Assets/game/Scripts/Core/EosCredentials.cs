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

        // クラウドセーブ (Player Data Storage) を手元で暗号化する鍵。無くても対戦はできるので VariableNames には入れない。
        // 端末が違っても同じ鍵でないと読めないので、ゲームごとに 1 つ決めて配る。
        public const string EncryptionKeyVariable = "EOS_ENCRYPTION_KEY";

        // 32 バイトを 16 進で書いた長さ (EOS の Options.EncryptionKey が求める形)。
        public const int EncryptionKeyLength = 64;

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

        public string EncryptionKey { get; }

        public bool HasEncryptionKey => IsValidEncryptionKey(EncryptionKey);

        public EosAccountLoginSettings AccountLogin { get; }

        public EosCredentials(string productId, string sandboxId, string deploymentId, string clientId, string clientSecret)
            : this(productId, sandboxId, deploymentId, clientId, clientSecret, null)
        {
        }

        public EosCredentials(
            string productId, string sandboxId, string deploymentId, string clientId, string clientSecret, string encryptionKey)
            : this(productId, sandboxId, deploymentId, clientId, clientSecret, encryptionKey, null)
        {
        }

        public EosCredentials(
            string productId,
            string sandboxId,
            string deploymentId,
            string clientId,
            string clientSecret,
            string encryptionKey,
            EosAccountLoginSettings accountLogin)
        {
            ProductId = Require(productId, nameof(productId));
            SandboxId = Require(sandboxId, nameof(sandboxId));
            DeploymentId = Require(deploymentId, nameof(deploymentId));
            ClientId = Require(clientId, nameof(clientId));
            ClientSecret = Require(clientSecret, nameof(clientSecret));
            EncryptionKey = Normalize(encryptionKey);
            AccountLogin = accountLogin ?? EosAccountLoginSettings.Disabled;
        }

        public static bool IsValidEncryptionKey(string key)
        {
            if (key == null || key.Length != EncryptionKeyLength)
            {
                return false;
            }

            foreach (var c in key)
            {
                var hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex)
                {
                    return false;
                }
            }

            return true;
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
                found[ClientSecretVariable],
                ResolveOptional(environment, localFile, EncryptionKeyVariable),
                EosAccountLoginSettings.Parse(
                    ResolveOptional(environment, localFile, EosAccountLoginSettings.EpicAccountLoginVariable),
                    ResolveOptional(environment, localFile, EosAccountLoginSettings.EpicDevAuthVariable)));
        }

        // ログに出ても漏れないよう、値は書かない。
        public override string ToString()
        {
            return "EosCredentials(values hidden)";
        }

        private static string ResolveOptional(
            Func<string, string> environment, IReadOnlyDictionary<string, string> localFile, string name)
        {
            var value = Normalize(environment(name));
            if (value == null && localFile != null && localFile.TryGetValue(name, out var fromFile))
            {
                value = Normalize(fromFile);
            }

            return value;
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
