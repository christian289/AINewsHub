namespace AINewsHub.Core.Interfaces;

public interface ISnowflakeIdGenerator
{
    long NextId();
    DateTime ExtractTimestamp(long snowflakeId);
}
