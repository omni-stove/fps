using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.Runtime
{
    public sealed class InputBindingOverridesStore
    {
        public const string DefaultKey = "Shooter.InputBindingOverrides";

        // キーを渡せるのは、テストで本物の設定を壊さないよう別のキーを使うため。
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

        public string Key { get; }

        public bool HasSaved => PlayerPrefs.HasKey(Key);

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
