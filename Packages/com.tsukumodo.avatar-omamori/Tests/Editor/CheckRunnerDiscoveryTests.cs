using System.Linq;
using NUnit.Framework;
using AvatarOmamori.Editor;

namespace AvatarOmamori.Tests.Editor
{
    public class CheckRunnerDiscoveryTests
    {
        [Test]
        public void Checks_実装済みの9チェックが全て検出される()
        {
            // Checks はアセンブリ内の IAvatarCheck 実装をリフレクション列挙するだけで
            // IsAvailable() は見ないため、MA 未インストール環境でも9件全てが列挙される
            var names = CheckRunner.Checks.Select(c => c.GetType().Name).ToList();

            var expected = new[]
            {
                "DescriptorDuplicateCheck",
                "MissingScriptCheck",
                "MissingShaderCheck",
                "EmptyParameterNameCheck",
                "ExpressionParameterBitLimitCheck",
                "AnimatorLayerWeightCheck",
                "MAMenuItemUnboundCheck",
                "MAObjectToggleCheck",
                "MAUnsetupAccessoryCheck",
            };

            foreach (var name in expected)
            {
                Assert.That(names, Does.Contain(name), $"{name} が CheckRunner.Checks に含まれていない");
            }
        }
    }
}
