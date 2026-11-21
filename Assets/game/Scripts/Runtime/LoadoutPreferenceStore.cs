using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 試合の側 (GameBootstrap・NetV2LoadoutChooser) は Load と Save で選んでいる組だけを扱い、組の一覧はメニューが LoadPresets と SavePresets で扱う。
    public sealed class LoadoutPreferenceStore
    {
        public const string DefaultKey = "Shooter.LoadoutChoice";

        public const string LogTag = "[Loadout]";

        private readonly LoadoutPresetSpec _spec;

        // キーを渡せるのは、テストで本物の選択を壊さないよう別のキーを使うため。spec が null なら KitTuning.LoadoutPresets を読む。
        public LoadoutPreferenceStore(string key = DefaultKey, LoadoutPresetSpec spec = null)
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
            _spec = spec;
        }

        public string Key { get; }

        // GameContent を差し替えたときに上限も変わるよう、渡されていなければ使うたびに引く
        public LoadoutPresetSpec Spec => _spec ?? KitTuning.LoadoutPresets;

        // 選んでいる組の選択。能力を保存していない (前の版の) 選択は AbilityIds が null で返る
        public LoadoutChoice Load()
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                return null;
            }

            return LoadPresets().Active?.Choice;
        }

        // 保存が無いか読めないときは空の一覧を返すので、呼ぶ側は最初の組を足してから保存する
        public LoadoutPresetBook LoadPresets()
        {
            var spec = Spec;
            if (!PlayerPrefs.HasKey(Key))
            {
                return new LoadoutPresetBook(spec);
            }

            var book = LoadoutPreference.FromJson(PlayerPrefs.GetString(Key, string.Empty), spec, out var problem, out var dropped);
            if (book == null)
            {
                Debug.LogWarning(LoadoutPreferenceLog.Unreadable(Key, problem));
                return new LoadoutPresetBook(spec);
            }

            if (dropped > 0)
            {
                Debug.LogWarning(LoadoutPreferenceLog.PresetsDropped(Key, dropped, spec.MaxPresets));
            }

            return book;
        }

        // 選んでいる組の中身を替える。組がまだ無ければ、名前の無い最初の組として足す
        public void Save(LoadoutChoice choice)
        {
            LoadoutPresetBook.RequireSavable(choice);
            var book = LoadPresets();
            if (book.ActiveIndex == LoadoutPresetBook.NoPreset)
            {
                book.Add(string.Empty, choice);
            }
            else
            {
                book.Replace(book.ActiveIndex, choice);
            }

            SavePresets(book);
        }

        public void SavePresets(LoadoutPresetBook book)
        {
            PlayerPrefs.SetString(Key, LoadoutPreference.ToJson(book));
            PlayerPrefs.Save();
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
