# willhaben-scrap

Monitors [willhaben.at](https://www.willhaben.at) for new private real estate listings in Vienna, Lower Austria, and Burgenland — and sends you an email when new ones appear.

## Features

- Filters for private sellers only (no agencies/dealers)
- Configurable filters for rentals, commercial, and property listings
- Email notifications via Gmail with property details and direct links
- Smart change detection — only notifies on new listings
- Randomized request intervals and user-agent rotation

## Tech Stack

| Component    | Technology                                                          |
|--------------|---------------------------------------------------------------------|
| Language     | C# / .NET 10                                                       |
| Web scraping | [HtmlAgilityPack](https://html-agility-pack.net/) 1.12.4           |
| Email        | [MailKit](https://github.com/jstedfast/MailKit) 4.14.1              |
| Hosting      | Docker                                                              |

## Prerequisites

- [Docker](https://docs.docker.com/get-started/get-docker/) **or** [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- A Gmail account with an App Password (see below)

## Gmail App Password

The scraper needs an **App Password** to send emails through your Gmail account. This is a separate password generated specifically for this purpose — your regular Gmail password is never used.

1. Go to [myaccount.google.com](https://myaccount.google.com/) and sign in.
2. Navigate to **Security** > **2-Step Verification** and make sure it is turned on. If it's not, follow the prompts to enable it first.
3. Once 2-Step Verification is enabled, go to [App Passwords](https://myaccount.google.com/apppasswords).
4. Enter a name (e.g. `willhaben-scrap`) and click **Create**.
5. Google will show you a 16-character password — copy it and use it as the `GMAIL_APP_PASSWORD` value.

> **Note:** You will only see the password once. If you lose it, you can delete the old one and create a new one.

## Setup

### Option 1: Docker (Recommended)

1. Download the [`compose.yaml`](compose.yaml) file.

2. Create a `.env` file in the same directory:

   ```env
   GMAIL_ADDRESS=your-email@gmail.com
   GMAIL_APP_PASSWORD=your-app-password
   RECIPIENT_EMAIL=recipient@example.com
   ```

   | Variable             | Description                                                                 |
   |----------------------|-----------------------------------------------------------------------------|
   | `GMAIL_ADDRESS`      | The Gmail address used to **send** the notification emails                  |
   | `GMAIL_APP_PASSWORD`  | The [App Password](https://support.google.com/accounts/answer/185833) for that Gmail account (not your regular password) |
   | `RECIPIENT_EMAIL`    | The email address that **receives** the notifications (can be any email)    |

3. Open a terminal in the folder where you saved both files and start the container:

   ```bash
   docker compose up -d
   ```

4. To check that it's running, run this in the same folder:

   ```bash
   docker compose logs -f
   ```

### Option 2: .NET SDK

1. Clone the repository:

   ```bash
   git clone https://github.com/oagesus/willhaben-scrap.git
   cd willhaben-scrap
   ```

2. Configure secrets using the .NET User Secrets manager:

   ```bash
   cd WillhabenScrap
   dotnet user-secrets set "GMAIL_ADDRESS" "your-email@gmail.com"
   dotnet user-secrets set "GMAIL_APP_PASSWORD" "your-app-password"
   dotnet user-secrets set "RECIPIENT_EMAIL" "recipient@example.com"
   ```

3. Run the application:

   ```bash
   dotnet run
   ```

## How It Works

1. On first run, indexes existing listings to establish a baseline (no emails sent).
2. Every ~2 minutes, scrapes up to 5 pages per region for new listings.
3. New listings trigger an email with property details (price, size, rooms, location, type).

## Configuration

Filtering is controlled by constants in `ScrapingService.cs`:

| Filter               | Default | Description                            |
|----------------------|---------|----------------------------------------|
| `OnlyPrivateListings`| `true`  | Show only private seller listings      |
| `ExcludeRentals`     | `true`  | Exclude rental/lease listings          |
| `ExcludeCommercial`  | `true`  | Exclude commercial property listings   |
| `ExcludeProperties`  | `false` | Exclude land/property listings         |

## License

This project is for personal/educational use.
