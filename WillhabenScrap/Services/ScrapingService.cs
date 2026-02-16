using System.Text.Json;
using HtmlAgilityPack;
using WillhabenScrap.Models;

namespace WillhabenScrap.Services;

public class ScrapingService
{
    private const int MaxPagesPerRegion = 5; // Number of pages to scrape per region (30 listings per page)
    private const int IntervalMinMs = 115_000; // Main loop interval min (ms) - time between full scrape cycles
    private const int IntervalMaxMs = 125_000; // Main loop interval max (ms)
    private const int DelayBetweenRegionsMinMs = 2000; // Delay between scraping each region (ms)
    private const int DelayBetweenRegionsMaxMs = 5000;
    private const int DelayBetweenPagesMinMs = 1500; // Delay between scraping each page within a region (ms)
    private const int DelayBetweenPagesMaxMs = 3000;
    private const bool ExcludeRentals = false; // Skip rental listings
    private const bool ExcludeCommercial = true; // Skip commercial property types
    private const bool ExcludeProperties = true; // Skip land/property listings
    private const bool OnlyPrivateListings = true; // Only show private sellers

    private static readonly string[] CommercialPropertyTypes =
    [
        "Geschäfts-/Ladenlokal",
        "Büro/Ordination",
        "Gastronomie",
        "Lagerhalle",
        "Werkstatt"
    ];

    private static readonly string[] PropertyTypes =
    [
        "Grundstück",
        "Baugrundstück",
        "Gewerbegrundstück",
        "Land-/Forstwirtschaft"
    ];

    private static readonly string[] RegionUrls =
    [
        "https://www.willhaben.at/iad/immobilien/immobilien/wien",
        "https://www.willhaben.at/iad/immobilien/immobilien/niederoesterreich",
        "https://www.willhaben.at/iad/immobilien/immobilien/burgenland"
    ];

    private static readonly string[] UserAgents =
    [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:133.0) Gecko/20100101 Firefox/133.0"
    ];

    private readonly HashSet<string> _knownListingIds = new();
    private readonly HttpClient _httpClient;
    private readonly Random _random = new();
    private bool _isFirstRun = true;

    public ScrapingService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "de-AT,de;q=0.9,en;q=0.8");
    }

    public async Task<List<Listing>> CheckForNewPrivateListings()
    {
        var allNewPrivate = new List<Listing>();

        foreach (var regionUrl in RegionUrls)
        {
            try
            {
                var listings = await ScrapeRegion(regionUrl);
                var privateListings = listings.Where(l => l != null).ToList();
                var regionNewCount = 0;

                foreach (var listing in privateListings)
                {
                    if (_knownListingIds.Add(listing.Id))
                    {
                        if (!_isFirstRun)
                        {
                            allNewPrivate.Add(listing);
                            regionNewCount++;
                        }
                    }
                }

                Log($"{regionUrl.Split('/').Last()}: {privateListings.Count} private, {(_isFirstRun ? "indexing" : $"{regionNewCount} new")}");

                await Task.Delay(_random.Next(DelayBetweenRegionsMinMs, DelayBetweenRegionsMaxMs));
            }
            catch (Exception ex)
            {
                Log($"Error scraping {regionUrl}: {ex.Message}");
            }
        }

        if (_isFirstRun)
        {
            Log($"First run complete. Indexed {_knownListingIds.Count} private listings.");
            _isFirstRun = false;
        }

        return allNewPrivate;
    }

    private async Task<List<Listing>> ScrapeRegion(string url)
    {
        var allListings = new List<Listing>();

        for (int page = 1; page <= MaxPagesPerRegion; page++)
        {
            var pageUrl = page == 1 ? url : $"{url}?page={page}";

            try
            {
                var listings = await ScrapePage(pageUrl);
                if (listings.Count == 0) break;

                allListings.AddRange(listings);

                if (listings.Count < 25) break;

                await Task.Delay(_random.Next(DelayBetweenPagesMinMs, DelayBetweenPagesMaxMs));
            }
            catch (Exception ex)
            {
                Log($"Error on page {page}: {ex.Message}");
                break;
            }
        }

        return allListings;
    }

    private async Task<List<Listing>> ScrapePage(string url)
    {
        _httpClient.DefaultRequestHeaders.Remove("User-Agent");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgents[_random.Next(UserAgents.Length)]);

        var html = await _httpClient.GetStringAsync(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var scriptNode = doc.DocumentNode.SelectSingleNode("//script[@id='__NEXT_DATA__']");
        if (scriptNode == null)
        {
            Log($"No __NEXT_DATA__ found on {url}");
            return [];
        }

        var json = scriptNode.InnerText;
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var advertSummary = root
            .GetProperty("props")
            .GetProperty("pageProps")
            .GetProperty("searchResult")
            .GetProperty("advertSummaryList")
            .GetProperty("advertSummary");

        var listings = new List<Listing>();

        foreach (var ad in advertSummary.EnumerateArray())
        {
            if (OnlyPrivateListings && !IsPrivateListing(ad)) continue;
            if (ExcludeRentals && IsRentalListing(ad)) continue;
            if (ExcludeCommercial && IsCommercialListing(ad)) continue;
            if (ExcludeProperties && IsPropertyListing(ad)) continue;

            var listing = ParseListing(ad);
            if (listing != null)
                listings.Add(listing);
        }

        return listings;
    }

    private static bool IsPrivateListing(JsonElement ad)
    {
        var attrValue = GetAttributeValue(ad, "ISPRIVATE");
        return attrValue == "1";
    }

    private static bool IsRentalListing(JsonElement ad)
    {
        var ownageType = GetAttributeValue(ad, "OWNAGETYPE");
        return ownageType == "Miete";
    }

    private static bool IsCommercialListing(JsonElement ad)
    {
        var propertyType = GetAttributeValue(ad, "PROPERTY_TYPE");
        return propertyType != null && CommercialPropertyTypes.Contains(propertyType);
    }

    private static bool IsPropertyListing(JsonElement ad)
    {
        var propertyType = GetAttributeValue(ad, "PROPERTY_TYPE");
        return propertyType != null && PropertyTypes.Contains(propertyType);
    }

    private static Listing? ParseListing(JsonElement ad)
    {
        var id = GetAttributeValue(ad, "ADID");
        if (string.IsNullOrEmpty(id)) return null;

        var seoUrl = GetAttributeValue(ad, "SEO_URL") ?? "";
        var price = GetAttributeValue(ad, "PRICE_FOR_DISPLAY")
                    ?? FormatPrice(GetAttributeValue(ad, "ESTATE_PRICE/PRICE_SUGGESTION"));

        var location = GetAttributeValue(ad, "LOCATION") ?? "";
        var address = GetAttributeValue(ad, "ADDRESS") ?? "";
        var postcode = GetAttributeValue(ad, "POSTCODE") ?? "";
        var fullLocation = !string.IsNullOrEmpty(address)
            ? $"{address}, {postcode} {location}".Trim()
            : location;

        return new Listing
        {
            Id = id,
            Title = GetAttributeValue(ad, "HEADING") ?? "",
            Price = price ?? "Preis auf Anfrage",
            Location = fullLocation,
            Url = string.IsNullOrEmpty(seoUrl) ? "" : $"https://www.willhaben.at/iad/{seoUrl}",
            PropertyType = GetAttributeValue(ad, "PROPERTY_TYPE") ?? "",
            Size = GetAttributeValue(ad, "ESTATE_SIZE/LIVING_AREA")
                   ?? GetAttributeValue(ad, "ESTATE_SIZE/USEABLE_AREA")
                   ?? GetAttributeValue(ad, "ESTATE_SIZE")
                   ?? "",
            Rooms = GetAttributeValue(ad, "NUMBER_OF_ROOMS") ?? ""
        };
    }

    private static string? GetAttributeValue(JsonElement ad, string name)
    {
        if (!ad.TryGetProperty("attributes", out var attributes)) return null;
        if (!attributes.TryGetProperty("attribute", out var attrArray)) return null;

        foreach (var attr in attrArray.EnumerateArray())
        {
            if (attr.GetProperty("name").GetString() == name)
            {
                var values = attr.GetProperty("values");
                if (values.GetArrayLength() > 0)
                    return values[0].GetString();
            }
        }

        return null;
    }

    private static string? FormatPrice(string? priceStr)
    {
        if (string.IsNullOrEmpty(priceStr)) return null;
        if (double.TryParse(priceStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var price))
            return $"€ {price:N2}";
        return priceStr;
    }

    public int GetRandomizedInterval()
    {
        return _random.Next(IntervalMinMs, IntervalMaxMs);
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}] [Scraper] {message}");
    }
}