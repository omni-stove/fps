using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ゲームごとに変える調整値のうち、Resources の JSON (<see cref="TuningJson"/> の形) から読むもの。
    /// <list type="bullet">
    /// <item>グレネード: <see cref="GrenadeResourcePath"/> (<see cref="Grenade"/>)。オフラインの <see cref="GrenadeThrower"/> とネット対戦が使う。</item>
    /// <item>印: <see cref="PingResourcePath"/> (<see cref="Ping"/>)。オフラインの <see cref="PingController"/> とネット対戦が使う。</item>
    /// </list>
    /// ファイルが無ければ Spec の既定値 (<see cref="GrenadeSpec.Default"/>・<see cref="PingSpec.Default"/>) で動く。
    /// 壊れている・値が不正なら、ファイル名と項目名を添えたエラーを出して既定値で続ける。
    /// 初めて使うときに読み、Play の間は持ち続ける (Play の開始時に読み直す)。
    /// </summary>
    public static class KitTuning
    {
        public const string LogTag = "[Tuning]";

        /// <summary>グレネードの調整値を置く Resources のパス (拡張子なし)。</summary>
        public const string GrenadeResourcePath = "Tuning/grenade";

        /// <summary>印の調整値を置く Resources のパス (拡張子なし)。</summary>
        public const string PingResourcePath = "Tuning/ping";

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static GrenadeSpec _grenade;
        private static PingSpec _ping;

        /// <summary>グレネードの調整値。<see cref="GrenadeResourcePath"/> が無いか読めなければ <see cref="GrenadeSpec.Default"/>。</summary>
        public static GrenadeSpec Grenade => _grenade ?? (_grenade = Load(GrenadeResourcePath, TuningJson.ParseGrenade, GrenadeSpec.Default));

        /// <summary>印の調整値。<see cref="PingResourcePath"/> が無いか読めなければ <see cref="PingSpec.Default"/>。</summary>
        public static PingSpec Ping => _ping ?? (_ping = Load(PingResourcePath, TuningJson.ParsePing, PingSpec.Default));

        /// <summary>読んだ値を捨て、次に使うときに Resources から読み直させる。</summary>
        public static void Reload()
        {
            _grenade = null;
            _ping = null;
        }

        /// <summary>
        /// text を parse で読む。text が null (ファイルが無い) なら fallback を返す。
        /// 読めなければ sourceName と項目名を添えたエラーを出して fallback を返す。Resources を使わずに試すときに呼ぶ。
        /// </summary>
        /// <exception cref="ArgumentNullException">parse か fallback が null。</exception>
        public static T Read<T>(string text, string sourceName, Func<string, string, T> parse, T fallback)
            where T : class
        {
            if (parse == null)
            {
                throw new ArgumentNullException(nameof(parse));
            }

            if (fallback == null)
            {
                throw new ArgumentNullException(nameof(fallback));
            }

            if (text == null)
            {
                return fallback;
            }

            try
            {
                return parse(text, sourceName);
            }
            catch (TuningJsonException e)
            {
                Debug.LogError($"{LogTag} {e.Message}; using the default values instead");
                return fallback;
            }
        }

        private static T Load<T>(string path, Func<string, string, T> parse, T fallback)
            where T : class
        {
            var asset = Resources.Load<TextAsset>(path);
            var spec = Read(asset != null ? asset.text : null, $"Resources/{path}.json", parse, fallback);
            if (asset != null)
            {
                Debug.Log($"{LogTag} read Resources/{path}.json");
                Resources.UnloadAsset(asset);
            }

            return spec;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Reload();
        }
    }
}
