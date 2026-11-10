using System.Globalization;
using System.Text;

namespace Service.Services.VoiceSearch;

/// <summary>
/// Makes spoken or typed words comparable: lower case, Azerbaijani and Russian letters folded to a common form (ə is e, ı is i, ş is s,
/// й is и, ё is е...), marks and punctuation removed. A recogniser does not always write the diacritics, so both sides are folded.
/// </summary>
internal static class VoiceText
{
    public static string Fold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var raw in text.Normalize(NormalizationForm.FormC))
        {
            var c = raw switch
            {
                'İ' or 'I' or 'ı' => 'i',
                'Ə' or 'ə' => 'e',
                'Ö' or 'ö' => 'o',
                'Ü' or 'ü' => 'u',
                'Ç' or 'ç' => 'c',
                'Ş' or 'ş' => 's',
                'Ğ' or 'ğ' => 'g',
                'Ё' or 'ё' => 'е',
                'Й' or 'й' => 'и',
                _ => char.ToLowerInvariant(raw)
            };

            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark || c is '\'' or '’' or 'ʼ' or '`')
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return builder.ToString();
    }

    /// <summary>The words of the text, folded, with the original spelling kept next to each (a typed airport code is recognised by its capitals).</summary>
    public static List<(string Folded, string Original)> Words(string? text)
    {
        var result = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }

        var current = new StringBuilder();
        void Flush()
        {
            if (current.Length == 0)
            {
                return;
            }

            var original = current.ToString();
            var folded = Fold(original).Trim();
            if (folded.Length > 0)
            {
                result.Add((folded, original));
            }

            current.Clear();
        }

        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c) || c is '\'' or '’' or 'ʼ' or '`')
            {
                current.Append(c);
            }
            else
            {
                Flush();
            }
        }

        Flush();
        return result;
    }
}
