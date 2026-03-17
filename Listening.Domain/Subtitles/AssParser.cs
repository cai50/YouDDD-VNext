using Listening.Domain.ValueObjects;
using Opportunity.LrcParser;
using SubtitlesParser.Classes.Parsers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System;
using System.Linq;
using Zack.DomainCommons.Models;

namespace Listening.Domain.Subtitles
{
    /// <summary>
    /// parser for *.srt files and *.vtt files
    /// </summary>
    class AssParser : ISubtitleParser
    {
        public bool Accept(string typeName)
        {
            return typeName.Equals("ass", StringComparison.OrdinalIgnoreCase);
        }

        public (IEnumerable<Sentence>, IEnumerable<Sentence>) ParseV2(MultiSubTitle subtitle)
        {
            var assParser = new SubtitlesParser.Classes.Parsers.SubParser();
            IEnumerable<Sentence> enSubs = Enumerable.Empty<Sentence>();
            IEnumerable<Sentence> zhSubs = Enumerable.Empty<Sentence>();

            var enText = subtitle?.English ?? string.Empty;
            var zhText = subtitle?.Chinese ?? string.Empty;

            // 如果中文为空，但英文不为空，说明传入的是一份双语字幕（第一行为中文，第二行为英文），
            // 我们解析英文流并对每条字幕内容做拆分（优先按 "\\N" 拆分）
            if (string.IsNullOrWhiteSpace(zhText) && !string.IsNullOrWhiteSpace(enText))
            {
                try
                {
                    using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(enText)))
                    {
                        var subtitleItems = assParser.ParseStream(ms);
                        if (subtitleItems != null)
                        {
                            var zhList = new List<Sentence>();
                            var enList = new List<Sentence>();
                            foreach (var item in subtitleItems)
                            {
                                // 把多行合并为 ASS 风格的 \N，再用 SplitText 拆分中英文
                                var combined = string.Join("\\N", item.PlaintextLines ?? Enumerable.Empty<string>());
                                var (zhPart, enPart) = SplitText(combined);
                                zhList.Add(new Sentence(TimeSpan.FromMilliseconds(item.StartTime), TimeSpan.FromMilliseconds(item.EndTime), zhPart));
                                enList.Add(new Sentence(TimeSpan.FromMilliseconds(item.StartTime), TimeSpan.FromMilliseconds(item.EndTime), enPart));
                            }
                            zhSubs = zhList;
                            enSubs = enList;
                        }
                    }
                }
                catch (Exception)
                {
                    zhSubs = Enumerable.Empty<Sentence>();
                    enSubs = Enumerable.Empty<Sentence>();
                }

                // 返回中文在前，英文在后
                return (zhSubs, enSubs);
            }

            // 否则分别解析英文和中文流（如果有）
            if (!string.IsNullOrWhiteSpace(enText))
            {
                try
                {
                    using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(enText)))
                    {
                        var subtitleItems = assParser.ParseStream(ms);

                        if (subtitleItems != null)
                        {
                            enSubs = subtitleItems.Select(item => new Sentence(
                                StartTime: TimeSpan.FromMilliseconds(item.StartTime),
                                EndTime: TimeSpan.FromMilliseconds(item.EndTime),
                                // PlainContents 是 List<string>，代表该时间轴下的多行文本
                                Value: string.Join(Environment.NewLine, item.PlaintextLines)
                            ));
                        }
                    }
                }
                catch (Exception)
                {
                    enSubs = Enumerable.Empty<Sentence>();
                }
            }

            if (!string.IsNullOrWhiteSpace(zhText))
            {
                try
                {
                    using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(zhText)))
                    {
                        var subtitleItems = assParser.ParseStream(ms);
                        if (subtitleItems != null)
                        {
                            zhSubs = subtitleItems.Select(item => new Sentence(
                                StartTime: TimeSpan.FromMilliseconds(item.StartTime),
                                EndTime: TimeSpan.FromMilliseconds(item.EndTime),
                                // PlainContents 是 List<string>，代表该时间轴下的多行文本
                                Value: string.Join(Environment.NewLine, item.PlaintextLines)
                            ));
                        }
                    }
                }
                catch (Exception)
                {
                    zhSubs = Enumerable.Empty<Sentence>();
                }
            }

            // 返回中文在前，英文在后（与 JsonParser 保持一致）
            return (zhSubs, enSubs);
        }

        public MultiSubTitle Trans(string subtitle)
        {
            var assParser = new SubtitlesParser.Classes.Parsers.SubParser();
            try
            {
                using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(subtitle)))
                {
                    var subtitleItems = assParser.ParseStream(ms);
                    if (subtitleItems != null)
                    {
                        var first = subtitleItems.Select(item => new Sentence(
                            StartTime: TimeSpan.FromMilliseconds(item.StartTime),
                            EndTime: TimeSpan.FromMilliseconds(item.EndTime),
                            Value: item.PlaintextLines.ElementAtOrDefault(0) ?? ""));

                        var second = subtitleItems.Select(item => new Sentence(
                            StartTime: TimeSpan.FromMilliseconds(item.StartTime),
                            EndTime: TimeSpan.FromMilliseconds(item.EndTime),
                            Value: item.PlaintextLines.ElementAtOrDefault(1) ?? ""));

                        return new MultiSubTitle(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
                    }
                }
            }
            catch (Exception)
            {
                // ignore and fall through
            }

            return new MultiSubTitle("[]", "[]");
        }

        private (string zh, string en) SplitText(string text)
        {
            // 常见的 ASS 双语字幕用 \N 分隔
            if (text.Contains("\\N"))
            {
                var parts = text.Split(new[] { "\\N" }, StringSplitOptions.None);
                // 通常第一行中文，第二行英文，或者反之
                // 这里可以根据是否包含汉字简单判断
                if (Regex.IsMatch(parts[0], @"[\u4e00-\u9fa5]"))
                    return (parts[0], parts.Length > 1 ? parts[1] : "");
                else
                    return (parts.Length > 1 ? parts[1] : "", parts[0]);
            }

            // 如果没有 \N，尝试用正则匹配汉字和英文字符（简单的粗略拆分）
            string zh = Regex.Replace(text, @"[^\u4e00-\u9fa5，。！？；：“”（）\s]", "");
            string en = Regex.Replace(text, @"[\u4e00-\u9fa5]", "");

            return (zh.Trim(), en.Trim());
        }

        public IEnumerable<Sentence> Parse(string subtitle, bool IsEng)
        {
            throw new NotImplementedException();
        }


    }
}
