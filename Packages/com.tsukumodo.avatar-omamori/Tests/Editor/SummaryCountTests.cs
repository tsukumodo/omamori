using System.Collections.Generic;
using NUnit.Framework;
using AvatarOmamori.Editor;

namespace AvatarOmamori.Tests.Editor
{
    /// <summary>
    /// 結果サマリー・Foldout 見出し・カード画像に出す件数の数え方を固定するテスト。
    /// 1つの問題を「サマリー＋内訳」の複数行で説明するチェックがあるため、
    /// 内訳行まで数えると実際より多くの問題があるように見えてしまう。
    /// </summary>
    public class SummaryCountTests
    {
        [Test]
        public void 内訳行は件数に数えない()
        {
            // Descriptor 重複（3個）を検出したときの並び。ユーザーから見た問題は1つ
            var items = new List<CheckResult>
            {
                new CheckResult(Severity.Error, "VRC Avatar Descriptor が 3 個見つかりました"),
                new CheckResult(Severity.Error, "重複した VRC Avatar Descriptor: Avatar/Body", isDetail: true),
                new CheckResult(Severity.Error, "重複した VRC Avatar Descriptor: Avatar/Hair", isDetail: true),
            };

            Assert.AreEqual(1, AvatarOmamoriWindow.CountPrimary(items));
        }

        [Test]
        public void 内訳行がなければ全件数える()
        {
            var items = new List<CheckResult>
            {
                new CheckResult(Severity.Warning, "マテリアルスロットが null です"),
                new CheckResult(Severity.Warning, "FX レイヤーの Weight が 0 です"),
            };

            Assert.AreEqual(2, AvatarOmamoriWindow.CountPrimary(items));
        }

        [Test]
        public void 空のリストは0件()
        {
            Assert.AreEqual(0, AvatarOmamoriWindow.CountPrimary(new List<CheckResult>()));
        }

        [Test]
        public void IsDetailの既定値はfalse()
        {
            // 既存チェックは isDetail を指定していないため、既定で件数に数えられる必要がある
            var result = new CheckResult(Severity.Error, "何らかの問題");

            Assert.IsFalse(result.IsDetail);
        }

        [Test]
        public void 内訳行だけのリストはグループを描画しない()
        {
            // サマリー行が無い＝内訳行だけが取り残されたケース（DEC-070）。
            // 文脈のない断片になるため ShouldDrawGroup は false を返す必要がある
            var items = new List<CheckResult>
            {
                new CheckResult(Severity.Error, "重複した VRC Avatar Descriptor: Avatar/Body", isDetail: true),
                new CheckResult(Severity.Error, "重複した VRC Avatar Descriptor: Avatar/Hair", isDetail: true),
            };

            Assert.IsFalse(AvatarOmamoriWindow.ShouldDrawGroup(items));
        }

        [Test]
        public void サマリー行があればグループを描画する()
        {
            var items = new List<CheckResult>
            {
                new CheckResult(Severity.Error, "VRC Avatar Descriptor が 3 個見つかりました"),
                new CheckResult(Severity.Error, "重複した VRC Avatar Descriptor: Avatar/Body", isDetail: true),
                new CheckResult(Severity.Error, "重複した VRC Avatar Descriptor: Avatar/Hair", isDetail: true),
            };

            Assert.IsTrue(AvatarOmamoriWindow.ShouldDrawGroup(items));
            Assert.AreEqual(1, AvatarOmamoriWindow.CountPrimary(items));
        }

        [Test]
        public void 空リストはグループを描画しない()
        {
            Assert.IsFalse(AvatarOmamoriWindow.ShouldDrawGroup(new List<CheckResult>()));
        }
    }
}
