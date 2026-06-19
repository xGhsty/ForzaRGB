using ForzaRGB;

// Global exception handler — catch any unhandled crash
AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    Console.WriteLine($"\n[CRASH] Unhandled exception: {e.ExceptionObject}");
    Console.WriteLine("\nPress any key to exit...");
    Console.ReadKey();
};

// Allocate console window for WinExe
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
static extern bool AllocConsole();
AllocConsole();

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title = "ForzaRGB - Forza Horizon 6 x iCUE LINK Sync";
Console.WriteLine("╔════════════════════════════════════════╗");
Console.WriteLine("║   ForzaRGB v3.0.9 — by xGhosty         ║");
Console.WriteLine("║   Forza Horizon 6 x iCUE LINK RGB      ║");
Console.WriteLine("╚════════════════════════════════════════╝\n");

using var icue = new IcueService();
using var udp  = new ForzaUdpService(port: 7777);

if (!icue.Connect())
{
    Console.WriteLine("\nCould not connect to iCUE. Press any key to exit...");
    Console.ReadKey();
    return;
}

icue.StartIdleAnimation();
icue.StartHeadAnimation();

CarClass lastClass        = CarClass.Unknown;
int      logCounter       = 0;
float    electricTopSpeed = 0f;
var      rpmDb            = new CarRpmDatabase();

Console.WriteLine("\nRunning. Press Q to quit.");
Console.WriteLine("Waiting for Forza Horizon 6...");
Console.WriteLine("(Settings -> HUD and Gameplay -> Telemetry -> 127.0.0.1:7777)\n");

udp.OnPacketReceived += packet =>
{
    if (!packet.IsGameActive() || (packet.CurrentEngineRpm == 0 && packet.Speed == 0))
    {
        icue.StartIdleAnimation();
        return;
    }

    icue.StopIdleAnimation();

    bool isElectric = packet.NumCylinders == 0 || packet.EngineIdleRpm == 0;

    CarClass carClass = RpmColorMapper.FromForzaInt(packet.CarClass);
    float    rpmNorm  = packet.GetRpmNormalized();
    int      gear     = packet.Gear;

    if (carClass != lastClass)
    {
        lastClass        = carClass;
        electricTopSpeed = 0f;
        string type = isElectric ? "electric [EV]" : "combustion";
        Console.WriteLine($"[Forza] Car class: {carClass} ({type})");
    }

    if (isElectric && electricTopSpeed == 0f)
    {
        float? saved = rpmDb.GetEvTopSpeed(packet.CarOrdinal);
        if (saved.HasValue)
        {
            electricTopSpeed = saved.Value;
            Console.WriteLine($"[DB] EV #{packet.CarOrdinal} — saved top speed: {saved:F0} km/h");
        }
    }

    if (++logCounter >= 60)
    {
        logCounter = 0;

        if (!isElectric && rpmNorm > 0.05f && gear != 11)
        {
            var (r, g, b) = RpmColorMapper.GetColor(carClass, rpmNorm);
            string gearDisplay = gear == 0 ? "R" : gear.ToString();
            Console.WriteLine($"[Forza] RPM: {packet.CurrentEngineRpm:F0}/{packet.EngineMaxRpm:F0} ({rpmNorm:P0}) Gear:{gearDisplay} -> RGB({r},{g},{b})");
        }
        else if (isElectric && packet.Speed > 1f)
        {
            Console.WriteLine($"[Forza] Electric - speed: {packet.Speed * 3.6f:F0} km/h");
        }
    }

    if (isElectric)
    {
        float speedKmh = packet.Speed * 3.6f;
        if (speedKmh > electricTopSpeed)
        {
            electricTopSpeed = speedKmh;
            rpmDb.UpdateEvTopSpeed(packet.CarOrdinal, speedKmh);
        }

        float refSpeed  = Math.Max(electricTopSpeed, 100f);
        float speedNorm = Math.Clamp(speedKmh / refSpeed, 0f, 1f);
        icue.SetElectricColor(speedNorm, carClass);
    }
    else
    {
        icue.SetColorFromClassAndRpm(carClass, rpmNorm, gear,
                                     packet.CurrentEngineRpm, packet.EngineMaxRpm,
                                     packet.EngineIdleRpm, packet.CarOrdinal,
                                     packet.Speed * 3.6f);
    }
};

udp.OnConnectionLost += () =>
{
    lastClass = CarClass.Unknown;
    icue.StartIdleAnimation();
};

// Handle system resume after sleep — restart UDP listener
Microsoft.Win32.SystemEvents.PowerModeChanged += (sender, e) =>
{
    if (e.Mode == Microsoft.Win32.PowerModes.Resume)
    {
        Console.WriteLine("[System] Resumed from sleep — restarting...");
        udp.Restart();
    }
};

udp.Start();

while (Console.ReadKey(intercept: true).Key != ConsoleKey.Q) { }

Console.WriteLine("\nShutting down...");
