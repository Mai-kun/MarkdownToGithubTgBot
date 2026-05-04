using System.ComponentModel.DataAnnotations;

namespace MdNoteToGithub.Models;

public class UserSettings
{
    public bool NeedDownloadImages = false;
    public long TelegramId { get; set; }

    [MaxLength(256)]
    public string GithubToken { get; set; } = string.Empty;

    [MaxLength(256)]
    public string RepoOwner { get; set; } = string.Empty;

    [MaxLength(256)]
    public string RepoName { get; set; } = string.Empty;

    [MaxLength(4)]
    public string LanguageCode { get; set; } = "ru";
}