using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.PawnIo;
using System.Security.Principal;

using var identity = WindowsIdentity.GetCurrent();
var admin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
Console.WriteLine($"Administrator: {admin}");
Console.WriteLine($"PawnIO installed: {PawnIo.IsInstalled} (version: {PawnIo.Version?.ToString() ?? "none"})");
var computer = new Computer { IsCpuEnabled = true };
try
{
    computer.Open();
    foreach (var hardware in computer.Hardware.Where(h => h.HardwareType == HardwareType.Cpu))
    {
        hardware.Update();
        Console.WriteLine($"CPU: {hardware.Name}");
        foreach (var sensor in hardware.Sensors.Where(s => s.SensorType == SensorType.Temperature))
            Console.WriteLine($"  {sensor.Name}: {sensor.Value?.ToString() ?? "null"} °C");
    }
}
catch (Exception error)
{
    Console.WriteLine($"Sensor error: {error.GetType().Name}: {error.Message}");
}
finally
{
    computer.Close();
}
