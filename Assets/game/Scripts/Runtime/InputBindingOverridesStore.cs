using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fps.Runtime
{
    /// <summary>
    /// Input Actions の割り当ての上書きを、<see cref="InputActionRebindingExtensions.SaveBindingOverridesAsJson(IInputActionCollection2)"/> の JSON で
    /// <see cref="PlayerPrefs"/> の 1 つのキーに読み書きする。上書きが 1 つも無ければキーを消す。
    /// 読めなかったときは上書きをすべて外し (既定の割り当てで遊べるように)、理由を <c>[Input]</c> 付きの警告で出す。
    /// </summary>
    public sealed class InputBindingOverridesStore
    {
        /// <summary>ゲームが使う PlayerPrefs のキー。</summary>
        public const string DefaultKey = "Fps.InputBindingOverrides";

        /// <param name="key">PlayerPrefs のキー。テストでは本物の設定を壊さないよう別のキーを渡す。</param>
        public InputBindingOverridesStore(string key = DefaultKey)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (key.Length == 0)
            {
                throw new ArgumentException("key must not be empty", nameof(key));
            }

            Key = key;
        }

        /// <summary>読み書きする PlayerPrefs のキー。</summary>
        public string Key { get; }

        /// <summary>保存した上書きがあるか。</summary>
        public bool HasSaved => PlayerPrefs.HasKey(Key);

        /// <summary>
        /// 保存した上書きを actions に入れる。前から入っていた上書きは外す。まだ保存していなければ警告を出さずに上書きを外すだけで false。
        /// 壊れていれば警告を出して上書きを外し false。読めたら true。
        /// </summary>
        public bool Load(InputActionAsset actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            if (!PlayerPrefs.HasKey(Key))
            {
                actions.RemoveAllBindingOverrides();
                return false;
            }

            // 同じキーに整数や小数が入っていると GetString は空文字を返し、壊れた値として扱う。
            var json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning($"[Input] binding overrides '{Key}' are empty or not text; using the default bindings");
                actions.RemoveAllBindingOverrides();
                return false;
            }

            try
            {
                actions.LoadBindingOverridesFromJson(json);
            }
            catch (Exception e)
            {
                // 途中まで入った上書きが残ると、どのキーが効くか分からなくなる。
                actions.RemoveAllBindingOverrides();
                Debug.LogWarning($"[Input] binding overrides '{Key}' could not be read ({e.GetType().Name}: {e.Message}); using the default bindings");
                return false;
            }

            return true;
        }

        /// <summary>actions の上書きを保存し、すぐディスクに書き出す。上書きが無ければキーを消す。</summary>
        public void Save(InputActionAsset actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            if (!HasAnyOverride(actions))
            {
                Delete();
                return;
            }

            PlayerPrefs.SetString(Key, actions.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        /// <summary>保存した上書きを消す。次の <see cref="Load"/> は既定の割り当てに戻す。</summary>
        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        private static bool HasAnyOverride(InputActionAsset actions)
        {
            foreach (var map in actions.actionMaps)
            {
                foreach (var binding in map.bindings)
                {
                    if (binding.overridePath != null || binding.overrideInteractions != null || binding.overrideProcessors != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
