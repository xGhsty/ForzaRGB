using System.Runtime.InteropServices;

namespace ForzaRGB;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void CorsairSessionStateChangedHandler(IntPtr context, IntPtr eventData);

public class IcueService : IDisposable
{
    private bool    _connected   = false;
    private string? _fanDeviceId = null;
    private uint[]? _fanLedIds   = null;
    private uint[]? _headLedIds  = null;

    private readonly CorsairSessionStateChangedHandler _sessionCallback;
    private volatile bool _sessionConnected = false;

    // Blink speeds in ms (on/off)
    public enum BlinkSpeed { Slow, Normal, Fast }
    private const int BlinkSlowOnMs  = 150, BlinkSlowOffMs  = 150;
    private const int BlinkOnMs      = 110, BlinkOffMs      = 110;
    private const int BlinkFastOnMs  =  35, BlinkFastOffMs  =  35;

    private volatile bool       _blinkActive = false;
    private volatile BlinkSpeed _blinkSpeed  = BlinkSpeed.Normal;
    private Thread?             _blinkThread = null;
    private volatile byte       _blinkR, _blinkG, _blinkB;

    private volatile bool _idleAnimActive = false;
    private volatile bool _headAnimActive = false;
    private Thread?       _idleThread     = null;
    private Thread?       _headThread     = null;

    // Redline learning
    private int                    _trackedCarOrdinal    = -1;
    private float                  _observedMaxRpm       = 0f;
    private bool                   _rpmLearned           = false;
    private int                    _lastGear             = -1;
    private int                    _gearChangeSkip       = 0;
    private int                    _fakeReadingCooldown  = 0;
    private float                  _lastRpm              = 0f;
    private List<float>            _rpmSamples           = new();
    private Dictionary<int, float> _gearBestRpm          = new();
    private CarRpmDatabase         _rpmDb                = new();

    // Idle animation palette — FH6 colors
    private static readonly (float R, float G, float B)[] IdlePalette =
    [
        (255, 20,  147),
        ( 57, 255,  20),
        (  0, 191, 255),
        (255, 20,  147),
    ];

    public IcueService()
    {
        _sessionCallback = OnSessionStateChanged;
    }

    private void OnSessionStateChanged(IntPtr context, IntPtr eventData)
    {
        if (eventData != IntPtr.Zero)
        {
            int state = Marshal.ReadInt32(eventData);
            if (state == 3) _sessionConnected = true;
        }
    }

    public bool Connect()
    {
        string? dllName = CorsairApi.FindAndLoad();
        if (dllName == null)
        {
            Console.WriteLine("[iCUE] iCUE SDK DLL not found!");
            Console.WriteLine("[iCUE] Download from: https://github.com/CorsairOfficial/cue-sdk/releases");
            Console.WriteLine("[iCUE] Place any DLL with 'iCUESDK' in the name next to ForzaRGB.exe");
            return false;
        }

        Console.WriteLine($"[iCUE] Loaded {dllName}");

        try
        {
            var callbackPtr = Marshal.GetFunctionPointerForDelegate(_sessionCallback);
            int err = CorsairApi.CorsairConnect(callbackPtr, IntPtr.Zero);
            if (err != 0)
            {
                Console.WriteLine($"[iCUE] Connection failed, code: {err}");
                return false;
            }

            Console.WriteLine("[iCUE] Connecting to iCUE...");
            for (int i = 0; i < 50; i++)
            {
                Thread.Sleep(100);
                if (_sessionConnected) break;
            }

            if (!_sessionConnected)
                Console.WriteLine("[iCUE] Session timeout - trying anyway...");

            _connected = true;
            Console.WriteLine("[iCUE] Connected to iCUE SDK v4.");
            return DiscoverFanDevice();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[iCUE] Unexpected error: {ex.Message}");
            return false;
        }
    }

    private bool DiscoverFanDevice()
    {
        const int maxDevices = 32;
        int deviceInfoSize   = Marshal.SizeOf<CorsairDeviceInfo>();
        IntPtr devicesBuf    = Marshal.AllocHGlobal(maxDevices * deviceInfoSize);

        try
        {
            var filter = new CorsairDeviceFilter { deviceTypeMask = (int)CorsairDeviceType.All };
            int err    = CorsairApi.CorsairGetDevices(ref filter, maxDevices, devicesBuf, out int count);

            if (err != 0) { Console.WriteLine($"[iCUE] GetDevices error: {err}"); return false; }

            Console.WriteLine($"[iCUE] Found {count} device(s):");
            for (int i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<CorsairDeviceInfo>(devicesBuf + i * deviceInfoSize);
                Console.WriteLine($"[iCUE]   [{i}] {info.model} (LEDs: {info.ledCount})");

                if (info.model != null && info.model.Contains("LINK System Hub", StringComparison.OrdinalIgnoreCase))
                {
                    _fanDeviceId = info.id;
                    Console.WriteLine($"[iCUE] ✓ Found iCUE LINK System Hub ({info.ledCount} LEDs)");
                    LoadLeds(info.id);
                    return true;
                }
            }

            Console.WriteLine("[iCUE] iCUE LINK System Hub not found. Is it connected? Is it connected?");
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(devicesBuf);
        }
    }

    private void LoadLeds(string deviceId)
    {
        const int maxLeds = 64;
        int ledPosSize    = Marshal.SizeOf<CorsairLedPosition>();
        IntPtr ledsBuf    = Marshal.AllocHGlobal(maxLeds * ledPosSize);

        try
        {
            CorsairApi.CorsairGetLedPositions(deviceId, maxLeds, ledsBuf, out int count);

            var fanLeds  = new List<uint>();
            var headLeds = new List<uint>();

            for (int i = 0; i < count; i++)
            {
                var pos    = Marshal.PtrToStructure<CorsairLedPosition>(ledsBuf + i * ledPosSize);
                uint group = (pos.id >> 16) & 0xFFFF;

                if (group == 0x000C)      fanLeds.Add(pos.id);  // RX RGB fans
                else if (group == 0x000B) headLeds.Add(pos.id); // TITAN pump head
            }

            _fanLedIds  = fanLeds.ToArray();
            _headLedIds = headLeds.ToArray();
            Console.WriteLine($"[iCUE] Loaded {_fanLedIds.Length} fan LEDs + {_headLedIds.Length} head LEDs.");
        }
        finally
        {
            Marshal.FreeHGlobal(ledsBuf);
        }
    }

    public void SetFanColor(byte r, byte g, byte b)
    {
        if (!_connected || _fanDeviceId == null || _fanLedIds == null) return;

        var leds = _fanLedIds
            .Select(id => new CorsairLedColor { id = id, r = r, g = g, b = b, a = 255 })
            .ToArray();

        CorsairApi.CorsairSetLedColors(_fanDeviceId, leds.Length, leds);
    }

    public void SetColorFromClassAndRpm(CarClass carClass, float rpmNormalized, int gear,
                                        float currentRpm, float maxRpm, float idleRpm, int carOrdinal)
    {
        var (r, g, b) = RpmColorMapper.GetColor(carClass, rpmNormalized);

        if (carOrdinal != _trackedCarOrdinal)
        {
            _trackedCarOrdinal   = carOrdinal;
            float? saved         = _rpmDb.GetMaxRpm(carOrdinal, maxRpm);
            _observedMaxRpm      = saved ?? 0f;
            _rpmLearned          = saved.HasValue;
            _lastGear            = -1;
            _gearChangeSkip      = 0;
            _fakeReadingCooldown = 0;
            _lastRpm             = 0f;
            _rpmSamples.Clear();
            _gearBestRpm.Clear();

            if (saved.HasValue)
                Console.WriteLine($"[DB] Car #{carOrdinal} — saved redline: {saved:F0} RPM");
            else
                Console.WriteLine($"[DB] Car #{carOrdinal} — unknown car, learning redline...");
        }

        // Sample RPM at gear change — only when previous gear had high RPM
        if (gear != _lastGear)
        {
            bool prevGearValid = _lastGear >= 2 && _lastGear <= 10;
            bool prevRpmValid  = _lastRpm >= maxRpm * 0.80f && _lastRpm <= maxRpm * 0.93f;

            if (prevGearValid && prevRpmValid && _fakeReadingCooldown == 0)
            {
                float prevBest = _gearBestRpm.GetValueOrDefault(_lastGear, 0f);
                if (_lastRpm > prevBest)
                {
                    _gearBestRpm[_lastGear] = _lastRpm;
                    _rpmSamples.Add(_lastRpm);

                    if (_rpmSamples.Count >= 3)
                    {
                        float median = _rpmSamples.OrderBy(x => x).ElementAt(_rpmSamples.Count / 2);
                        if (median > _observedMaxRpm)
                        {
                            _observedMaxRpm = median;
                            _rpmLearned     = true;
                            _rpmDb.UpdateMaxRpm(carOrdinal, maxRpm, median);
                            Console.WriteLine($"[DB] Car #{carOrdinal} — learned redline: {median:F0} RPM ({median/maxRpm:P0})");
                        }
                    }
                }
            }

            _lastGear       = gear;
            _gearChangeSkip = 30;
        }
        if (_gearChangeSkip > 0) _gearChangeSkip--;

        if (currentRpm >= maxRpm * 0.99f)
            _fakeReadingCooldown = 60;
        else if (_fakeReadingCooldown > 0)
            _fakeReadingCooldown--;

        _lastRpm = currentRpm;

        // Blink thresholds
        bool isFirstGear = gear == 1 || gear > 10;
        float threshold1 = maxRpm * 0.80f;
        float threshold2 = _rpmLearned ? _observedMaxRpm * 0.92f : maxRpm * 0.87f;
        float threshold3 = _rpmLearned ? _observedMaxRpm * 0.98f : maxRpm * 0.93f;
        float blinkOff   = threshold1 * 0.94f;

        if (currentRpm >= threshold3)
            StartBlink(r, g, b, BlinkSpeed.Fast);
        else if (currentRpm >= threshold2)
            StartBlink(r, g, b, BlinkSpeed.Normal);
        else if (currentRpm >= threshold1)
            StartBlink(r, g, b, BlinkSpeed.Slow);
        else if (currentRpm < blinkOff)
        {
            StopBlink();
            SetFanColor(r, g, b);
        }
    }

    public void SetElectricColor(float speedNormalized, CarClass carClass)
    {
        if (!_connected || _fanDeviceId == null || _fanLedIds == null) return;
        StopBlink();

        int ledCount = _fanLedIds.Length;
        var classColors = RpmColorMapper.ClassColors;
        var (cr, cg, cb) = classColors.TryGetValue(carClass, out var c) ? c : (255, 215, 0);

        // Aqua color for EVs
        const byte er = 0, eg = 210, eb = 255;

        var leds = new CorsairLedColor[ledCount];
        for (int i = 0; i < ledCount; i++)
        {
            float ledThreshold = (float)i / ledCount;
            float blend        = Math.Clamp((speedNormalized - ledThreshold) * ledCount, 0f, 1f);

            leds[i] = new CorsairLedColor
            {
                id = _fanLedIds[i],
                r  = (byte)(cr + (er - cr) * blend),
                g  = (byte)(cg + (eg - cg) * blend),
                b  = (byte)(cb + (eb - cb) * blend),
                a  = 255
            };
        }
        CorsairApi.CorsairSetLedColors(_fanDeviceId, leds.Length, leds);
    }

    private void StartBlink(byte r, byte g, byte b, BlinkSpeed speed = BlinkSpeed.Normal)
    {
        _blinkR     = r;
        _blinkG     = g;
        _blinkB     = b;
        _blinkSpeed = speed;

        if (_blinkActive) return;
        _blinkActive = true;

        _blinkThread = new Thread(() =>
        {
            while (_blinkActive)
            {
                int onMs  = _blinkSpeed switch { BlinkSpeed.Slow => BlinkSlowOnMs,  BlinkSpeed.Fast => BlinkFastOnMs,  _ => BlinkOnMs  };
                int offMs = _blinkSpeed switch { BlinkSpeed.Slow => BlinkSlowOffMs, BlinkSpeed.Fast => BlinkFastOffMs, _ => BlinkOffMs };

                SetFanColor(_blinkR, _blinkG, _blinkB);
                Thread.Sleep(onMs);
                if (!_blinkActive) break;
                SetFanColor(0, 0, 0);
                Thread.Sleep(offMs);
            }
        })
        { IsBackground = true };

        _blinkThread.Start();
    }

    public void StopBlink()
    {
        _blinkActive = false;
        _blinkThread = null;
    }

    public void StartIdleAnimation()
    {
        StopBlink();
        if (_idleAnimActive) return;
        _idleAnimActive = true;

        _idleThread = new Thread(() =>
        {
            int    ledCount = _fanLedIds?.Length ?? 16;
            double time     = 0;

            while (_idleAnimActive)
            {
                if (!_connected || _fanDeviceId == null || _fanLedIds == null) break;

                var leds = new CorsairLedColor[ledCount];
                for (int i = 0; i < ledCount; i++)
                {
                    double phase     = time + (double)i / ledCount * 2.0;
                    var (r, g, b)    = SamplePalette((float)(phase % 1.0));
                    float brightness = 0.5f + 0.5f * (float)((Math.Sin(phase * Math.PI * 2) + 1) / 2);
                    leds[i] = new CorsairLedColor
                    {
                        id = _fanLedIds[i],
                        r  = (byte)(r * brightness),
                        g  = (byte)(g * brightness),
                        b  = (byte)(b * brightness),
                        a  = 255
                    };
                }
                CorsairApi.CorsairSetLedColors(_fanDeviceId, leds.Length, leds);
                time += 0.008;
                if (time >= 1.0) time -= 1.0;
                Thread.Sleep(16);
            }
        })
        { IsBackground = true };
        _idleThread.Start();
    }

    public void StartHeadAnimation()
    {
        if (_headAnimActive) return;
        _headAnimActive = true;

        _headThread = new Thread(() =>
        {
            int    ledCount = _headLedIds?.Length ?? 20;
            double time     = 0.5;

            while (_headAnimActive)
            {
                if (!_connected || _fanDeviceId == null || _headLedIds == null) break;

                var leds = new CorsairLedColor[ledCount];
                for (int i = 0; i < ledCount; i++)
                {
                    double phase     = time + (double)i / ledCount * 2.0;
                    var (r, g, b)    = SamplePalette((float)(phase % 1.0));
                    float brightness = 0.5f + 0.5f * (float)((Math.Sin(phase * Math.PI * 2) + 1) / 2);
                    leds[i] = new CorsairLedColor
                    {
                        id = _headLedIds[i],
                        r  = (byte)(r * brightness),
                        g  = (byte)(g * brightness),
                        b  = (byte)(b * brightness),
                        a  = 255
                    };
                }
                CorsairApi.CorsairSetLedColors(_fanDeviceId, leds.Length, leds);
                time += 0.018;
                if (time >= 1.0) time -= 1.0;
                Thread.Sleep(16);
            }
        })
        { IsBackground = true };
        _headThread.Start();
    }

    private static (float R, float G, float B) SamplePalette(float t)
    {
        float scaled = t * (IdlePalette.Length - 1);
        int   idx    = (int)scaled;
        float frac   = scaled - idx;

        if (idx >= IdlePalette.Length - 1) return IdlePalette[^1];

        var (r1, g1, b1) = IdlePalette[idx];
        var (r2, g2, b2) = IdlePalette[idx + 1];
        return (r1 + (r2 - r1) * frac, g1 + (g2 - g1) * frac, b1 + (b2 - b1) * frac);
    }

    public void StopIdleAnimation()
    {
        _idleAnimActive = false;
        _idleThread     = null;
    }

    public void StopHeadAnimation()
    {
        _headAnimActive = false;
        _headThread     = null;
    }

    public void SetIdleColor()
    {
        StopBlink();
        StopIdleAnimation();
        var (r, g, b) = RpmColorMapper.IdleColor;
        SetFanColor(r, g, b);
    }

    public void Dispose()
    {
        if (_connected)
        {
            StopBlink();
            StopIdleAnimation();
            StopHeadAnimation();
            SetIdleColor();
            Thread.Sleep(100);
            CorsairApi.CorsairDisconnect();
            _connected = false;
        }
    }
}
