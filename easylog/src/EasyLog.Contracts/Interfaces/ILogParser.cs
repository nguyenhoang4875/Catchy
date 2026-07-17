using EasyLog.Contracts.Models;

namespace EasyLog.Contracts.Interfaces;

public interface ILogParser
{
    bool TryParse(string rawLine, long rowId, out LogRecord record);
}

