using System.Drawing;
using System.Drawing.Drawing2D;
using TmrOverlay.App.Telemetry;

namespace TmrOverlay.App;

internal sealed class StatusOverlayForm : Form
{
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;

    private readonly TelemetryCaptureState _state;
    private readonly Panel _indicatorPanel;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly Label _detailLabel;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    public StatusOverlayForm(TelemetryCaptureState state)
    {
        _state = state;

        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.FromArgb(26, 26, 26);
        ClientSize = new Size(280, 88);
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.None;
        Location = new Point(24, 24);
        MaximizeBox = false;
        MinimizeBox = false;
        Opacity = 0.88d;
        Padding = new Padding(16, 14, 16, 14);
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;

        _indicatorPanel = new Panel
        {
            Location = new Point(16, 16),
            Size = new Size(12, 12),
            BackColor = Color.FromArgb(140, 140, 140)
        };
        _indicatorPanel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(_indicatorPanel.BackColor);
            e.Graphics.FillEllipse(brush, 0, 0, _indicatorPanel.Width - 1, _indicatorPanel.Height - 1);
        };

        _titleLabel = new Label
        {
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold, GraphicsUnit.Point),
            Location = new Point(36, 10),
            Text = "TmrOverlay"
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(16, 34),
            Size = new Size(248, 22),
            Text = "Waiting for iRacing"
        };

        _detailLabel = new Label
        {
            AutoSize = false,
            ForeColor = Color.FromArgb(180, 180, 180),
            Font = new Font("Consolas", 9f, FontStyle.Regular, GraphicsUnit.Point),
            Location = new Point(16, 56),
            Size = new Size(248, 18),
            Text = "collector idle"
        };

        Controls.Add(_indicatorPanel);
        Controls.Add(_titleLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_detailLabel);

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = 250
        };
        _refreshTimer.Tick += (_, _) => RefreshOverlay();
        _refreshTimer.Start();

        RefreshOverlay();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var createParams = base.CreateParams;
            createParams.ExStyle |= WsExTransparent | WsExToolWindow | WsExNoActivate;
            return createParams;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            _titleLabel.Dispose();
            _statusLabel.Dispose();
            _detailLabel.Dispose();
            _indicatorPanel.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmMouseActivate)
        {
            message.Result = (IntPtr)MaNoActivate;
            return;
        }

        base.WndProc(ref message);
    }

    private void RefreshOverlay()
    {
        var snapshot = _state.Snapshot();

        if (snapshot.IsCapturing)
        {
            BackColor = Color.FromArgb(18, 46, 34);
            _indicatorPanel.BackColor = Color.FromArgb(80, 214, 124);
            _statusLabel.Text = "Collecting live session data";
            _detailLabel.Text = $"frames {snapshot.FrameCount,7:N0}   drops {snapshot.DroppedFrameCount,4:N0}";
        }
        else if (snapshot.IsConnected)
        {
            BackColor = Color.FromArgb(56, 44, 14);
            _indicatorPanel.BackColor = Color.FromArgb(242, 176, 64);
            _statusLabel.Text = "Connected to iRacing";
            _detailLabel.Text = "waiting for first telemetry frame";
        }
        else
        {
            BackColor = Color.FromArgb(26, 26, 26);
            _indicatorPanel.BackColor = Color.FromArgb(140, 140, 140);
            _statusLabel.Text = "Waiting for iRacing";
            _detailLabel.Text = "collector idle";
        }

        _indicatorPanel.Invalidate();
    }
}
