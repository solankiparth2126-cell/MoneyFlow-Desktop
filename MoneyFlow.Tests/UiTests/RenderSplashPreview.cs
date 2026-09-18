using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using MoneyFlow.Desktop.Forms;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class RenderSplashPreview
{
    [Fact]
    public void GeneratePreviewImage()
    {
        using var splash = new SplashScreenForm();
        using var bmp = new Bitmap(SplashScreenForm.SplashWidth, SplashScreenForm.SplashHeight);

        // Set full progress so logo, title, and bar are all painted at 100%
        var fadeField = typeof(SplashScreenForm).GetField("_fadeAlpha", BindingFlags.NonPublic | BindingFlags.Instance);
        fadeField?.SetValue(splash, 1.0f);

        var progField = typeof(SplashScreenForm).GetField("_displayProgress", BindingFlags.NonPublic | BindingFlags.Instance);
        progField?.SetValue(splash, 0.45f);

        var slideField = typeof(SplashScreenForm).GetField("_logoSlideOffset", BindingFlags.NonPublic | BindingFlags.Instance);
        slideField?.SetValue(splash, 0.0f);

        var statusField = typeof(SplashScreenForm).GetField("_currentStatus", BindingFlags.NonPublic | BindingFlags.Instance);
        statusField?.SetValue(splash, "Preparing workspace...");

        var onPaintMethod = typeof(SplashScreenForm).GetMethod("OnPaint", BindingFlags.NonPublic | BindingFlags.Instance);
        using (var g = Graphics.FromImage(bmp))
        {
            var pea = new PaintEventArgs(g, new Rectangle(0, 0, bmp.Width, bmp.Height));
            onPaintMethod!.Invoke(splash, new object[] { pea });
        }

        string outDir = @"C:\Users\solan\.gemini\antigravity-ide\brain\7492011c-cc1e-4699-bbc1-0e0910df479c";
        string outPath = System.IO.Path.Combine(outDir, "splash_screen_updated.png");
        bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Png);
    }
}
