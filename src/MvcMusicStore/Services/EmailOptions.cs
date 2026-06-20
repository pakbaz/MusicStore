namespace MvcMusicStore.Services;

public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// Address order confirmation emails are sent from.
    /// </summary>
    public string FromAddress { get; set; } = "no-reply@musicstore.local";

    /// <summary>
    /// Friendly display name paired with <see cref="FromAddress"/>.
    /// </summary>
    public string FromName { get; set; } = "MVC Music Store";
}
