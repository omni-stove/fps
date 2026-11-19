using System;
using System.IO;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // マッチングに使うレートを、進行のプロフィールと別のスロットに持つ。進行を外したゲームでもレートを回せるよう本体に置く。
    public sealed class SkillRecorder
    {
        public const string Slot = "skill";

        // 次の保存で上書きして消してしまわないよう、読めなかった内容を別の枠に残す (新しい版のゲームで書いたものを古い版で開いたときなど)。
        public const string RejectedSlot = "skill-rejected";

        public const string ResourcePath = KitTuning.SkillResourcePath;

        // 前の保存に失敗した分を、レートの動かない試合の後でも書くために覚える。
        private bool _unsaved;

        public SkillRecorder(ISaveStorage storage)
            : this(storage, LoadSpec())
        {
        }

        public SkillRecorder(ISaveStorage storage, SkillSpec spec)
        {
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
            Skill = new SkillRating(spec ?? throw new ArgumentNullException(nameof(spec)));
            Rating = spec.InitialRating;
        }

        public ISaveStorage Storage { get; }

        public SkillRating Skill { get; }

        public int Rating { get; private set; }

        public bool IsInstalled { get; private set; }

        public static SkillSpec LoadSpec()
        {
            return KitTuning.Load(c => c.SkillTuning, ResourcePath, SkillJson.Parse, SkillSpec.Default);
        }

        public void Install()
        {
            if (IsInstalled)
            {
                return;
            }

            Debug.Log(SkillLog.Configured(Skill.Spec));
            var loaded = Load();
            Debug.Log(SkillLog.Loaded(new SkillSave(loaded)));
            CombatEvents.MatchFinished += OnMatchFinished;
            IsInstalled = true;
            Rating = loaded;
        }

        // 保存先の中身が外から替わったとき (クラウドセーブが他の端末の新しいレートを採ったとき) に呼ぶ。
        // 読み直さないと、次の保存で手元の古いレートが新しい時刻で書かれ、採った分を消してしまう。
        public void Reload()
        {
            if (!IsInstalled)
            {
                return;
            }

            var loaded = Load();
            Debug.Log(SkillLog.Loaded(new SkillSave(loaded)));
            _unsaved = false;
            Rating = loaded;
        }

        // 版 5 までのプロフィールに入っていたレートを引き継ぐ。skill のスロットがすでにあれば、そちらが新しいので何もしない。
        public bool AdoptLegacy(int rating)
        {
            if (rating < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rating), rating, "rating must not be negative");
            }

            if (!IsInstalled)
            {
                // 読み込む前に引き継ぐと、Install で読んだレートに上書きされて消えるので断る。
                throw new InvalidOperationException("a legacy rating can only be adopted while the skill recorder is installed");
            }

            if (Storage.TryRead(Slot, out var text) && text != null)
            {
                return false;
            }

            // 調整で上限・下限を狭めたゲームでも範囲の外のレートを持ち込まないよう、今の範囲に収める。
            var adopted = Skill.Clamp(rating);
            Debug.Log(SkillLog.AdoptedLegacy(rating, adopted));
            Rating = adopted;

            // プロフィールは次の保存でレートを書かなくなるので、ここで書いておかないと引き継いだ値を失う。
            Save();
            return true;
        }

        public void Uninstall()
        {
            if (!IsInstalled)
            {
                return;
            }

            CombatEvents.MatchFinished -= OnMatchFinished;
            IsInstalled = false;
        }

        public bool Save()
        {
            if (!IsInstalled)
            {
                // 読み込む前に書くと、保存してあるレートを初めの値で上書きして消すので断る。
                throw new InvalidOperationException("the rating can only be saved while the skill recorder is installed");
            }

            var save = new SkillSave(Rating);
            try
            {
                Storage.Write(Slot, save.ToJson());
                _unsaved = false;
                Debug.Log(SkillLog.Saved(save));
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // 手元の Rating は持ち続けるので、次の試合が終わったときに書ける。
                _unsaved = true;
                Debug.LogError(SkillLog.SaveFailed(e.Message));
                return false;
            }
        }

        private int Load()
        {
            if (!Storage.TryRead(Slot, out var text) || text == null)
            {
                return Skill.Spec.InitialRating;
            }

            try
            {
                return SkillSave.FromJson(text).Rating;
            }
            catch (FormatException e)
            {
                Debug.LogWarning(SkillLog.Rejected(e.Message));
                KeepRejected(text);
                return Skill.Spec.InitialRating;
            }
        }

        private void KeepRejected(string text)
        {
            try
            {
                Storage.Write(RejectedSlot, text);
                Debug.LogWarning(SkillLog.Kept(RejectedSlot));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // 退避は失ってもレートは初めの値から回せるので、読み込みは止めずに続ける。
                Debug.LogError(SkillLog.SaveFailed(e.Message));
            }
        }

        private void OnMatchFinished(MatchResult result)
        {
            if (!result.Rated)
            {
                if (_unsaved)
                {
                    Save();
                }

                return;
            }

            var previous = Rating;
            var opponents = result.OpponentRating ?? Skill.Spec.InitialRating;
            var rating = Skill.Next(previous, opponents, result.Outcome);
            Debug.Log(SkillLog.Rated(result.Outcome, previous, rating, opponents));
            Rating = rating;
            Save();
        }

    }
}
