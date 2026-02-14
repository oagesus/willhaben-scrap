using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using WillhabenScrap.Models;

namespace WillhabenScrap.Services;

public class EmailService : IEmailService
{
    private readonly string _gmailAddress;
    private readonly string _gmailAppPassword;
    private readonly string _recipientEmail;
    private readonly string _template;

    public EmailService(string gmailAddress, string gmailAppPassword, string recipientEmail)
    {
        _gmailAddress = gmailAddress;
        _gmailAppPassword = gmailAppPassword;
        _recipientEmail = recipientEmail;
        _template = LoadTemplate();
    }

    public async Task SendNewListingsEmail(List<Listing> listings)
    {
        if (listings.Count == 0) return;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Willhaben Scraper", _gmailAddress));
        message.To.Add(new MailboxAddress("", _recipientEmail));
        message.Subject = $"{listings.Count} neue private Immobilien auf Willhaben";

        var bodyBuilder = new BodyBuilder();
        bodyBuilder.HtmlBody = BuildHtmlBody(listings);
        message.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_gmailAddress, _gmailAppPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        Log($"Email sent with {listings.Count} new listing(s)");
    }

    private string BuildHtmlBody(List<Listing> listings)
    {
        var viennaZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        var viennaTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, viennaZone);
        var timezoneAbbr = viennaZone.IsDaylightSavingTime(DateTime.UtcNow) ? "CEST" : "CET";

        var listingBlockStart = _template.IndexOf("<!--LISTING_START-->");
        var listingBlockEnd = _template.IndexOf("<!--LISTING_END-->") + "<!--LISTING_END-->".Length;

        var before = _template[..listingBlockStart];
        var listingTemplate = _template[listingBlockStart..listingBlockEnd];
        var after = _template[listingBlockEnd..];

        var renderedListings = listings.Select(l =>
            listingTemplate
                .Replace("{{TITLE}}", l.Title)
                .Replace("{{PRICE}}", l.Price)
                .Replace("{{LOCATION}}", l.Location)
                .Replace("{{URL}}", l.Url)
                .Replace("{{PROPERTY_TYPE}}", string.IsNullOrEmpty(l.PropertyType) ? "-" : l.PropertyType)
                .Replace("{{SIZE}}", string.IsNullOrEmpty(l.Size) || l.Size == "0" ? "-" : l.Size)
                .Replace("{{ROOMS}}", string.IsNullOrEmpty(l.Rooms) || l.Rooms == "0" ? "-" : l.Rooms)
        ).ToList();

        if (renderedListings.Count > 0)
        {
            var last = renderedListings[^1];
            var hrIndex = last.LastIndexOf("<hr");
            if (hrIndex >= 0)
            {
                var hrEnd = last.IndexOf("/>", hrIndex) + 2;
                renderedListings[^1] = last[..hrIndex] + last[hrEnd..];
            }
        }

        var html = before + string.Join("", renderedListings) + after;

        return html
            .Replace("{{SENT_AT}}", viennaTime.ToString("yyyy-MM-dd HH:mm:ss"))
            .Replace("{{TIMEZONE}}", timezoneAbbr)
            .Replace("{{COUNT}}", listings.Count.ToString());
    }

    private static string LoadTemplate()
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "Emails", "WillhabenScrapEmail.html");

        if (File.Exists(templatePath))
        {
            Log($"Template loaded: {templatePath}");
            return File.ReadAllText(templatePath);
        }

        Log("WARNING: Email template not found, using fallback");
        return string.Empty;
    }

    private static void Log(string message)
    {
        Console.WriteLine($"[{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}] [Email] {message}");
    }
}