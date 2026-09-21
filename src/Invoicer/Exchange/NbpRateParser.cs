using System.Globalization;
using System.Text.Json;

namespace Invoicer.Exchange;

/// <summary>
/// Reads an NBP "exchangerates/rates/a" payload and returns the most recent quotation in it.
///
/// Uses JsonDocument rather than JsonSerializer.Deserialize&lt;T&gt; for the same reason
/// <see cref="Update.ReleaseParser"/> does: the app ships with PublishTrimmed=true, which breaks
/// reflection-based deserialization in ways that only surface in the published binary.
///
/// The rate is read as a decimal straight from the JSON number. It must never pass through a
/// double: NBP publishes up to eight decimal places, and a rate that is silently imprecise is
/// exactly the defect this feature exists to prevent.
/// </summary>
public static class NbpRateParser
{
    /// <summary>
    /// The last entry of the payload's "rates" array, which for a date-range query is the most
    /// recent publication in that range. Returns null when the payload is empty or malformed.
    /// </summary>
    public static NbpRate? ParseLast(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;
            if (!root.TryGetProperty("rates", out var rates) || rates.ValueKind != JsonValueKind.Array)
                return null;

            NbpRate? last = null;
            foreach (var entry in rates.EnumerateArray())
            {
                var parsed = ParseEntry(entry);
                if (parsed is not null)
                    last = parsed;
            }

            return last;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static NbpRate? ParseEntry(JsonElement entry)
    {
        if (entry.ValueKind != JsonValueKind.Object)
            return null;

        if (!entry.TryGetProperty("mid", out var mid) || mid.ValueKind != JsonValueKind.Number)
            return null;
        if (!mid.TryGetDecimal(out var rate) || rate <= 0)
            return null;

        if (!entry.TryGetProperty("effectiveDate", out var date) || date.ValueKind != JsonValueKind.String)
            return null;
        if (!DateTime.TryParseExact(date.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var effectiveDate))
            return null;

        if (!entry.TryGetProperty("no", out var number) || number.ValueKind != JsonValueKind.String)
            return null;

        var tableNumber = number.GetString();
        return string.IsNullOrWhiteSpace(tableNumber)
            ? null
            : new NbpRate(rate, effectiveDate, tableNumber);
    }
}
