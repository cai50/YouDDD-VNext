using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Listening.Domain.ValueObjects;

namespace Listening.Domain.Subtitles
{
    internal static class LanguageUtils
    {
        public static bool IsMostlyChinese(IEnumerable<Sentence> sentences)
        {
            if (sentences == null) return false;
            long chineseCount = 0;
            long totalCount = 0;

            foreach (var s in sentences)
            {
                var text = s?.Value;
                if (string.IsNullOrEmpty(text)) continue;

                foreach (var c in text)
                {
                    if (char.IsWhiteSpace(c)) continue;
                    totalCount++;
                    // fast range check for CJK Unified Ideographs block
                    if (c >= '\u4e00' && c <= '\u9fff') chineseCount++;
                }
            }

            return totalCount > 0 && (double)chineseCount / totalCount > 0.5;
        }
    }
}