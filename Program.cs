using ForzaRGB;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.Title = "ForzaRGB - Forza Horizon 6 x iCUE LINK Sync";
Console.WriteLine("╔════════════════════════════════════════╗");
Console.WriteLine("║   ForzaRGB v2.9.6 — by xGhosty         ║");
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

udp.OnPacketReceived += packet =>
{
    if (!packet.IsGameActive())
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
        // Load saved topspeed for this EV on first packet
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
        if (!isElectric && rpmNorm > 0.05f)
        {
            var (r, g, b) = RpmColorMapper.GetColor(carClass, rpmNorm);
            if (gear != 11) // nie pokazuj neutralu
            {
                string gearDisplay = gear == 0 ? "R" : gear.ToString();
                Console.WriteLine($"[Forza] RPM: {packet.CurrentEngineRpm:F0}/{packet.EngineMaxRpm:F0} ({rpmNorm:P0}) Gear:{gearDisplay} -> RGB({r},{g},{b})");
            }
        }
        else if (isElectric && packet.Speed > 1f)
        {
            float speedKmh = packet.Speed * 3.6f;
            Console.WriteLine($"[Forza] Electric - speed: {speedKmh:F0} km/h");
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
                                     packet.EngineIdleRpm, packet.CarOrdinal);
    }
};

udp.OnConnectionLost += () =>
{
    lastClass = CarClass.Unknown;
    icue.StartIdleAnimation();
};

udp.Start();

Console.WriteLine("\nRunning. Press Q to quit.\n");
Console.WriteLine("Waiting for Forza Horizon 6...");
Console.WriteLine("(Settings -> Telemetry -> 127.0.0.1:7777)\n");

while (Console.ReadKey(intercept: true).Key != ConsoleKey.Q) { }

Console.WriteLine("\nShutting down...");
