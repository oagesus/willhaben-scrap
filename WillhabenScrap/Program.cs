using Microsoft.Extensions.Configuration;
using WillhabenScrap.Services;

var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();

var gmailAddress = config["GMAIL_ADDRESS"];
var gmailAppPassword = config["GMAIL_APP_PASSWORD"];
var recipientEmail = config["RECIPIENT_EMAIL"];

if (string.IsNullOrEmpty(gmailAddress) || string.IsNullOrEmpty(gmailAppPassword) || string.IsNullOrEmpty(recipientEmail))
{
    Console.WriteLine("ERROR: Missing configuration. Set GMAIL_ADDRESS, GMAIL_APP_PASSWORD, RECIPIENT_EMAIL.");
    Console.WriteLine("Local: dotnet user-secrets set \"GMAIL_ADDRESS\" \"your@gmail.com\"");
    Console.WriteLine("Docker: Use .env file with compose.yaml");
    return;
}

var emailService = new EmailService(gmailAddress, gmailAppPassword, recipientEmail);
var scrapingService = new ScrapingService();

Log("Willhaben Private Immobilien Scraper started.");
Log($"Monitoring: Wien, Niederösterreich, Burgenland");
Log($"Notifications: {recipientEmail}");

while (true)
{
    try
    {
        var newListings = await scrapingService.CheckForNewPrivateListings();

        if (newListings.Count > 0)
        {
            Log($"Found {newListings.Count} new private listing(s)! Sending email...");
            await emailService.SendNewListingsEmail(newListings);
        }
    }
    catch (Exception ex)
    {
        Log($"Error: {ex.Message}");
    }

    var interval = scrapingService.GetRandomizedInterval();
    Log($"Next check in {interval / 1000}s");
    await Task.Delay(interval);
}

static void Log(string message)
{
    Console.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}");
}