namespace EasyLog.Contracts.Models;

public sealed record DeviceInfo(
    string Serial,
    string State,
    string? Product = null,
    string? Model = null,
    string? Device = null)
{
    public override string ToString() => string.IsNullOrWhiteSpace(Model) ? $"{Serial} ({State})" : $"{Model} [{Serial}] ({State})";
}

