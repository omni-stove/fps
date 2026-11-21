using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Shooter.NetV2
{
    // 波・通貨・直す/置く案内は ClientHordeStatus がスナップショットから作り、ここでは Interact の押下で補強を頼み、その状態を GameSignals.ModeStatus に出す。
    // NetV2Hud の目的の並びに入れずにここで出すのは、HUD はマップを替えるたびに作り直されるが、要求を送る口と入力はこの部品が接続の間ずっと持つため
    public sealed class NetV2HordePart : INetV2ClientPart
    {
        public const string InteractActionPath = InputActionPaths.Interact;

        private readonly Func<int> _localPlayerId;
        private readonly Func<IMessage, bool> _send;
        private readonly Func<bool> _canAct;
        private readonly Func<bool> _interactPressed;
        private readonly Func<string> _keyLabel;
        private readonly Action<string> _log;
        private ClientHordeStatus _status;
        private int _statusFor;
        private ModeStatus _published;
        private InputAction _interactAction;
        private bool _disposed;

        // send が null なら案内を見せるだけで、押下を読まず要求も送らない (再生)。押したかとキーの表示を引く口は、テストや別の入力から差し替えられるよう受け取る
        public NetV2HordePart(
            Func<int> localPlayerId,
            Func<IMessage, bool> send,
            Func<bool> canAct,
            Func<bool> interactPressed = null,
            Func<string> keyLabel = null,
            Action<string> log = null)
        {
            _localPlayerId = localPlayerId ?? throw new ArgumentNullException(nameof(localPlayerId));
            _send = send;
            _canAct = canAct ?? throw new ArgumentNullException(nameof(canAct));
            _interactPressed = interactPressed ?? ReadInteractPressed;
            _keyLabel = keyLabel ?? ReadKeyLabel;
            _log = log ?? (line => Debug.Log(line));
        }

        // Horde の試合でなければ null
        public ClientHordeStatus Status => _status != null && _status.Status != null ? _status : null;

        // NetV2ClientParts.Registered に並べる口。再生でも波と通貨は見返せるので出し、頼む口だけを外す
        public static INetV2ClientPart Create(NetV2ClientPartsContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var client = context.Client;
            return new NetV2HordePart(
                () => client.PlayerId,
                context.Replay ? null : context.Send,
                () => !context.Replay && NetV2ClientParts.CanAct(client),
                log: context.Log);
        }

        public bool Handle(IMessage message)
        {
            return false;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            // 自分の Id は受け入れられてから決まり、部品はその前に作られるので、Id が変わったら読み手を作り直す
            var localId = _localPlayerId();
            if (_status == null || _statusFor != localId)
            {
                _status = new ClientHordeStatus(localId, _send);
                _statusFor = localId;
            }

            _status.InteractKeyLabel = _send != null ? _keyLabel() : null;
            _status.Apply(snapshot);
            Publish(_status.Status);
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            var offered = _send != null && _status != null && _status.OffersFortify && _canAct();
            KeySharing.SetClaim(InputActionPaths.Interact, this, offered);
            if (!offered || GameUi.IsPauseOpen || !_interactPressed())
            {
                return;
            }

            var offer = _status.Horde.Offer.Value;
            var sent = _status.RequestFortify();
            _log(ClientHordeLog.FortifyRequested(offer.Action, offer.StructureName, offer.Cost, sent));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            KeySharing.Release(InputActionPaths.Interact, this);
            Publish(null);
            _status = null;
        }

        // ClientHordeStatus は値が変わらなければ同じ ModeStatus を返すので、参照が替わったときだけ出し直す
        private void Publish(ModeStatus status)
        {
            if (ReferenceEquals(status, _published))
            {
                return;
            }

            if (_published != null)
            {
                GameSignals.ModeStatus.ClearIfCurrent(_published);
            }

            _published = status;
            if (_published != null)
            {
                GameSignals.ModeStatus.Publish(_published);
            }
        }

        private InputAction InteractAction()
        {
            if (_interactAction == null)
            {
                var asset = InputSystem.actions;
                _interactAction = asset != null ? asset.FindAction(InteractActionPath) : null;
            }

            return _interactAction;
        }

        private bool ReadInteractPressed()
        {
            var action = InteractAction();
            return action != null && action.WasPressedThisFrame();
        }

        // オフラインの HordeInteractInput と同じ表示にする
        private string ReadKeyLabel()
        {
            var action = InteractAction();
            return action != null ? action.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions) : null;
        }
    }
}
