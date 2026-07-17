using EasyLog.Contracts.Enums;

namespace EasyLog.Contracts.Models;

public sealed record CollectionRequest(
    SessionMode Mode,
    string? SourcePath = null,
    string? DeviceSerial = null,
    string? AdbPath = null,
    IReadOnlyList<LogBufferKind>? Buffers = null,
    string? AdditionalArguments = null,
    DateTimeOffset? LiveStartFrom = null,
    int ReadBufferSize = 4096)
{
    public static CollectionRequest ForFile(string filePath) => new(SessionMode.File, SourcePath: filePath);

    public static CollectionRequest ForAdb(
        string? deviceSerial = null,
        string? adbPath = null,
        IReadOnlyList<LogBufferKind>? buffers = null,
        string? additionalArguments = null,
        DateTimeOffset? liveStartFrom = null) =>
        new(SessionMode.LiveAdb, DeviceSerial: deviceSerial, AdbPath: adbPath, Buffers: buffers, AdditionalArguments: additionalArguments, LiveStartFrom: liveStartFrom);
}

