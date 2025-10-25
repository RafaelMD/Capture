using System.Collections.Generic;

namespace CaptureApp.Models;

public static class WhisperLanguageCatalog
{
    public static IReadOnlyList<LanguageOption> Languages { get; } = new List<LanguageOption>
    {
        new("pt", "Portuguese"),
        new("en", "English")
    };
}
