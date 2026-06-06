using System.ComponentModel;
using System.Text.Json.Serialization;

namespace FicsitMcp.Domain.Configuration;

/// <summary>
/// A credential value (admin token, password) that must never be logged or echoed.
/// Wrapping the raw string in this type makes leakage opt-out by default: <see cref="ToString"/>
/// redacts, and the type is excluded from JSON serialization, so logging an options object
/// or returning it in a tool result emits "***" rather than the secret. Callers extract the
/// real value only through the explicit <see cref="Reveal"/> method, which is greppable and
/// stands out in review.
/// </summary>
/// <remarks>
/// Configuration binding sees this as a string via <see cref="SecretJsonConverter"/>, so a
/// secret can be supplied directly in appsettings/env without the property being typed as a
/// raw <see cref="string"/> that would otherwise serialize in the clear.
/// </remarks>
[JsonConverter(typeof(SecretJsonConverter))]
[TypeConverter(typeof(SecretTypeConverter))]
public readonly struct Secret : IEquatable<Secret>
{
    private readonly string? _value;

    /// <summary>Wraps a raw credential value. A null or empty value means "no secret set".</summary>
    public Secret(string? value) => _value = string.IsNullOrEmpty(value) ? null : value;

    /// <summary>True when a non-empty secret value is present.</summary>
    public bool HasValue => _value is not null;

    /// <summary>
    /// Returns the raw secret value, or <c>null</c> if none was set. The deliberately blunt
    /// name keeps every credential read visible in diffs and audits.
    /// </summary>
    public string? Reveal() => _value;

    /// <summary>Redacted form — never the real value — so logs and tool results stay clean.</summary>
    public override string ToString() => _value is null ? "(unset)" : "***";

    /// <inheritdoc />
    public bool Equals(Secret other) => string.Equals(_value, other._value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Secret other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value is null ? 0 : StringComparer.Ordinal.GetHashCode(_value);

    /// <summary>Implicitly wraps a raw string so config binding and call sites read naturally.</summary>
    public static implicit operator Secret(string? value) => new(value);
}
