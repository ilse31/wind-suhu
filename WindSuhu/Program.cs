using System.Drawing;
using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.PawnIo;

namespace WindSuhu;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var mutex = new Mutex(true, "Local\\WindSuhu.SingleInstance", out var firstInstance);
        var ownsMutex = firstInstance;
        if (!ownsMutex)
        {
            if (!args.Contains("--elevated-restart")) return;
            try { ownsMutex = mutex.WaitOne(TimeSpan.FromSeconds(15)); }
            catch (AbandonedMutexException) { ownsMutex = true; }
            if (!ownsMutex) return;
        }
        try { Application.Run(new MonitorContext()); }
        finally { mutex.ReleaseMutex(); }
    }
}

internal sealed class MonitorContext : ApplicationContext
{
    private readonly Computer _computer = new() { IsCpuEnabled = true };
    private readonly NotifyIcon _temperatureIcon;
    private readonly NotifyIcon _memoryIcon;
    private readonly NotifyIcon _diskIcon;
    private readonly ContextMenuStrip _menu = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 2000 };
    private readonly Dashboard _dashboard = new();
    private readonly DriveInfo _systemDrive = new(Path.GetPathRoot(Environment.SystemDirectory)!);
    private readonly bool _isAdministrator;

    public MonitorContext()
    {
        using var identity = WindowsIdentity.GetCurrent();
        _isAdministrator = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        try { _computer.Open(); } catch { /* RAM and disk remain available. */ }
        _menu.Items.Add("Buka panel", null, (_, _) => ShowDashboard());
        _menu.Items.Add("Jalankan sebagai Administrator", null, (_, _) => RestartElevated());
        _menu.Items.Add("Unduh PawnIO resmi", null, (_, _) => OpenPawnIoPage());
        _menu.Items.Add("Keluar", null, (_, _) => ExitThread());

        _temperatureIcon = CreateIcon("°C", Color.FromArgb(255, 144, 89));
        _memoryIcon = CreateIcon("RAM", Color.FromArgb(93, 180, 255));
        _diskIcon = CreateIcon("DISK", Color.FromArgb(119, 218, 165));
        _timer.Tick += (_, _) => RefreshMetrics();
        _timer.Start();
        RefreshMetrics();
    }

    private NotifyIcon CreateIcon(string label, Color color)
    {
        var icon = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Icon = IconPainter.Create(label, "—", color),
            Text = "Wind Suhu: menunggu pembacaan",
            Visible = true
        };
        icon.MouseClick += (_, args) => { if (args.Button == MouseButtons.Left) ShowDashboard(); };
        return icon;
    }

    private void ShowDashboard()
    {
        _dashboard.Show();
        _dashboard.Activate();
    }

    private void RestartElevated()
    {
        try
        {
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Lokasi aplikasi tidak ditemukan.");
            var process = Process.Start(new ProcessStartInfo(executable)
            {
                Arguments = "--elevated-restart",
                UseShellExecute = true,
                Verb = "runas"
            });
            if (process is not null) ExitThread();
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223)
        {
            // The user canceled the Windows UAC prompt; keep this instance running.
        }
        catch (Exception error)
        {
            MessageBox.Show($"Tidak bisa menjalankan ulang aplikasi: {error.Message}",
                "Wind Suhu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void OpenPawnIoPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://pawnio.eu/") { UseShellExecute = true });
        }
        catch (Exception error)
        {
            MessageBox.Show($"Tidak bisa membuka situs PawnIO: {error.Message}",
                "Wind Suhu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RefreshMetrics()
    {
        try
        {
            var temperature = ReadCpuTemperature();
            var memory = ReadMemory();
            var disk = ReadDisk();

            SetIcon(_temperatureIcon, "°C", temperature is null ? "—" : Math.Round(temperature.Value).ToString("0"),
                temperature is null ? "Suhu CPU: sensor tidak tersedia" : $"Suhu CPU: {temperature:0.#} °C", Color.FromArgb(255, 144, 89));
            SetIcon(_memoryIcon, "RAM", memory is null ? "—" : Math.Round(memory.Value.Percent).ToString("0"),
                memory is null ? "RAM: tidak tersedia" : $"RAM: {memory.Value.UsedGiB:0.0} / {memory.Value.TotalGiB:0.0} GiB ({memory.Value.Percent:0}%)", Color.FromArgb(93, 180, 255));
            SetIcon(_diskIcon, "DISK", disk is null ? "—" : Math.Round(disk.Value.Percent).ToString("0"),
                disk is null ? "Disk: tidak tersedia" : $"Disk {_systemDrive.Name}: {disk.Value.UsedGiB:0.0} / {disk.Value.TotalGiB:0.0} GiB ({disk.Value.Percent:0}%)", Color.FromArgb(119, 218, 165));

            _dashboard.UpdateMetrics(temperature, memory, disk, _systemDrive.Name);
            _dashboard.UpdateSensorStatus(PawnIo.IsInstalled, _isAdministrator, temperature is not null);
        }
        catch { /* One failed sample must not close the tray application. */ }
    }

    private float? ReadCpuTemperature()
    {
        try
        {
            float? hottest = null;
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType != HardwareType.Cpu) continue;
                hardware.Update();
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType != SensorType.Temperature || sensor.Value is not float value) continue;
                    if (value is <= 0 or > 150) continue;
                    hottest = Math.Max(hottest ?? value, value);
                }
            }
            return hottest;
        }
        catch { return null; }
    }

    private static Usage? ReadMemory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0) return null;
        return Usage.FromBytes(status.TotalPhysical, status.TotalPhysical - status.AvailablePhysical);
    }

    private Usage? ReadDisk()
    {
        try
        {
            if (!_systemDrive.IsReady || _systemDrive.TotalSize <= 0) return null;
            return Usage.FromBytes((ulong)_systemDrive.TotalSize,
                (ulong)(_systemDrive.TotalSize - _systemDrive.AvailableFreeSpace));
        }
        catch { return null; }
    }

    private static void SetIcon(NotifyIcon target, string label, string number, string tooltip, Color color)
    {
        var next = IconPainter.Create(label, number, color);
        var previous = target.Icon;
        target.Icon = next;
        target.Text = tooltip.Length <= 63 ? tooltip : tooltip[..63];
        previous?.Dispose();
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        _timer.Dispose();
        foreach (var icon in new[] { _temperatureIcon, _memoryIcon, _diskIcon })
        {
            icon.Visible = false;
            icon.Icon?.Dispose();
            icon.Dispose();
        }
        _dashboard.Dispose();
        _menu.Dispose();
        _computer.Close();
        base.ExitThreadCore();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}

internal readonly record struct Usage(double TotalGiB, double UsedGiB, double Percent)
{
    public static Usage FromBytes(ulong total, ulong used) => new(
        total / 1073741824d, used / 1073741824d, used * 100d / total);
}

internal static class IconPainter
{
    public static Icon Create(string label, string value, Color accent)
    {
        using var bitmap = new Bitmap(64, 64);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using var background = new SolidBrush(Color.FromArgb(27, 32, 42));
            using var foreground = new SolidBrush(Color.White);
            using var accentBrush = new SolidBrush(accent);
            graphics.FillRoundedRectangle(background, new Rectangle(0, 0, 64, 64), 13);
            using var labelFont = new Font("Segoe UI", 11, FontStyle.Bold, GraphicsUnit.Pixel);
            using var valueFont = new Font("Segoe UI", value.Length > 2 ? 29 : 34, FontStyle.Bold, GraphicsUnit.Pixel);
            var format = new StringFormat { Alignment = StringAlignment.Center };
            graphics.DrawString(label, labelFont, accentBrush, new RectangleF(0, 4, 64, 17), format);
            graphics.DrawString(value, valueFont, foreground, new RectangleF(0, 20, 64, 39), format);
        }
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
    }

    private static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle bounds, int radius)
    {
        using var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}
