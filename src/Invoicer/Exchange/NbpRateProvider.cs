using System.Globalization;
using System.Net;
using Invoicer.Update;

namespace Invoicer.Exchange;

/// <summary>
/// Looks up the NBP average (table A) rate that applies to a given relevant date.
///
/// Follows <see cref="UpdateChecker"/>'s pattern for an optional network call from a desktop
/// app: short timeout, every failure mapped to a message, nothing thrown at the caller.
/// </summary>
public static class NbpRateProvider
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How far back the lookup window reaches. NBP has no "latest before date" endpoint and its
    /// single-date endpoint returns 404 for any non-publication day, so the rate is found by
    /// requesting a window that ends the day before the relevant date and taking its last entry.
    ///
    /// Fourteen days clears the longest run of consecutive non-publication days in the Polish
    /// calendar with margin, and is far inside NBP's 93-day limit for one query. No holiday
    /// calendar is needed: NBP's own publication gaps already record which days were working days.
    /// </summary>
    internal const int WindowDays = 14;

    /// <summary>
    /// The query window for a relevant date. It ends the day before, because art. 31a calls for
    /// the last working day *preceding* the relevant date - a table published on that date itself
    /// is never the right one.
    /// </summary>
    internal static (DateTime Start, DateTime End) Window(DateTime relevantDate)
    {
        var end = relevantDate.Date.AddDays(-1);
        return (end.AddDays(-(WindowDays - 1)), end);
    }

    internal static string BuildUrl(string currency, DateTime relevantDate)
    {
        var (start, end) = Window(relevantDate);
        var code = ExchangeRateRules.Normalize(currency).ToLowerInvariant();

        return "https://api.nbp.pl/api/exchangerates/rates/a/"
               + $"{code}/{Format(start)}/{Format(end)}/?format=json";

        static string Format(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The rate for <paramref name="currency"/> from the last NBP table published strictly before
    /// <paramref name="relevantDate"/>.
    /// </summary>
    public static async Task<NbpRateResult> GetRateAsync(
        string currency,
        DateTime relevantDate,
        CancellationToken cancellationToken = default,
        TimeProvider? timeProvider = null)
    {
        using var client = CreateClient();
        return await GetRateAsync(client, currency, relevantDate, cancellationToken, timeProvider);
    }

    /// <summary>
    /// The same lookup over a caller-supplied client, which the caller owns and disposes.
    /// Exists so the failure paths can be exercised without a socket.
    /// </summary>
    internal static async Task<NbpRateResult> GetRateAsync(
        HttpClient client,
        string currency,
        DateTime relevantDate,
        CancellationToken cancellationToken = default,
        TimeProvider? timeProvider = null)
    {
        var code = ExchangeRateRules.Normalize(currency);

        if (code.Length != 3)
            return NbpRateResult.Failed($"'{currency}' is not a three-letter currency code.");
        if (!ExchangeRateRules.RequiresRate(code))
            return NbpRateResult.Failed("PLN invoices do not need an exchange rate.");

        // A relevant date in the future has no rate yet, and NBP answers a forward-looking range
        // with 400. Say why rather than relaying a rejection the user cannot act on.
        var today = (timeProvider ?? TimeProvider.System).GetLocalNow().Date;
        if (relevantDate.Date > today)
        {
            return NbpRateResult.Failed(
                $"NBP has not published a rate for {relevantDate:dd.MM.yyyy} yet: that date is in the future. "
                + "Use an earlier rate date, or enter the rate once it is published.");
        }

        try
        {
            using var response = await client.GetAsync(
                BuildUrl(code, relevantDate),
                HttpCompletionOption.ResponseContentRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return NbpRateResult.Failed(DescribeFailure(response.StatusCode, code, relevantDate));

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var rate = NbpRateParser.ParseLast(json);

            return rate is null
                ? NbpRateResult.Failed("NBP returned a response that could not be read.")
                : NbpRateResult.Success(rate);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return NbpRateResult.Failed("The exchange rate lookup timed out.");
        }
        catch (HttpRequestException ex)
        {
            return NbpRateResult.Failed($"Could not reach NBP: {ex.Message}");
        }
        catch (Exception ex)
        {
            return NbpRateResult.Failed(ex.Message);
        }
    }

    internal static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout };
        client.DefaultRequestHeaders.Add("User-Agent", $"Invoicer/{AppVersion.Display}");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        return client;
    }

    internal static string DescribeFailure(HttpStatusCode status, string currency, DateTime relevantDate)
    {
        var (start, end) = Window(relevantDate);

        return status switch
        {
            // The range endpoint answers 404 both for an unknown currency and for a window with
            // no published table, and the body does not distinguish them.
            HttpStatusCode.NotFound =>
                $"NBP published no {currency} rate between {start:dd.MM.yyyy} and {end:dd.MM.yyyy}. "
                + "Check the currency code, or enter the rate by hand.",
            HttpStatusCode.BadRequest => $"NBP rejected the request for {currency}.",
            HttpStatusCode.TooManyRequests => "NBP rate limit reached. Try again shortly.",
            _ => $"NBP returned {(int)status} {status}.",
        };
    }
}
