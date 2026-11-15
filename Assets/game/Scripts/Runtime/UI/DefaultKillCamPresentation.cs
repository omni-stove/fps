using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 既定のキルカムの演出。記録どおりの速さで再生し、再生している間は既定の HUD (<see cref="Hud"/>) の <see cref="KillCamView"/> に枠と見出しを出す。
    /// 既定の HUD が無い (差し替えた HUD・HUD の無いシーン) ときは何も出さずに再生させる。
    /// シーンを読み込む前 (エディターでは読み込み時にも) に <see cref="KillCamPresentations.UseDefault"/> で登録する。
    /// </summary>
    public sealed class DefaultKillCamPresentation : IKillCamPresentation
    {
        private KillCamView _view;

        public float PlaybackSpeed => 1f;

        /// <summary>今枠を出している HUD のビュー。出していなければ null。</summary>
        public KillCamView View => _view != null ? _view : null;

        public void Begin(KillCamShot shot)
        {
            Close();
            _view = GameUi.FindHud() is Hud hud ? hud.KillCam : null;
            if (_view != null)
            {
                _view.Show(shot.Caption);
            }
        }

        public void Progress(float progress)
        {
            if (_view != null)
            {
                _view.SetProgress(progress);
            }
        }

        public void End(bool completed)
        {
            Close();
        }

        private void Close()
        {
            if (_view != null)
            {
                _view.Hide();
            }

            _view = null;
        }

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            KillCamPresentations.UseDefault(new DefaultKillCamPresentation());
        }
    }
}
