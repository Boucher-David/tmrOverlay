using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using TmrOverlay.App.Overlays.Styling;

namespace TmrOverlay.App.Overlays.Abstractions;

internal sealed class OverlayTableLayoutPanel : TableLayoutPanel
{
    public OverlayTableLayoutPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw,
            true);

        BackColor = OverlayTheme.Colors.PanelBackground;
        CellBorderStyle = TableLayoutPanelCellBorderStyle.None;
        Margin = Padding.Empty;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (ClientSize.Width <= 1 || ClientSize.Height <= 1)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.None;
        using var pen = new Pen(OverlayTheme.Colors.WindowBorder);
        var right = ClientSize.Width - 1;
        var bottom = ClientSize.Height - 1;
        e.Graphics.DrawRectangle(pen, 0, 0, right, bottom);

        var x = 0;
        foreach (var width in GetColumnWidths().Take(Math.Max(0, ColumnCount - 1)))
        {
            x += width;
            if (x > 0 && x < ClientSize.Width)
            {
                e.Graphics.DrawLine(pen, x, 0, x, bottom);
            }
        }

        var y = 0;
        foreach (var height in GetRowHeights().Take(Math.Max(0, RowCount - 1)))
        {
            y += height;
            if (y > 0 && y < ClientSize.Height)
            {
                e.Graphics.DrawLine(pen, 0, y, right, y);
            }
        }
    }
}
