using System.Drawing;
using System.Windows.Forms;
using FluentAssertions;
using MoneyFlow.Desktop.Dialogs;
using Xunit;

namespace MoneyFlow.Tests.UiTests;

public class NoCompanySelectedDialogTests
{
    [Fact]
    public void NoCompanySelectedDialog_InitializesWithCorrectDimensionsAndPerimeterBorder()
    {
        // Act
        using var dialog = new NoCompanySelectedDialog();

        // Assert
        dialog.Width.Should().Be(580);
        dialog.Height.Should().Be(220);
        dialog.FormBorderStyle.Should().Be(FormBorderStyle.None);
        dialog.Padding.Should().Be(new Padding(1), "1px uniform perimeter frame border");
        dialog.BackColor.Should().Be(Color.FromArgb(17, 36, 58), "Deep navy border frame matching header");
    }

    [Fact]
    public void NoCompanySelectedDialog_ContainsFullWidthHeaderAndCloseButton()
    {
        // Act
        using var dialog = new NoCompanySelectedDialog();

        // Assert
        Control? pnlHeader = null;
        Button? btnClose = null;

        void FindHeader(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is Panel p && p.Dock == DockStyle.Top)
                {
                    pnlHeader = p;
                }
                if (c is Button b && b.Text == "✕")
                {
                    btnClose = b;
                }
                FindHeader(c);
            }
        }
        FindHeader(dialog);

        pnlHeader.Should().NotBeNull("Header panel must dock to Top across entire card width");
        pnlHeader!.Dock.Should().Be(DockStyle.Top);
        btnClose.Should().NotBeNull("Header must contain standard ✕ close button");
    }

    [Fact]
    public void NoCompanySelectedDialog_ContainsExitAndStayButtonsWithSufficientWidth()
    {
        // Act
        using var dialog = new NoCompanySelectedDialog();

        // Assert
        bool foundExit = false;
        bool foundStay = false;

        void FindButtons(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c.Text != null)
                {
                    if (c.Text.Contains("Exit"))
                    {
                        foundExit = true;
                        c.Width.Should().BeGreaterThanOrEqualTo(90, "Exit button needs sufficient width for DPI text rendering");
                    }
                    if (c.Text.Contains("Stay"))
                    {
                        foundStay = true;
                        c.Width.Should().BeGreaterThanOrEqualTo(80, "Stay button needs sufficient width");
                    }
                }
                FindButtons(c);
            }
        }
        FindButtons(dialog);

        foundExit.Should().BeTrue("Exit Application button must be present");
        foundStay.Should().BeTrue("Stay button must be present");
    }

    [Fact]
    public void NoCompanySelectedDialog_BodyPanel_IsNotOverlappedByHeader()
    {
        // Act
        using var dialog = new NoCompanySelectedDialog();
        dialog.Show();

        // Assert
        Panel? card = null;
        Panel? pnlHeader = null;
        Panel? pnlBody = null;

        foreach (Control c in dialog.Controls)
        {
            if (c is Panel p)
            {
                card = p;
                break;
            }
        }

        card.Should().NotBeNull();
        foreach (Control child in card!.Controls)
        {
            if (child is Panel p)
            {
                if (p.Dock == DockStyle.Top) pnlHeader = p;
                else if (p.Dock == DockStyle.Fill) pnlBody = p;
            }
        }

        pnlHeader.Should().NotBeNull();
        pnlBody.Should().NotBeNull();
        pnlBody!.Top.Should().BeGreaterThanOrEqualTo(pnlHeader!.Height, "Body panel must start below the header, not under it");

        dialog.Close();
    }
}

