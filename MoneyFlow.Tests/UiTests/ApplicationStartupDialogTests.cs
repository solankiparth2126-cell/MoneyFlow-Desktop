using System.Drawing;
using System.Windows.Forms;
using FluentAssertions;
using MoneyFlow.Desktop.Dialogs;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class ApplicationStartupDialogTests
{
    [Fact]
    public void ApplicationStartupDialog_InitializesWithCorrectDimensionsAndProperties()
    {
        // Act
        using var dialog = new ApplicationStartupDialog();

        // Assert
        dialog.Width.Should().Be(740);
        dialog.Height.Should().Be(360);
        dialog.FormBorderStyle.Should().Be(FormBorderStyle.None);
        dialog.Padding.Should().Be(new Padding(1), "1px uniform frame perimeter border");
        dialog.BackColor.Should().Be(Color.FromArgb(17, 36, 58), "Deep navy border frame matching header");
    }

    [Fact]
    public void ApplicationStartupDialog_DoesNotContainFinancialYearCycleElements()
    {
        // Act
        using var dialog = new ApplicationStartupDialog();

        // Assert - verify no child labels or panels contain 'Financial Year Cycle'
        bool foundFinYear = false;
        void CheckControl(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child.Text != null && child.Text.Contains("Financial Year Cycle", System.StringComparison.OrdinalIgnoreCase))
                {
                    foundFinYear = true;
                }
                CheckControl(child);
            }
        }
        CheckControl(dialog);

        foundFinYear.Should().BeFalse("Financial Year Cycle belongs in Company Creation, not global startup");
    }

    [Fact]
    public void ApplicationStartupDialog_DoesNotContainProfileReadyBadge()
    {
        // Act
        using var dialog = new ApplicationStartupDialog();

        // Assert - verify no child controls contain 'Profile Ready'
        bool foundProfileReady = false;
        void CheckControl(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child.Text != null && child.Text.Contains("Profile Ready", System.StringComparison.OrdinalIgnoreCase))
                {
                    foundProfileReady = true;
                }
                CheckControl(child);
            }
        }
        CheckControl(dialog);

        foundProfileReady.Should().BeFalse("Profile Ready badge was removed from the header");
    }

    [Fact]
    public void ApplicationStartupDialog_ContainsHeaderCloseButton()
    {
        // Act
        using var dialog = new ApplicationStartupDialog();

        // Assert - verify header contains the close button with '✕'
        Button? closeBtn = null;
        void FindCloseButton(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is Button btn && btn.Text == "✕")
                {
                    closeBtn = btn;
                    return;
                }
                FindCloseButton(child);
            }
        }
        FindCloseButton(dialog);

        closeBtn.Should().NotBeNull("Dialog header must feature a close '✕' button");
    }
}
