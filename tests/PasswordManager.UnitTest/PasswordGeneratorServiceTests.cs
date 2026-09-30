using FlowLauncher.Plugin.PasswordManager.Models;
using FlowLauncher.Plugin.PasswordManager.Services;

using Xunit;

namespace FlowLauncher.Plugin.PasswordManager.Tests;

public class PasswordGeneratorServiceTests
{
    private static readonly PasswordCharacterSet All = new(true, true, true, true);

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(64)]
    [InlineData(256)]
    public void Generate_ReturnsRequestedLength(int length)
    {
        var password = PasswordGeneratorService.Generate(length, All);
        Assert.Equal(length, password.Length);
    }

    [Fact]
    public void Generate_ClampsBelowMinimum()
    {
        var password = PasswordGeneratorService.Generate(0, All);
        Assert.Equal(PasswordGeneratorService.MinLength, password.Length);
    }

    [Fact]
    public void Generate_ClampsAboveMaximum()
    {
        var password = PasswordGeneratorService.Generate(9999, All);
        Assert.Equal(PasswordGeneratorService.MaxLength, password.Length);
    }

    [Fact]
    public void Generate_OnlyUsesCharactersFromAlphabet()
    {
        var set = new PasswordCharacterSet(true, true, true, true, "");
        var alphabet = set.BuildAlphabet();

        for (var i = 0; i < 200; i++)
        {
            var password = PasswordGeneratorService.Generate(32, set);
            Assert.All(password, c => Assert.Contains(c, alphabet));
        }
    }

    [Fact]
    public void Generate_IncludesEverySelectedCategory()
    {
        for (var i = 0; i < 200; i++)
        {
            var password = PasswordGeneratorService.Generate(8, All);
            Assert.Contains(password, char.IsLower);
            Assert.Contains(password, char.IsUpper);
            Assert.Contains(password, char.IsDigit);
            Assert.Contains(password, c => !char.IsLetterOrDigit(c));
        }
    }

    [Fact]
    public void Generate_ShortPassword_StillCoversAtLeastOneCategory()
    {
        for (var i = 0; i < 200; i++)
        {
            var password = PasswordGeneratorService.Generate(2, All);
            Assert.Equal(2, password.Length);
            Assert.Contains(password, c =>
                char.IsLower(c) || char.IsUpper(c) || char.IsDigit(c) || !char.IsLetterOrDigit(c));
        }
    }

    [Fact]
    public void Generate_ExcludesIgnoredCharacters()
    {
        var set = new PasswordCharacterSet(true, true, true, true, "0O1lI");
        for (var i = 0; i < 200; i++)
        {
            var password = PasswordGeneratorService.Generate(48, set);
            Assert.DoesNotContain(password, c => "0O1lI".Contains(c));
        }
    }

    [Fact]
    public void Generate_IgnoredCharactersAppliedAcrossAllCategories()
    {
        // 'a' 来自小写、'A' 来自大写、'0' 来自数字、'!' 来自符号
        var set = new PasswordCharacterSet(true, true, true, true, "aA0!");
        var alphabet = set.BuildAlphabet();

        Assert.DoesNotContain('a', alphabet);
        Assert.DoesNotContain('A', alphabet);
        Assert.DoesNotContain('0', alphabet);
        Assert.DoesNotContain('!', alphabet);

        for (var i = 0; i < 100; i++)
        {
            var password = PasswordGeneratorService.Generate(32, set);
            Assert.All(password, c => Assert.Contains(c, alphabet));
        }
    }

    [Fact]
    public void Generate_SingleCategory_OnlyProducesThatCategory()
    {
        var set = new PasswordCharacterSet(false, false, false, true);
        for (var i = 0; i < 100; i++)
        {
            var password = PasswordGeneratorService.Generate(24, set);
            Assert.All(password, c => Assert.Contains(c, PasswordCharacterSet.SymbolPool));
        }
    }

    [Fact]
    public void Generate_NoCategorySelected_Throws()
    {
        var set = new PasswordCharacterSet(false, false, false, false);
        Assert.Throws<InvalidOperationException>(() => PasswordGeneratorService.Generate(16, set));
    }

    [Fact]
    public void Generate_AllCharactersIgnored_Throws()
    {
        var set = new PasswordCharacterSet(false, false, false, true, PasswordCharacterSet.SymbolPool);
        Assert.Throws<InvalidOperationException>(() => PasswordGeneratorService.Generate(16, set));
    }

    [Fact]
    public void TryGenerate_InvalidConfig_ReturnsNull()
    {
        var set = new PasswordCharacterSet(false, false, false, false);
        Assert.Null(PasswordGeneratorService.TryGenerate(16, set));
    }

    [Fact]
    public void TryGenerate_ValidConfig_ReturnsPassword()
    {
        Assert.Equal(16, PasswordGeneratorService.TryGenerate(16, All)!.Length);
    }

    [Fact]
    public void Generate_ProducesVariedOutput()
    {
        var samples = Enumerable.Range(0, 50)
            .Select(_ => PasswordGeneratorService.Generate(24, All))
            .ToList();

        Assert.Equal(50, samples.Distinct().Count());
    }

    [Fact]
    public void Generate_IsNotPurelySequential()
    {
        // 至少有一位字符在小样本中发生位置变化，避免洗牌被意外跳过
        var samples = Enumerable.Range(0, 30)
            .Select(_ => PasswordGeneratorService.Generate(24, All))
            .ToList();

        var firstChars = samples.Select(s => s[0]).Distinct().Count();
        Assert.True(firstChars > 1, "所有样本首字符相同，洗牌可能失效");
    }

    [Fact]
    public void Generate_ManyCategoriesShorterThanLength_StillGenerates()
    {
        // 4 类可用但长度只有 3：不能抛异常，且长度正确
        var password = PasswordGeneratorService.Generate(3, All);
        Assert.Equal(3, password.Length);
    }
}

public class PasswordCharacterSetTests
{
    [Fact]
    public void BuildAlphabet_AllCategories_ContainsEachPool()
    {
        var alphabet = new PasswordCharacterSet(true, true, true, true).BuildAlphabet();

        Assert.Contains('a', alphabet);
        Assert.Contains('Z', alphabet);
        Assert.Contains('5', alphabet);
        Assert.Contains('!', alphabet);
    }

    [Fact]
    public void BuildAlphabet_DisabledCategory_IsAbsent()
    {
        var alphabet = new PasswordCharacterSet(true, false, false, false).BuildAlphabet();

        Assert.Equal(PasswordCharacterSet.LowerPool.Length, alphabet.Length);
        Assert.Equal(PasswordCharacterSet.LowerPool.Order(), alphabet.Order());
    }

    [Fact]
    public void BuildAlphabet_NoDuplicates()
    {
        var alphabet = new PasswordCharacterSet(true, true, true, true).BuildAlphabet();
        Assert.Equal(alphabet.Length, alphabet.Distinct().Count());
    }

    [Fact]
    public void BuildPools_SkipsFullyIgnoredCategory()
    {
        // 只勾选符号但把符号全忽略 → 没有可用池
        var set = new PasswordCharacterSet(false, false, false, true, PasswordCharacterSet.SymbolPool);
        Assert.Empty(set.BuildPools());
    }

    [Fact]
    public void BuildPools_CountMatchesSelectedCategories()
    {
        Assert.Equal(2, new PasswordCharacterSet(true, false, true, false).BuildPools().Count);
        Assert.Equal(4, new PasswordCharacterSet(true, true, true, true).BuildPools().Count);
        Assert.Empty(new PasswordCharacterSet(false, false, false, false).BuildPools());
    }

    [Fact]
    public void Describe_ListsSelectedCategoriesInChinese()
    {
        Assert.Equal("小写+大写", new PasswordCharacterSet(true, true, false, false).Describe());
        Assert.Equal("数字", new PasswordCharacterSet(false, false, true, false).Describe());
        Assert.Equal("符号", new PasswordCharacterSet(false, false, false, true).Describe());
        Assert.Equal("无可用字符", new PasswordCharacterSet(false, false, false, false).Describe());
    }

    [Fact]
    public void Describe_CoversAllCategories()
    {
        Assert.Equal("小写+大写+数字+符号", new PasswordCharacterSet(true, true, true, true).Describe());
    }
}

public class PasswordLengthParserTests
{
    [Fact]
    public void TryParse_CommaSeparated_ReturnsAscendingDistinct()
    {
        Assert.True(PasswordLengthParser.TryParse("24, 8, 16, 12", out var lengths));
        Assert.Equal([8, 12, 16, 24], lengths);
    }

    [Fact]
    public void TryParse_SupportsMultipleSeparators()
    {
        Assert.True(PasswordLengthParser.TryParse("8 12;16、20|24/32", out var lengths));
        Assert.Equal([8, 12, 16, 20, 24, 32], lengths);
    }

    [Fact]
    public void TryParse_Deduplicates()
    {
        Assert.True(PasswordLengthParser.TryParse("16,16,16", out var lengths));
        Assert.Equal([16], lengths);
    }

    [Fact]
    public void TryParse_IgnoresNonNumericTokens()
    {
        Assert.True(PasswordLengthParser.TryParse("abc, 16, xyz", out var lengths));
        Assert.Equal([16], lengths);
    }

    [Fact]
    public void TryParse_ClampsToValidRange()
    {
        Assert.True(PasswordLengthParser.TryParse("0, 9999", out var lengths));
        Assert.Equal(
            [PasswordGeneratorService.MinLength, PasswordGeneratorService.MaxLength],
            lengths);
    }

    [Fact]
    public void TryParse_CapsCount()
    {
        Assert.True(PasswordLengthParser.TryParse("1,2,3,4,5,6,7,8,9,10,11,12", out var lengths));
        Assert.Equal(PasswordGeneratorService.MaxConfiguredLengths, lengths.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    public void TryParse_InvalidInput_ReturnsFalse(string? text)
    {
        Assert.False(PasswordLengthParser.TryParse(text, out _));
    }

    [Fact]
    public void CountNumericTokens_CountsOnlyNumbers()
    {
        Assert.Equal(3, PasswordLengthParser.CountNumericTokens("8, abc, 16, 24"));
        Assert.Equal(0, PasswordLengthParser.CountNumericTokens("abc"));
        Assert.Equal(0, PasswordLengthParser.CountNumericTokens(""));
    }
}

public class PasswordStrengthEstimatorTests
{
    [Fact]
    public void EntropyBits_GrowsWithLengthAndAlphabet()
    {
        var shortBits = PasswordStrengthEstimator.EntropyBits(8, 62);
        var longBits = PasswordStrengthEstimator.EntropyBits(16, 62);
        var wideBits = PasswordStrengthEstimator.EntropyBits(8, 87);

        Assert.True(longBits > shortBits);
        Assert.True(wideBits > shortBits);
    }

    [Fact]
    public void EntropyBits_DegenerateInputs_AreZero()
    {
        Assert.Equal(0, PasswordStrengthEstimator.EntropyBits(0, 62));
        Assert.Equal(0, PasswordStrengthEstimator.EntropyBits(8, 0));
        Assert.Equal(0, PasswordStrengthEstimator.EntropyBits(8, 1));
    }

    [Fact]
    public void Describe_ClassifiesWeakPassword()
    {
        // 6 位小写字母：6 * log2(26) ≈ 28 bit
        Assert.Contains("弱", PasswordStrengthEstimator.Describe(6, 26));
    }

    [Fact]
    public void Describe_ClassifiesVeryStrongPassword()
    {
        // 20 位全字符集：20 * log2(87) ≈ 129 bit
        Assert.Contains("极强", PasswordStrengthEstimator.Describe(20, 87));
    }
}

public class GeneratorSettingsTests
{
    [Fact]
    public void Defaults_EnableAllCategories()
    {
        var settings = new Settings();

        Assert.True(settings.GeneratorLowercase);
        Assert.True(settings.GeneratorUppercase);
        Assert.True(settings.GeneratorDigits);
        Assert.True(settings.GeneratorSymbols);
        Assert.Equal("", settings.GeneratorExcludeChars);
    }

    [Fact]
    public void Defaults_ExposeFallbackLengthList()
    {
        var settings = new Settings();
        Assert.Equal([8, 12, 16, 20, 24], settings.GetGeneratorLengths());
    }

    [Fact]
    public void GetGeneratorLengths_ParsesConfiguredValue()
    {
        var settings = new Settings { GeneratorLengths = "10, 30" };
        Assert.Equal([10, 30], settings.GetGeneratorLengths());
    }

    [Fact]
    public void GetGeneratorLengths_InvalidValue_FallsBackToDefault()
    {
        var settings = new Settings { GeneratorLengths = "not a number" };
        Assert.Equal([8, 12, 16, 20, 24], settings.GetGeneratorLengths());
    }

    [Fact]
    public void GetCharacterSet_ReflectsConfiguredFlags()
    {
        var settings = new Settings
        {
            GeneratorLowercase = false,
            GeneratorUppercase = true,
            GeneratorDigits = false,
            GeneratorSymbols = true,
            GeneratorExcludeChars = "0O1lI",
        };

        var set = settings.GetCharacterSet();

        Assert.False(set.Lowercase);
        Assert.True(set.Uppercase);
        Assert.False(set.Digits);
        Assert.True(set.Symbols);
        Assert.Equal("0O1lI", set.Exclude);

        var alphabet = set.BuildAlphabet();
        Assert.DoesNotContain('O', alphabet);
        Assert.DoesNotContain('I', alphabet);
        Assert.All(alphabet, c => Assert.True(char.IsUpper(c) || !char.IsLetterOrDigit(c)));
    }

    [Fact]
    public void GetCharacterSet_NullExcludeChars_DoesNotThrow()
    {
        var settings = new Settings { GeneratorExcludeChars = null! };
        Assert.NotNull(settings.GetCharacterSet().BuildAlphabet());
    }
}
