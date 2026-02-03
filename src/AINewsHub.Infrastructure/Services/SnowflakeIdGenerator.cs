using AINewsHub.Core.Interfaces;

namespace AINewsHub.Infrastructure.Services;

/// <summary>
/// Twitter Snowflake ID generator
/// 64-bit: 1(sign) + 41(timestamp) + 10(machine) + 12(sequence)
/// </summary>
public class SnowflakeIdGenerator : ISnowflakeIdGenerator
{
    private const long Epoch = 1704067200000L; // 2024-01-01 00:00:00 UTC
    private const int TimestampBits = 41;
    private const int MachineBits = 10;
    private const int SequenceBits = 12;

    private const long MaxMachineId = (1L << MachineBits) - 1;
    private const long MaxSequence = (1L << SequenceBits) - 1;

    private readonly long _machineId;
    private long _sequence = 0;
    private long _lastTimestamp = -1;
    private readonly object _lock = new();

    public SnowflakeIdGenerator(long machineId = 1)
    {
        if (machineId < 0 || machineId > MaxMachineId)
            throw new ArgumentException($"Machine ID must be between 0 and {MaxMachineId}");
        _machineId = machineId;
    }

    public long NextId()
    {
        lock (_lock)
        {
            var timestamp = GetTimestamp();

            if (timestamp < _lastTimestamp)
                throw new InvalidOperationException("Clock moved backwards. Refusing to generate id.");

            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & MaxSequence;
                if (_sequence == 0)
                    timestamp = WaitNextMillis(_lastTimestamp);
            }
            else
            {
                _sequence = 0;
            }

            _lastTimestamp = timestamp;

            return ((timestamp - Epoch) << (MachineBits + SequenceBits))
                 | (_machineId << SequenceBits)
                 | _sequence;
        }
    }

    public DateTime ExtractTimestamp(long snowflakeId)
    {
        var timestamp = (snowflakeId >> (MachineBits + SequenceBits)) + Epoch;
        return DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime;
    }

    private static long GetTimestamp() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static long WaitNextMillis(long lastTimestamp)
    {
        var timestamp = GetTimestamp();
        while (timestamp <= lastTimestamp)
            timestamp = GetTimestamp();
        return timestamp;
    }
}
