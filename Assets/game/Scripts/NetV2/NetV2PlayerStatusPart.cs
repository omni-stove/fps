using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.NetV2
{
    // サーバーが持ち主の欄で送るスタミナ・よろけ・制圧を、オフラインの部品と同じ出口へ出す。
    // スタミナは GameSignals.Stamina (HUD が読む)、制圧は ISuppressionPresenter (既定は画面の端を暗くする SuppressionVignette)、
    // よろけは視点の回りを遅くする (PlayerStagger と同じ)。判定はサーバーが持つので、ここでは届いた値を見せるだけにする
    public sealed class NetV2PlayerStatusPart : INetV2ClientPart
    {
        private readonly Func<ISuppressionPresenter> _presenter;
        private readonly Func<FirstPersonLook> _look;
        private readonly SuppressionSpec _suppression;
        private readonly StaggerSpec _stagger;
        private StaminaStatus? _stamina;
        private StaminaStatus? _published;
        private StaggerKind _staggerKind;
        private float _suppressionLevel;
        private ISuppressionPresenter _presented;
        private FirstPersonLook _slowedLook;
        private bool _disposed;

        // 調整値は演出の強さ (画面の暗さの上限・視点の倍率) にだけ使う。サーバーと同じ Resources の値を渡す
        public NetV2PlayerStatusPart(
            Func<ISuppressionPresenter> presenter, Func<FirstPersonLook> look, SuppressionSpec suppression, StaggerSpec stagger)
        {
            _presenter = presenter;
            _look = look;
            _suppression = suppression ?? throw new ArgumentNullException(nameof(suppression));
            _stagger = stagger ?? throw new ArgumentNullException(nameof(stagger));
        }

        public StaminaStatus? Stamina => _stamina;

        public StaggerKind Stagger => _staggerKind;

        public float SuppressionLevel => _suppressionLevel;

        // 結線 (NetV2ClientParts) から 1 行で足せるよう、自分の体に既定の演出を付ける作り方をここに置く
        public static NetV2PlayerStatusPart For(NetV2ClientPartsContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var localPlayer = context.LocalPlayer;
            return new NetV2PlayerStatusPart(
                () => PresenterOf(localPlayer?.Invoke()),
                () => LookOf(localPlayer?.Invoke()),
                KitTuning.Suppression,
                StaggerActionInstaller.Tuning.Player);
        }

        public bool Handle(IMessage message)
        {
            return false;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null || !snapshot.HasOwner)
            {
                return;
            }

            var owner = snapshot.Owner;
            _stamina = owner.Stamina;
            _staggerKind = owner.Stagger;
            _suppressionLevel = owner.SuppressionLevel;
            PublishStamina();
        }

        // 演出は毎フレーム度合いを渡す約束 (ISuppressionPresenter) なので、スナップショットの届かないフレームにも渡す
        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            Present(_suppressionLevel);
            ApplyLook();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _stamina = null;
            _staggerKind = StaggerKind.None;
            _suppressionLevel = 0f;
            PublishStamina();
            Present(0f);
            RestoreLook();
            _disposed = true;
        }

        private void PublishStamina()
        {
            if (_published == _stamina)
            {
                return;
            }

            if (_stamina.HasValue)
            {
                GameSignals.Stamina.Publish(_stamina);
            }
            else
            {
                GameSignals.Stamina.ClearIfCurrent(_published);
            }

            _published = _stamina;
        }

        // 演出が替わったら前の演出に 0 を渡し、画面に残さない
        private void Present(float level)
        {
            var presenter = Alive(_presenter?.Invoke());
            if (!ReferenceEquals(presenter, _presented))
            {
                Alive(_presented)?.Present(0f, _suppression);
                _presented = presenter;
            }

            presenter?.Present(level, _suppression);
        }

        private void ApplyLook()
        {
            if (_staggerKind == StaggerKind.None)
            {
                RestoreLook();
                return;
            }

            var look = _look?.Invoke();
            if (look == null)
            {
                return;
            }

            if (_slowedLook != null && _slowedLook != look)
            {
                RestoreLook();
            }

            look.TurnMultiplier = _stagger.TurnMultiplier;
            _slowedLook = look;
        }

        private void RestoreLook()
        {
            if (_slowedLook != null)
            {
                _slowedLook.TurnMultiplier = 1f;
            }

            _slowedLook = null;
        }

        private static ISuppressionPresenter PresenterOf(NetV2LocalPlayer local)
        {
            return local != null ? PlayerActions.GetOrAdd<SuppressionVignette>(local.gameObject) : null;
        }

        private static FirstPersonLook LookOf(NetV2LocalPlayer local)
        {
            return local != null ? local.Look : null;
        }

        // 演出が MonoBehaviour なら、消された後も参照が残るので Unity の null 比較で見る
        private static ISuppressionPresenter Alive(ISuppressionPresenter presenter)
        {
            return presenter is UnityEngine.Object unityObject && unityObject == null ? null : presenter;
        }
    }
}
