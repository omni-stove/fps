using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// Play の開始時 (シーンを読む前) に <see cref="ProgressionRecorder"/> を <see cref="FileSaveStorage.CreateDefault"/> の置き場所で作って
    /// <see cref="ProgressionRecorder.Install"/> し、<see cref="Recorder"/> に置く。メニュー・ロードアウト・持ち物は
    /// <see cref="IsUnlocked"/> を「使えるか」の述語として受け取る。
    /// </summary>
    public static class ProgressionBootstrap
    {
        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static ProgressionRecorder _recorder;

        /// <summary>記録している <see cref="ProgressionRecorder"/>。Play の外 (Edit Mode のテストなど) で <see cref="Install(ProgressionRecorder)"/> していなければ null。</summary>
        public static ProgressionRecorder Recorder => _recorder;

        /// <summary>
        /// 今のプロフィールのレベルで id が使えるか。<see cref="Recorder"/> が無ければ全部使える。
        /// 述語として渡す先が呼ぶたびに今のプロフィールを引くので、試合中にレベルが上がればその場で使えるようになる。
        /// </summary>
        /// <exception cref="ArgumentException">id が null か空。</exception>
        public static bool IsUnlocked(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            return _recorder == null || _recorder.IsUnlocked(id);
        }

        /// <summary>storage に保存する記録係を作って <see cref="Install(ProgressionRecorder)"/> する。</summary>
        /// <exception cref="ArgumentNullException">storage が null。</exception>
        public static ProgressionRecorder Install(ISaveStorage storage)
        {
            if (storage == null)
            {
                throw new ArgumentNullException(nameof(storage));
            }

            return Install(new ProgressionRecorder(storage));
        }

        /// <summary>前の記録係を外し、recorder を始めて <see cref="Recorder"/> に置く。既に置いているものなら何もしない。</summary>
        /// <exception cref="ArgumentNullException">recorder が null。</exception>
        public static ProgressionRecorder Install(ProgressionRecorder recorder)
        {
            if (recorder == null)
            {
                throw new ArgumentNullException(nameof(recorder));
            }

            if (_recorder == recorder)
            {
                return recorder;
            }

            Uninstall();
            recorder.Install();
            _recorder = recorder;
            return recorder;
        }

        /// <summary>記録を止めて <see cref="Recorder"/> を空にする。置いていなければ何もしない。</summary>
        public static void Uninstall()
        {
            var recorder = _recorder;
            _recorder = null;
            recorder?.Uninstall();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            // CombatEvents も同じ時点で購読を空にするので、前の Play の記録係は外すだけで手放せる。
            Uninstall();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallOnPlay()
        {
            Install(FileSaveStorage.CreateDefault());
        }
    }
}
