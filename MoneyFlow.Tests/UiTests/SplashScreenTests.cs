using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using MoneyFlow.Desktop.Forms;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class SplashScreenTests
{
    [Fact]
    public void SplashScreen_InitializesWithCorrectDimensionsAndStyles()
    {
        using var splash = new SplashScreenForm();

        splash.Width.Should().Be(SplashScreenForm.SplashWidth);
        splash.Height.Should().Be(SplashScreenForm.SplashHeight);
        splash.FormBorderStyle.Should().Be(FormBorderStyle.None);
        splash.StartPosition.Should().Be(FormStartPosition.CenterScreen);
        splash.IsDoubleBuffered.Should().BeTrue();
        splash.ShowInTaskbar.Should().BeFalse();
        splash.BackColor.Should().Be(Color.FromArgb(248, 249, 255)); // #F8F9FF
    }

    [Fact]
    public void SplashScreen_PaintsCleanlyToMemoryBitmapWithoutExceptions()
    {
        using var splash = new SplashScreenForm();
        using var bmp = new Bitmap(SplashScreenForm.SplashWidth, SplashScreenForm.SplashHeight);

        // Invoke OnPaint via reflection with real graphics
        var onPaintMethod = typeof(SplashScreenForm).GetMethod("OnPaint", BindingFlags.NonPublic | BindingFlags.Instance);
        onPaintMethod.Should().NotBeNull();

        using (var g = Graphics.FromImage(bmp))
        {
            var pea = new PaintEventArgs(g, new Rectangle(0, 0, bmp.Width, bmp.Height));
            var act = () => onPaintMethod!.Invoke(splash, new object[] { pea });
            act.Should().NotThrow();
        }

        // Verify that the bitmap was painted and is not completely empty/black
        Color pixel = bmp.GetPixel(SplashScreenForm.SplashWidth / 2, SplashScreenForm.SplashHeight / 2);
        pixel.A.Should().Be(255);
    }

    [Fact]
    public void SplashScreen_SkipToEnd_TransitionsImmediatelyToReady()
    {
        using var splash = new SplashScreenForm();

        // Simulate SkipToEnd via reflection
        var skipMethod = typeof(SplashScreenForm).GetMethod("SkipToEnd", BindingFlags.NonPublic | BindingFlags.Instance);
        skipMethod.Should().NotBeNull();

        skipMethod!.Invoke(splash, null);

        splash.DialogResult.Should().Be(DialogResult.OK);
    }
}
