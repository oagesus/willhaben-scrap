using WillhabenScrap.Models;

namespace WillhabenScrap.Services;

public interface IEmailService
{
    Task SendNewListingsEmail(List<Listing> listings);
}