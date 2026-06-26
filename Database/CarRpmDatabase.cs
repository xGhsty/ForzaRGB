using System.Text.Json;

namespace ForzaRGB;

public class CarRpmDatabase
{
    private readonly string _filePath;
    private readonly Dictionary<string, float> _data;
    private const float SaveThreshold = 50f;

    public CarRpmDatabase()
    {
        _filePath = Path.Combine(AppContext.BaseDirectory, "car_rpm_data.json");
        _data     = Load();
        Console.WriteLine($"[DB] Loaded data for {_data.Count} car configurations.");
    }

    // Key combines CarOrdinal + EngineMaxRpm to support engine swaps
    private static string Key(int carOrdinal, float engineMaxRpm) => $"{carOrdinal}_{(int)engineMaxRpm}";
    private static string EvKey(int carOrdinal) => $"ev_{carOrdinal}";

    public float? GetMaxRpm(int carOrdinal, float engineMaxRpm)
        => _data.TryGetValue(Key(carOrdinal, engineMaxRpm), out float rpm) ? rpm : null;

    public void UpdateMaxRpm(int carOrdinal, float engineMaxRpm, float observedRpm)
    {
        string key = Key(carOrdinal, engineMaxRpm);
        if (!_data.TryGetValue(key, out float current) || observedRpm > current + SaveThreshold)
        {
            _data[key] = observedRpm;
            Save();
        }
    }

    public float? GetEvTopSpeed(int carOrdinal)
        => _data.TryGetValue(EvKey(carOrdinal), out float speed) ? speed : null;

    public void UpdateEvTopSpeed(int carOrdinal, float speedKmh)
    {
        string key = EvKey(carOrdinal);
        if (!_data.TryGetValue(key, out float current) || speedKmh > current + SaveThreshold)
        {
            _data[key] = speedKmh;
            Save();
        }
    }

    private Dictionary<string, float> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return new Dictionary<string, float>();
            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<Dictionary<string, float>>(json)
                   ?? new Dictionary<string, float>();
        }
        catch
        {
            Console.WriteLine("[DB] Failed to read car_rpm_data.json — starting fresh.");
            return new Dictionary<string, float>();
        }
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Save error: {ex.Message}");
        }
    }
}
