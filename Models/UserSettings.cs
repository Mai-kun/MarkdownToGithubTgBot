namespace MdNoteToGithub.Models;

public class UserSettings
{
    public long TelegramId { get; set; }

    public string GithubToken { get; set; } = string.Empty;

    public string RepoOwner { get; set; } = string.Empty;

    public string RepoName { get; set; } = string.Empty;

    public string LanguageCode { get; set; } = "en";
}