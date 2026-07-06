using Diabolical.Services;

namespace Diabolical.Tests;

public class HotkeyManagerTests
{
    [Theory]
    [InlineData("Control", HotkeyManager.ModifierKeys.Control)]
    [InlineData("Ctrl", HotkeyManager.ModifierKeys.Control)]
    [InlineData("Alt", HotkeyManager.ModifierKeys.Alt)]
    [InlineData("Shift", HotkeyManager.ModifierKeys.Shift)]
    [InlineData("Win", HotkeyManager.ModifierKeys.Win)]
    [InlineData("control", HotkeyManager.ModifierKeys.Control)]
    public void ParseModifiers_SingleModifier_ParsesCaseInsensitively(string input, HotkeyManager.ModifierKeys expected)
    {
        Assert.Equal(expected, HotkeyManager.ParseModifiers(input));
    }

    [Fact]
    public void ParseModifiers_MultipleModifiers_CombinesFlags()
    {
        var result = HotkeyManager.ParseModifiers("Control+Alt");

        Assert.Equal(HotkeyManager.ModifierKeys.Control | HotkeyManager.ModifierKeys.Alt, result);
    }

    [Fact]
    public void ParseModifiers_UnknownModifier_Throws()
    {
        Assert.Throws<ArgumentException>(() => HotkeyManager.ParseModifiers("Control+Nonsense"));
    }
}
