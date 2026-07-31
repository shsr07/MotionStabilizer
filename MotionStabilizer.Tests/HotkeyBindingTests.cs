using MotionStabilizer.Models;
using Xunit;

namespace MotionStabilizer.Tests;

/// <summary>
/// Unit tests for <see cref="HotkeyBinding"/> — display string formatting,
/// IsSet logic, and Clone.
/// </summary>
public class HotkeyBindingTests
{
    [Fact]
    public void IsSet_False_WhenKeyIsEmpty()
    {
        var b = new HotkeyBinding { Key = "" };
        Assert.False(b.IsSet);
    }

    [Fact]
    public void IsSet_False_WhenKeyIsWhitespace()
    {
        var b = new HotkeyBinding { Key = "   " };
        Assert.False(b.IsSet);
    }

    [Fact]
    public void IsSet_True_WhenKeyIsSet()
    {
        var b = new HotkeyBinding { Key = "F1" };
        Assert.True(b.IsSet);
    }

    [Fact]
    public void DisplayString_Empty_WhenNotSet()
    {
        var b = new HotkeyBinding { Key = "" };
        Assert.Equal("—", b.DisplayString);
    }

    [Fact]
    public void DisplayString_KeyOnly_WhenNoModifiers()
    {
        var b = new HotkeyBinding { Key = "F1" };
        Assert.Equal("F1", b.DisplayString);
    }

    [Fact]
    public void DisplayString_WithCtrl()
    {
        var b = new HotkeyBinding { Key = "R", Ctrl = true };
        Assert.Equal("Ctrl + R", b.DisplayString);
    }

    [Fact]
    public void DisplayString_WithCtrlAltShift()
    {
        var b = new HotkeyBinding { Key = "G", Ctrl = true, Alt = true, Shift = true };
        Assert.Equal("Ctrl + Alt + Shift + G", b.DisplayString);
    }

    [Fact]
    public void DisplayString_ModifiersInCorrectOrder()
    {
        // Order should always be Ctrl, Alt, Shift, Key regardless of how they're set
        var b = new HotkeyBinding { Key = "X", Shift = true, Alt = true, Ctrl = true };
        Assert.Equal("Ctrl + Alt + Shift + X", b.DisplayString);
    }

    [Fact]
    public void Clone_CreatesDeepCopy()
    {
        var original = new HotkeyBinding
        {
            Name = "TestBinding",
            Key = "F5",
            Ctrl = true,
            Alt = false,
            Shift = true
        };
        var clone = original.Clone();

        Assert.Equal(original.Name, clone.Name);
        Assert.Equal(original.Key, clone.Key);
        Assert.Equal(original.Ctrl, clone.Ctrl);
        Assert.Equal(original.Alt, clone.Alt);
        Assert.Equal(original.Shift, clone.Shift);
        Assert.Equal(original.DisplayString, clone.DisplayString);
    }

    [Fact]
    public void Clone_IsIndependent_FromOriginal()
    {
        var original = new HotkeyBinding { Key = "F1", Ctrl = true };
        var clone = original.Clone();

        clone.Key = "F2";
        clone.Ctrl = false;

        Assert.Equal("F1", original.Key);
        Assert.True(original.Ctrl);
        Assert.Equal("F2", clone.Key);
        Assert.False(clone.Ctrl);
    }
}
