using System.Globalization;
using System.Net;
using System.Text;
using Invoicer.Exchange;
using Xunit;

namespace Invoicer.Tests;

/// <summary>
/// Serves recorded NBP payloads, or a chosen failure, without opening a socket. Every test in
/// this class goes through it, so the suite never depends on api.nbp.pl being reachable.
/// </summary>
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public Uri? LastRequestUri { get; private set; }

    public static StubHandler Returning(HttpStatusCode status, string body = "") =>
        new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

    public static StubHandler Throwing(Exception exception) => new(_ => throw exception);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        return Task.FromResult(_respond(request));
    }
}

public class NbpRateProviderTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", name));

    // Fixed so the recorded September 2026 windows are never "in the future".
    private static readonly TimeProvider Clock =
        new FixedClock(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));

    private sealed class FixedClock(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static async Task<NbpRateResult> Lookup(StubHandler handler, string currency, DateTime relevantDate)
    {
        using var client = new HttpClient(handler);
        return await NbpRateProvider.GetRateAsync(client, currency, relevantDate, default, Clock);
    }

    [Fact]
    public async Task FutureRateDate_IsExplainedWithoutARequest()
    {
        var handler = StubHandler.Returning(HttpStatusCode.OK, Fixture("nbp-usd-window-20260920.json"));

        var result = await Lookup(handler, "USD", new DateTime(2026, 10, 16));

        Assert.Null(result.Rate);
        Assert.Contains("has not published a rate for 16.10.2026 yet", result.Error);
        Assert.Null(handler.LastRequestUri);
    }

    [Fact]
    public async Task RateDateOfToday_IsAllowed()
    {
        var handler = StubHandler.Returning(HttpStatusCode.OK, Fixture("nbp-usd-window-20260920.json"));

        var result = await Lookup(handler, "USD", new DateTime(2026, 10, 15));

        Assert.NotNull(result.Rate);
    }

    // Task 2.1 / spec scenario: "Rate date falls on a Sunday".
    [Fact]
    public async Task RateDateOnASunday_ResolvesToThePrecedingFridaysTable()
    {
        var handler = StubHandler.Returning(HttpStatusCode.OK, Fixture("nbp-usd-window-20260920.json"));

        var result = await Lookup(handler, "USD", new DateTime(2026, 9, 20));

        Assert.Null(result.Error);
        Assert.Equal(new NbpRate(3.7998m, new DateTime(2026, 9, 18), "182/A/NBP/2026"), result.Rate);
    }

    [Fact]
    public async Task RateDateOnASunday_RequestsTheFourteenDayWindowEndingTheDayBefore()
    {
        var handler = StubHandler.Returning(HttpStatusCode.OK, Fixture("nbp-usd-window-20260920.json"));

        await Lookup(handler, "USD", new DateTime(2026, 9, 20));

        Assert.Equal(
            "https://api.nbp.pl/api/exchangerates/rates/a/usd/2026-09-06/2026-09-19/?format=json",
            handler.LastRequestUri?.ToString());
    }

    // Task 2.2 / spec scenario: "Rate date falls on a working day with its own published table".
    // 17.09.2026 published table 181/A at 3.8030; the strictly-before rule must skip it.
    [Fact]
    public async Task RateDateOnAWorkingDay_SkipsThatDaysOwnTable()
    {
        var handler = StubHandler.Returning(HttpStatusCode.OK, Fixture("nbp-usd-window-20260917.json"));

        var result = await Lookup(handler, "USD", new DateTime(2026, 9, 17));

        Assert.Equal(new NbpRate(3.7639m, new DateTime(2026, 9, 16), "180/A/NBP/2026"), result.Rate);
        Assert.NotEqual(3.8030m, result.Rate!.Rate);
    }

    [Fact]
    public void Window_EndsTheDayBeforeTheRelevantDateAndSpansFourteenDays()
    {
        var (start, end) = NbpRateProvider.Window(new DateTime(2026, 9, 20));

        Assert.Equal(new DateTime(2026, 9, 19), end);
        Assert.Equal(new DateTime(2026, 9, 6), start);
        Assert.Equal(14, (end - start).Days + 1);
    }

    // Task 2.3: every failure becomes a message, never an exception reaching the caller.
    [Fact]
    public async Task UnreachableHost_ReportsAMessage()
    {
        var handler = StubHandler.Throwing(new HttpRequestException("No such host is known."));

        var result = await Lookup(handler, "USD", new DateTime(2026, 9, 20));

        Assert.Null(result.Rate);
        Assert.Equal("Could not reach NBP: No such host is known.", result.Error);
    }

    [Fact]
    public async Task Timeout_ReportsAMessage()
    {
        var handler = StubHandler.Throwing(new TaskCanceledException("timed out"));

        var result = await Lookup(handler, "USD", new DateTime(2026, 9, 20));

        Assert.Null(result.Rate);
        Assert.Equal("The exchange rate lookup timed out.", result.Error);
    }

    [Fact]
    public async Task NotFound_ExplainsTheWindowThatWasSearched()
    {
        var handler = StubHandler.Returning(HttpStatusCode.NotFound, "404 NotFound - Not Found");

        var result = await Lookup(handler, "USD", new DateTime(2026, 9, 20));

        Assert.Null(result.Rate);
        Assert.Equal(
            "NBP published no USD rate between 06.09.2026 and 19.09.2026. "
            + "Check the currency code, or enter the rate by hand.",
            result.Error);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "NBP rejected the request for USD.")]
    [InlineData(HttpStatusCode.TooManyRequests, "NBP rate limit reached. Try again shortly.")]
    [InlineData(HttpStatusCode.InternalServerError, "NBP returned 500 InternalServerError.")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "NBP returned 503 ServiceUnavailable.")]
    public async Task UnsuccessfulStatus_ReportsAMessage(HttpStatusCode status, string expected)
    {
        var handler = StubHandler.Returning(status);

        var result = await Lookup(handler, "USD", new DateTime(2026, 9, 20));

        Assert.Null(result.Rate);
        Assert.Equal(expected, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"table\":\"A\"}")]
    [InlineData("{\"table\":\"A\",\"rates\":[]}")]
    [InlineData("{\"table\":\"A\",\"rates\":[{\"no\":\"1/A/NBP/2026\"}]}")]
    [InlineData("{\"table\":\"A\",\"rates\":[{\"no\":\"1/A/NBP/2026\",\"effectiveDate\":\"2026-09-18\"}]}")]
    [InlineData("{\"table\":\"A\",\"rates\":[{\"no\":\"1/A/NBP/2026\",\"effectiveDate\":\"18.09.2026\",\"mid\":3.5}]}")]
    [InlineData("{\"table\":\"A\",\"rates\":[{\"no\":\"1/A/NBP/2026\",\"effectiveDate\":\"2026-09-18\",\"mid\":0}]}")]
    public async Task UnreadableBody_ReportsAMessage(string body)
    {
        var handler = StubHandler.Returning(HttpStatusCode.OK, body);

        var result = await Lookup(handler, "USD", new DateTime(2026, 9, 20));

        Assert.Null(result.Rate);
        Assert.Equal("NBP returned a response that could not be read.", result.Error);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("DOLLAR")]
    [InlineData("")]
    public async Task MalformedCurrencyCode_IsRejectedWithoutARequest(string currency)
    {
        var handler = StubHandler.Returning(HttpStatusCode.OK, Fixture("nbp-usd-window-20260920.json"));

        var result = await Lookup(handler, currency, new DateTime(2026, 9, 20));

        Assert.Null(result.Rate);
        Assert.Contains("three-letter currency code", result.Error);
        Assert.Null(handler.LastRequestUri);
    }

    [Fact]
    public async Task Pln_IsRejectedWithoutARequest()
    {
        var handler = StubHandler.Returning(HttpStatusCode.OK, Fixture("nbp-usd-window-20260920.json"));

        var result = await Lookup(handler, "PLN", new DateTime(2026, 9, 20));

        Assert.Null(result.Rate);
        Assert.Equal("PLN invoices do not need an exchange rate.", result.Error);
        Assert.Null(handler.LastRequestUri);
    }

    // Task 2.4: published precision survives parsing, up to and beyond what FA(3) can hold.
    [Theory]
    [InlineData("nbp-usd-window-20260920.json", "3.7998")]
    [InlineData("nbp-huf-6dp.json", "0.012007")]
    [InlineData("nbp-idr-8dp.json", "0.00021425")]
    public void PublishedPrecision_SurvivesParsing(string fixture, string expected)
    {
        var rate = NbpRateParser.ParseLast(Fixture(fixture));

        Assert.NotNull(rate);
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), rate!.Rate);
        // decimal equality ignores trailing zeros, so compare the round-tripped text too:
        // a rate that had passed through a double would not print back identically.
        Assert.Equal(expected, rate.Rate.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ParseLast_TakesTheMostRecentEntryNotTheFirst()
    {
        var rate = NbpRateParser.ParseLast(Fixture("nbp-usd-window-20260920.json"));

        Assert.Equal(new DateTime(2026, 9, 18), rate!.EffectiveDate);
        Assert.Equal("182/A/NBP/2026", rate.TableNumber);
    }
}
