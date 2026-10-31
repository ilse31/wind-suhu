using System.Drawing;

namespace WindSuhu;

internal sealed class Dashboard : Form
{
    private readonly Label _temperature = ValueLabel();
    private readonly Label _memory = ValueLabel();
    private readonly Label _disk = ValueLabel();
    private readonly ProgressBar _memoryBar = Progress();
    private readonly ProgressBar _diskBar = Progress();
    private readonly Label _sensorStatus = DetailLabel();

    public Dashboard()
    {
        Text = "Wind Suhu";
        Size = new Size(360, 310);
        MinimumSize = Size;
        MaximumSize = Size;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(23, 27, 35);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 9,
            BackColor = BackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 39));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(Heading("SUHU CPU", Color.FromArgb(255, 144, 89)), 0, 0);
        layout.Controls.Add(_temperature, 0, 1);
        layout.Controls.Add(Heading("RAM", Color.FromArgb(93, 180, 255)), 0, 2);
        layout.Controls.Add(_memory, 0, 3);
        layout.Controls.Add(_memoryBar, 0, 4);
        layout.Controls.Add(Heading("DISK SISTEM", Color.FromArgb(119, 218, 165)), 0, 5);
        layout.Controls.Add(_disk, 0, 6);
        layout.Controls.Add(_diskBar, 0, 7);
        layout.Controls.Add(_sensorStatus, 0, 8);

        Controls.Add(layout);
        FormClosing += (_, e) =>
        {
            if (e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true;
            Hide();
        };
    }

    public void UpdateMetrics(float? temperature, Usage? memory, Usage? disk, string drive)
    {
        _temperature.Text = temperature is null ? "— °C" : $"{temperature:0.#} °C";
        _memory.Text = memory is null ? "—" : $"{memory.Value.Percent:0}%   ·   {memory.Value.UsedGiB:0.0} / {memory.Value.TotalGiB:0.0} GiB";
        _disk.Text = disk is null ? "—" : $"{disk.Value.Percent:0}%   ·   {disk.Value.UsedGiB:0.0} / {disk.Value.TotalGiB:0.0} GiB ({drive})";
        _memoryBar.Value = memory is null ? 0 : Math.Clamp((int)Math.Round(memory.Value.Percent), 0, 100);
        _diskBar.Value = disk is null ? 0 : Math.Clamp((int)Math.Round(disk.Value.Percent), 0, 100);
    }

    public void UpdateSensorStatus(bool pawnIoInstalled, bool administrator, bool temperatureAvailable)
    {
        _sensorStatus.Text = temperatureAvailable ? "Sensor CPU terbaca." :
            !pawnIoInstalled ? "PawnIO belum terpasang. Klik kanan ikon → Unduh PawnIO resmi." :
            !administrator ? "PawnIO terpasang. Klik kanan ikon → Jalankan sebagai Administrator." :
            "PawnIO dan izin Administrator siap, tetapi sensor CPU belum terbaca.";
    }

    private static Label Heading(string text, Color color) => new()
    {
        Text = text,
        ForeColor = color,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 9, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Label ValueLabel() => new()
    {
        Text = "—",
        ForeColor = Color.White,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 15, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Label DetailLabel(string text = "") => new()
    {
        Text = text,
        ForeColor = Color.FromArgb(156, 164, 177),
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI", 8),
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static ProgressBar Progress() => new()
    {
        Dock = DockStyle.Fill,
        Maximum = 100,
        Style = ProgressBarStyle.Continuous
    };
}
