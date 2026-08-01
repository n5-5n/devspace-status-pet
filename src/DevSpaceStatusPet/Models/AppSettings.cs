namespace DevSpaceStatusPet.Models;

public enum PetTheme
{
    Classic,
    Neon
}

public enum UiLanguagePreference
{
    Auto,
    Japanese,
    English
}

public sealed class AppSettings
{
    public string Theme { get; set; } = nameof(PetTheme.Classic);
    public bool ShowBubble { get; set; } = true;
    public string Language { get; set; } = nameof(UiLanguagePreference.Auto);
    public double Scale { get; set; } = 1.15;
    public double Opacity { get; set; } = 1.0;
    public int CompletionQuietSeconds { get; set; } = 45;
    public int StallMinutes { get; set; } = 30;
    public int MaxBubbles { get; set; } = 4;
    public bool NotificationsEnabled { get; set; } = true;

    public PetTheme ResolvedTheme =>
        Enum.TryParse<PetTheme>(Theme, true, out var value) ? value : PetTheme.Classic;

    public UiLanguagePreference LanguagePreference =>
        Enum.TryParse<UiLanguagePreference>(Language, true, out var value)
            ? value
            : UiLanguagePreference.Auto;

    public AppSettings Clone() => new()
    {
        Theme = Theme,
        ShowBubble = ShowBubble,
        Language = Language,
        Scale = Scale,
        Opacity = Opacity,
        CompletionQuietSeconds = CompletionQuietSeconds,
        StallMinutes = StallMinutes,
        MaxBubbles = MaxBubbles,
        NotificationsEnabled = NotificationsEnabled
    };

    public void Normalize()
    {
        Theme = Enum.TryParse<PetTheme>(Theme, true, out var theme)
            ? theme.ToString()
            : nameof(PetTheme.Classic);
        Language = Enum.TryParse<UiLanguagePreference>(Language, true, out var language)
            ? language.ToString()
            : nameof(UiLanguagePreference.Auto);
        Scale = Math.Clamp(Scale, 0.6, 2.5);
        Opacity = Math.Clamp(Opacity, 0.5, 1.0);
        CompletionQuietSeconds = Math.Clamp(CompletionQuietSeconds, 10, 300);
        StallMinutes = Math.Clamp(StallMinutes, 1, 240);
        MaxBubbles = Math.Clamp(MaxBubbles, 1, 8);
    }
}
