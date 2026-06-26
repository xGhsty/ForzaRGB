using System.Runtime.InteropServices;

namespace ForzaRGB;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ForzaSledPacket
{
    public int   IsRaceOn;
    public uint  TimestampMS;
    public float EngineMaxRpm;
    public float EngineIdleRpm;
    public float CurrentEngineRpm;
    public float AccelerationX, AccelerationY, AccelerationZ;
    public float VelocityX, VelocityY, VelocityZ;
    public float AngularVelocityX, AngularVelocityY, AngularVelocityZ;
    public float Yaw, Pitch, Roll;
    public float NormalizedSuspensionTravelFrontLeft, NormalizedSuspensionTravelFrontRight;
    public float NormalizedSuspensionTravelRearLeft, NormalizedSuspensionTravelRearRight;
    public float TireSlipRatioFrontLeft, TireSlipRatioFrontRight;
    public float TireSlipRatioRearLeft, TireSlipRatioRearRight;
    public float WheelRotationSpeedFrontLeft, WheelRotationSpeedFrontRight;
    public float WheelRotationSpeedRearLeft, WheelRotationSpeedRearRight;
    public int   WheelOnRumbleStripFrontLeft, WheelOnRumbleStripFrontRight;
    public int   WheelOnRumbleStripRearLeft, WheelOnRumbleStripRearRight;
    public float WheelInPuddleDepthFrontLeft, WheelInPuddleDepthFrontRight;
    public float WheelInPuddleDepthRearLeft, WheelInPuddleDepthRearRight;
    public float SurfaceRumbleFrontLeft, SurfaceRumbleFrontRight;
    public float SurfaceRumbleRearLeft, SurfaceRumbleRearRight;
    public float TireSlipAngleFrontLeft, TireSlipAngleFrontRight;
    public float TireSlipAngleRearLeft, TireSlipAngleRearRight;
    public float TireCombinedSlipFrontLeft, TireCombinedSlipFrontRight;
    public float TireCombinedSlipRearLeft, TireCombinedSlipRearRight;
    public float SuspensionTravelMetersFrontLeft, SuspensionTravelMetersFrontRight;
    public float SuspensionTravelMetersRearLeft, SuspensionTravelMetersRearRight;
    public int   CarOrdinal;
    public int   CarClass;       // 0=D 1=C 2=B 3=A 4=S1 5=S2 6=R 7=X
    public int   CarPerformanceIndex;
    public int   DrivetrainType; // 0=FWD 1=RWD 2=AWD
    public int   NumCylinders;   // 0 = electric vehicle
    public int   CarGroup;
    public float SmashableVelDiff;
    public float SmashableMass;
    public float PositionX, PositionY, PositionZ;
    public float Speed;
    public float Power;
    public float Torque;
    public float TireTempFrontLeft, TireTempFrontRight;
    public float TireTempRearLeft, TireTempRearRight;
    public float Boost;
    public float Fuel;
    public float DistanceTraveled;
    public float BestLap, LastLap, CurrentLap, CurrentRaceTime;
    public ushort LapNumber;
    public byte   RacePosition;
    public byte   Accel, Brake, Clutch, HandBrake;
    public byte   Gear;   // 0=neutral, 1-10=gears, 11+=reverse
    public sbyte  Steer;
    public sbyte  NormalizedDrivingLine;
    public sbyte  NormalizedAIBrakeDifference;
}

public static class ForzaPacketExtensions
{
    public static ForzaSledPacket Parse(byte[] data)
    {
        if (data.Length < 324)
            throw new ArgumentException($"Packet too short: {data.Length} bytes (minimum 324)");

        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<ForzaSledPacket>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    public static float GetRpmNormalized(this ForzaSledPacket p)
    {
        float range = p.EngineMaxRpm - p.EngineIdleRpm;
        if (range <= 0) return 0f;
        return Math.Clamp((p.CurrentEngineRpm - p.EngineIdleRpm) / range, 0f, 1f);
    }

    public static bool IsGameActive(this ForzaSledPacket p) => p.IsRaceOn > 0;
}
