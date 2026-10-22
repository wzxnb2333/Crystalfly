using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Crystalfly.Core.Networking;

namespace Crystalfly.Core.Diagnostics;

public sealed record ErrorFrame(string Module, string Type, string Method);
public sealed record ErrorDetail(string Type, int Code, IReadOnlyList<ErrorFrame> Frames);

/// <summary>A closed data model: no exception text, paths, arbitrary context, or user identifiers.</summary>
public sealed record ErrorReport(
    Guid Id, DateTimeOffset Timestamp, string Release, string OperatingSystem, string Architecture,
    ErrorOperation Operation, bool Fatal, IReadOnlyList<ErrorDetail> Errors, string Fingerprint)
{
    public const int MaxBytes = 64 * 1024;
    public const int MaxReports = 20;
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter<ErrorOperation>() }
    };

    public static bool ShouldIgnore(Exception exception) => exception is OperationCanceledException
        or ArgumentException or FormatException or OfflineModeException or OfflineTransitionException
        || exception is AggregateException aggregate
            && aggregate.InnerExceptions.All(ShouldIgnore);

    public static ErrorReport Create(Exception exception, ErrorOperation operation, bool fatal,
        string release, DateTimeOffset now)
    {
        var errors = new List<ErrorDetail>();
        for (Exception? current = exception; current is not null && errors.Count < 3; current = current.InnerException)
        {
            var frames = new StackTrace(current, fNeedFileInfo: false).GetFrames()
                .Take(16).Select(frame => frame.GetMethod()).OfType<MethodBase>()
                .Select(method => new ErrorFrame(Identifier(method.Module.Assembly.GetName().Name),
                    Identifier(method.DeclaringType?.FullName), Identifier(method.Name))).ToArray();
            errors.Add(new ErrorDetail(Identifier(current.GetType().FullName), current.HResult, frames));
        }
        var signature = string.Join('|', errors.Select(error =>
            $"{error.Type}:{error.Code}:{string.Join(';', error.Frames.Select(frame => $"{frame.Module}.{frame.Type}.{frame.Method}"))}"));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{operation}|{signature}")));
        return new ErrorReport(Guid.NewGuid(), now, Identifier(release),
            Environment.OSVersion.Version.ToString(), RuntimeInformation.ProcessArchitecture.ToString(),
            operation, fatal, errors, fingerprint);
    }

    public static string Identifier(string? value) =>
        !string.IsNullOrEmpty(value) && value.Length <= 160
            && value.All(character => char.IsAsciiLetterOrDigit(character) || "._+-<>`[],".Contains(character))
            ? value : "Unknown";

    public bool IsValid() => Id != Guid.Empty && Enum.IsDefined(Operation)
        && Identifier(Release) == Release && Identifier(OperatingSystem) == OperatingSystem
        && Identifier(Architecture) == Architecture
        && Fingerprint is { Length: 64 } && Fingerprint.All(char.IsAsciiHexDigit)
        && Errors is { Count: > 0 and <= 3 } && Errors.All(error => error is not null
            && Identifier(error.Type) == error.Type && error.Frames is { Count: <= 16 }
            && error.Frames.All(frame => frame is not null && Identifier(frame.Module) == frame.Module
                && Identifier(frame.Type) == frame.Type && Identifier(frame.Method) == frame.Method));
}
