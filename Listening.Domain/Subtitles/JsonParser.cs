using Listening.Domain.ValueObjects;
using System.Text.Json;
using Zack.DomainCommons.Models;
using System.Linq;
using System;
using System.Text.RegularExpressions;

namespace Listening.Domain.Subtitles
{
    class JsonParser : ISubtitleParser
    {
        public bool Accept(string typeName)
        {
            return typeName.Equals("json", StringComparison.OrdinalIgnoreCase);
        }

        public IEnumerable<Sentence> Parse(string subtitle, bool IsEng)
        {
            return JsonSerializer.Deserialize<IEnumerable<Sentence>>(subtitle);
        }

        public (IEnumerable<Sentence>, IEnumerable<Sentence>) ParseV2(MultiSubTitle subtitle)
        {
            return (JsonSerializer.Deserialize<IEnumerable<Sentence>>(subtitle.Chinese)
                , JsonSerializer.Deserialize<IEnumerable<Sentence>>(subtitle.English));
        }

        public MultiSubTitle Trans(string subtitle)
        {
            // 尝试先解析为 MultiSubTitle
            try
            {
                var multi = JsonSerializer.Deserialize<MultiSubTitle>(subtitle);
                if (multi != null)
                {
                    return multi;
                }
            }
            catch
            {
                // ignore and try next
            }

            // 尝试解析为 Sentence 列表（单语），如果成功则根据是否包含中文放到对应字段
            try
            {
                var sentences = JsonSerializer.Deserialize<IEnumerable<Sentence>>(subtitle);
                if (sentences != null)
                {
                    bool hasChinese = sentences.Any(s => !string.IsNullOrWhiteSpace(s.Value) && Regex.IsMatch(s.Value, @"[\u4e00-\u9fa5]"));
                    if (hasChinese)
                    {
                        return new MultiSubTitle(JsonSerializer.Serialize(sentences), JsonSerializer.Serialize(Enumerable.Empty<Sentence>()));
                    }
                    else
                    {
                        return new MultiSubTitle(JsonSerializer.Serialize(Enumerable.Empty<Sentence>()), JsonSerializer.Serialize(sentences));
                    }
                }
            }
            catch
            {
                // ignore
            }

            throw new ApplicationException("无法将输入的 JSON 转换为 MultiSubTitle");
        }
    }
}
