using MotionStabilizer.Services;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for <see cref="Win32Interop.KeyNameToVk"/> — converts key name
/// strings to Win32 virtual key codes. Tests the pure mapping logic (function
/// keys, letters, digits, special keys, NumPad) without relying on VkKeyScan.
/// </summary>
public class KeyNameToVkTests
{
    // ── Function keys F1–F24 ──

    [Theory]
    [InlineData("F1", 0x70)]
    [InlineData("F2", 0x71)]
    [InlineData("F3", 0x72)]
    [InlineData("F4", 0x73)]
    [InlineData("F5", 0x74)]
    [InlineData("F6", 0x75)]
    [InlineData("F7", 0x76)]
    [InlineData("F8", 0x77)]
    [InlineData("F9", 0x78)]
    [InlineData("F10", 0x79)]
    [InlineData("F11", 0x7A)]
    [InlineData("F12", 0x7B)]
    [InlineData("F24", 0x87)]
    public void KeyNameToVk_FunctionKeys(string key, uint expected)
    {
        Assert.Equal(expected, Win32Interop.KeyNameToVk(key));
    }

    // ── Letters A–Z ──

    [Theory]
    [InlineData("A", 0x41)]
    [InlineData("B", 0x42)]
    [InlineData("G", 0x47)]
    [InlineData("R", 0x52)]
    [InlineData("W", 0x57)]
    [InlineData("Z", 0x5A)]
    public void KeyNameToVk_Letters(string key, uint expected)
    {
        Assert.Equal(expected, Win32Interop.KeyNameToVk(key));
    }

    [Fact]
    public void KeyNameToVk_LowercaseLetter_ConvertsToUpper()
    {
        Assert.Equal(0x41u, Win32Interop.KeyNameToVk("a"));
        Assert.Equal(0x57u, Win32Interop.KeyNameToVk("w"));
    }

    // ── Digits 0–9 ──

    [Theory]
    [InlineData("0", 0x30)]
    [InlineData("1", 0x31)]
    [InlineData("5", 0x35)]
    [InlineData("9", 0x39)]
    public void KeyNameToVk_Digits(string key, uint expected)
    {
        Assert.Equal(expected, Win32Interop.KeyNameToVk(key));
    }

    // ── NumPad keys ──

    [Theory]
    [InlineData("NumPad0", 0x60)]
    [InlineData("NumPad1", 0x61)]
    [InlineData("NumPad5", 0x65)]
    [InlineData("NumPad9", 0x69)]
    public void KeyNameToVk_NumPad(string key, uint expected)
    {
        Assert.Equal(expected, Win32Interop.KeyNameToVk(key));
    }

    // ── Special keys ──

    [Theory]
    [InlineData("Space", 0x20)]
    [InlineData("Enter", 0x0D)]
    [InlineData("Return", 0x0D)]
    [InlineData("Tab", 0x09)]
    [InlineData("Esc", 0x1B)]
    [InlineData("Escape", 0x1B)]
    [InlineData("Home", 0x24)]
    [InlineData("End", 0x23)]
    [InlineData("Insert", 0x2D)]
    [InlineData("Delete", 0x2E)]
    [InlineData("PageUp", 0x21)]
    [InlineData("PageDown", 0x22)]
    [InlineData("Left", 0x25)]
    [InlineData("Up", 0x26)]
    [InlineData("Right", 0x27)]
    [InlineData("Down", 0x28)]
    public void KeyNameToVk_SpecialKeys(string key, uint expected)
    {
        Assert.Equal(expected, Win32Interop.KeyNameToVk(key));
    }

    // ── Edge cases ──

    [Fact]
    public void KeyNameToVk_EmptyString_ReturnsZero()
    {
        Assert.Equal(0u, Win32Interop.KeyNameToVk(""));
    }

    [Fact]
    public void KeyNameToVk_Null_ReturnsZero()
    {
        Assert.Equal(0u, Win32Interop.KeyNameToVk(null!));
    }

    [Fact]
    public void KeyNameToVk_Whitespace_ReturnsZero()
    {
        Assert.Equal(0u, Win32Interop.KeyNameToVk("   "));
    }

    [Fact]
    public void KeyNameToVk_UnknownKey_ReturnsZero()
    {
        Assert.Equal(0u, Win32Interop.KeyNameToVk("UnknownKey"));
    }
}
