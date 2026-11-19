using System;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Shooter.Editor
{
    // Unity の既定 (Recompile And Continue Playing) では、Play 中に読み直すと保存できない private の値 (判定役や購読) だけが空に戻り、
    // 結び付いたはずの部品が毎フレーム例外を出す。部品ごとに直すときりがないので、Play の間は読み直しを止めて、Play を止めてから読み直す。
    // 利用者の設定 (EditorPrefs) は書き換えず、この Editor の中だけで止める。
    public sealed class PlayModeReloadLock
    {
        private readonly Action _lock;
        private readonly Action _unlock;

        public PlayModeReloadLock(Action lockReload, Action unlockReload)
        {
            _lock = lockReload ?? throw new ArgumentNullException(nameof(lockReload));
            _unlock = unlockReload ?? throw new ArgumentNullException(nameof(unlockReload));
        }

        public bool IsHeld { get; private set; }

        public bool HasDeferred { get; private set; }

        public void Handle(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    Hold();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                case PlayModeStateChange.EnteredEditMode:
                    Release();
                    break;
            }
        }

        public void Hold()
        {
            if (IsHeld)
            {
                return;
            }

            _lock();
            IsHeld = true;
            HasDeferred = false;
        }

        public void Release()
        {
            if (!IsHeld)
            {
                return;
            }

            IsHeld = false;
            _unlock();
        }

        // 1 回の Play で何度コンパイルしても、知らせるのは最初の 1 回だけにする。
        public bool NoteCompiled()
        {
            if (!IsHeld || HasDeferred)
            {
                return false;
            }

            HasDeferred = true;
            return true;
        }
    }

    [InitializeOnLoad]
    internal static class PlayModeReloadLockHook
    {
        private static readonly PlayModeReloadLock _lock =
            new PlayModeReloadLock(EditorApplication.LockReloadAssemblies, EditorApplication.UnlockReloadAssemblies);

        static PlayModeReloadLockHook()
        {
            EditorApplication.playModeStateChanged += _lock.Handle;
            CompilationPipeline.compilationFinished += HandleCompilationFinished;

            // この部品が入る前に Play 中のまま読み直されたときも、次の読み直しからは止める。
            if (EditorApplication.isPlaying)
            {
                _lock.Hold();
            }
        }

        private static void HandleCompilationFinished(object context)
        {
            if (_lock.NoteCompiled())
            {
                Debug.Log(PlayModeReloadLog.Deferred());
            }
        }
    }
}
