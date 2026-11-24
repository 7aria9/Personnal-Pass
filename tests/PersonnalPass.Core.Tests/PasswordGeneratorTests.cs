namespace PersonnalPass.Core.Tests;

public class PasswordGeneratorTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void Respects_length(int length)
    {
        Assert.Equal(length, PasswordGenerator.Generate(new PasswordOptions(length)).Length);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(33)]
    public void Rejects_out_of_range_length(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PasswordGenerator.Generate(new PasswordOptions(length)));
    }

    [Fact]
    public void Rejects_no_character_set()
    {
        Assert.Throws<ArgumentException>(() =>
            PasswordGenerator.Generate(new PasswordOptions(12, false, false, false, false)));
    }

    [Fact]
    public void Contains_every_selected_family_and_nothing_else()
    {
        for (var i = 0; i < 500; i++)
        {
            var pwd = PasswordGenerator.Generate(new PasswordOptions(8, Uppercase: true, Lowercase: false, Digits: true, Special: true));
            Assert.Contains(pwd, PasswordGenerator.UppercaseChars.Contains);
            Assert.Contains(pwd, PasswordGenerator.DigitChars.Contains);
            Assert.Contains(pwd, PasswordGenerator.SpecialChars.Contains);
            Assert.DoesNotContain(pwd, PasswordGenerator.LowercaseChars.Contains);
        }
    }

    [Fact]
    public void Digits_only()
    {
        var pwd = PasswordGenerator.Generate(new PasswordOptions(20, false, false, true, false));
        Assert.All(pwd, c => Assert.True(char.IsAsciiDigit(c)));
    }

    [Fact]
    public void Produces_different_values()
    {
        var set = Enumerable.Range(0, 100).Select(_ => PasswordGenerator.Generate(new PasswordOptions())).ToHashSet();
        Assert.Equal(100, set.Count);
    }
}
