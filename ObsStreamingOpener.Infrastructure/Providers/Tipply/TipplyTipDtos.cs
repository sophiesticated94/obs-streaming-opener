using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObsStreamingOpener.Infrastructure.Providers.Tipply;

public sealed record TipplyTipsEnvelope(
    IReadOnlyList<TipplyTipDto>? Data,
    IReadOnlyList<TipplyTipDto>? Items,
    IReadOnlyList<TipplyTipDto>? Tips,
    IReadOnlyList<TipplyTipDto>? Records)
{
    public IReadOnlyList<TipplyTipDto> AllItems => Data ?? Items ?? Tips ?? Records ?? [];
}

public sealed record TipplyTipDto(
    string? Id,
    string? TipId,
    string? TransactionId,
    string? Nick,
    string? Username,
    string? DisplayName,
    string? Name,
    string? Message,
    string? Content,
    string? Comment,
    [property: JsonConverter(typeof(FlexibleDecimalJsonConverter))]
    decimal? Amount,
    [property: JsonConverter(typeof(FlexibleDecimalJsonConverter))]
    decimal? Value,
    string? Currency,
    string? CurrencyCode,
    string? Status,
    string? PaymentMethod,
    [property: JsonConverter(typeof(FlexibleDateTimeOffsetJsonConverter))]
    DateTimeOffset? CreatedAt,
    [property: JsonConverter(typeof(FlexibleDateTimeOffsetJsonConverter))]
    DateTimeOffset? Date,
    [property: JsonConverter(typeof(FlexibleDateTimeOffsetJsonConverter))]
    DateTimeOffset? PaidAt);

internal sealed class FlexibleDecimalJsonConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetDecimal(out var number))
        {
            return number;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value
                .Replace("PLN", "", StringComparison.OrdinalIgnoreCase)
                .Replace("EUR", "", StringComparison.OrdinalIgnoreCase)
                .Replace("USD", "", StringComparison.OrdinalIgnoreCase)
                .Replace("zł", "", StringComparison.OrdinalIgnoreCase)
                .Trim()
                .Replace(" ", "")
                .Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
        }

        using var document = JsonDocument.ParseValue(ref reader);
        return null;
    }

    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

internal sealed class FlexibleDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : null;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var unix))
        {
            return unix > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(unix)
                : DateTimeOffset.FromUnixTimeSeconds(unix);
        }

        using var document = JsonDocument.ParseValue(ref reader);
        return null;
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
