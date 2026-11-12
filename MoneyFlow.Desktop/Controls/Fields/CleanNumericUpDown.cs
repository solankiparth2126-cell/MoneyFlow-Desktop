using System;
using System.Drawing;
using System.Windows.Forms;

namespace MoneyFlow.Desktop.Controls.Fields;

/// <summary>
/// A clean numeric input control for ERP forms that removes native up/down spinner arrows
/// while preserving NumericUpDown decimal handling, precision, formatting, and validation.
/// </summary>
public class CleanNumericUpDown : NumericUpDown
{
    public CleanNumericUpDown()
    {
        TabStop = true;
        if (Controls.Count > 0)
        {
            Controls[0].Visible = false;
            Controls[0].Width = 0;
            Controls[0].TabStop = false;
        }
        if (Controls.Count > 1)
        {
            Controls[1].TabStop = true;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (Controls.Count > 0)
        {
            Controls[0].Visible = false;
            Controls[0].Width = 0;
            Controls[0].TabStop = false;
        }
        if (Controls.Count > 1)
        {
            Controls[1].TabStop = true;
        }
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (Controls.Count > 0)
        {
            Controls[0].Visible = false;
            Controls[0].Width = 0;
            Controls[0].TabStop = false;
        }
        if (Controls.Count > 1)
        {
            Controls[1].TabStop = true;
            Controls[1].Bounds = new Rectangle(0, (Height - Controls[1].Height) / 2, Width, Controls[1].Height);
        }
    }
}
