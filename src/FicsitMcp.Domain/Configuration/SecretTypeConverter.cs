using System.ComponentModel;
using System.Globalization;

namespace FicsitMcp.Domain.Configuration;

/// <summary>
/// Lets the configuration binder (which uses <see cref="TypeConverter"/>, not JSON converters)
/// turn a raw config/env string into a <see cref="Secret"/>. Converting back to string yields the
/// redacted form so a secret cannot leak through any TypeConverter-based serialization either.
/// </summary>
public sealed class SecretTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string s ? new Secret(s) : base.ConvertFrom(context, culture, value)!;

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        destinationType == typeof(string) && value is Secret secret
            ? secret.ToString()
            : base.ConvertTo(context, culture, value, destinationType);
}
