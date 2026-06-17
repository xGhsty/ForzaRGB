namespace ForzaRGB;

public enum CarClass
{
    D = 0, C = 1, B = 2, A = 3,
    S1 = 4, S2 = 5, R = 6, X = 7,
    Unknown = -1
}

public static class RpmColorMapper
{
    public static readonly Dictionary<CarClass, (int R, int G, int B)> ClassColors = new()
    {
        { CarClass.D,  (  0, 191, 255) },
        { CarClass.C,  (255, 215,   0) },
        { CarClass.B,  (255, 106,   0) },
        { CarClass.A,  (255,  26,  75) },
        { CarClass.S1, (160,  32, 240) },
        { CarClass.S2, ( 58, 127, 255) },
        { CarClass.R,  (255,  20, 147) },
        { CarClass.X,  ( 57, 255,  20) },
    };

    private const float DarkFactor    = 0.15f;
    private const float RedlineStart  = 0.60f;
    private const float RedlineFullAt = 0.82f;

    private static readonly (int R, int G, int B) RedlineColor = (255, 0, 0);

    public static (byte R, byte G, byte B) GetColor(CarClass carClass, float rpmNormalized)
    {
        rpmNormalized = Math.Clamp(rpmNormalized, 0f, 1f);
        var baseColor = ClassColors.TryGetValue(carClass, out var c) ? c : ClassColors[CarClass.D];

        if (rpmNormalized < RedlineStart)
        {
            float t          = rpmNormalized / RedlineStart;
            float brightness = 1f - t * (1f - DarkFactor);
            return (
                R: (byte)(baseColor.R * brightness),
                G: (byte)(baseColor.G * brightness),
                B: (byte)(baseColor.B * brightness)
            );
        }
        else
        {
            float t       = Math.Clamp((rpmNormalized - RedlineStart) / (RedlineFullAt - RedlineStart), 0f, 1f);
            var darkColor = (
                R: (int)(baseColor.R * DarkFactor),
                G: (int)(baseColor.G * DarkFactor),
                B: (int)(baseColor.B * DarkFactor)
            );
            var blended = Lerp(darkColor, RedlineColor, t);

            if (t > 0.7f)
            {
                float clean = (t - 0.7f) / 0.3f;
                return (blended.R, (byte)(blended.G * (1f - clean)), (byte)(blended.B * (1f - clean)));
            }
            return blended;
        }
    }

    public static CarClass FromForzaInt(int value) => value switch
    {
        0 => CarClass.D,  1 => CarClass.C,
        2 => CarClass.B,  3 => CarClass.A,
        4 => CarClass.S1, 5 => CarClass.S2,
        6 => CarClass.R,  7 => CarClass.X,
        _ => CarClass.Unknown
    };

    private static (byte R, byte G, byte B) Lerp((int R, int G, int B) from, (int R, int G, int B) to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return (
            R: (byte)(from.R + (to.R - from.R) * t),
            G: (byte)(from.G + (to.G - from.G) * t),
            B: (byte)(from.B + (to.B - from.B) * t)
        );
    }

    public static (byte R, byte G, byte B) IdleColor => (0, 20, 60);
}
