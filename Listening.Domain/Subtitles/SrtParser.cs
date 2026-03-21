using Listening.Domain.ValueObjects;
using System.Text;
using System.Text.Json;
using System.Linq;
using System;
using System.Collections.Generic;
using Zack.DomainCommons.Models;

namespace Listening.Domain.Subtitles
{
    /// <summary>
    /// parser for *.srt files and *.vtt files
    /// </summary>
    class SrtParser : ISubtitleParser
    {
        public bool Accept(string typeName)
        {
            return typeName.Equals("srt", StringComparison.OrdinalIgnoreCase)
                || typeName.Equals("vtt", StringComparison.OrdinalIgnoreCase);
        }

        public IEnumerable<Sentence> Parse(string subtitle, bool IsEng)
        {
            var srtParser = new SubtitlesParser.Classes.Parsers.SubParser();
            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(subtitle)))
            {
                var items = srtParser.ParseStream(ms);
                return items.Select(s => new Sentence(TimeSpan.FromMilliseconds(s.StartTime),
                    TimeSpan.FromMilliseconds(s.EndTime), String.Join(" ", s.Lines)));
            }
        }

        public (IEnumerable<Sentence>, IEnumerable<Sentence>) ParseV2(MultiSubTitle subtitle)
        {
            var srtParser = new SubtitlesParser.Classes.Parsers.SubParser();
            IEnumerable<Sentence> enSubs = Enumerable.Empty<Sentence>();
            IEnumerable<Sentence> zhSubs = Enumerable.Empty<Sentence>();

            var enText = subtitle?.English ?? string.Empty;
            var zhText = subtitle?.Chinese ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(enText))
            {
                try
                {
                    using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(enText)))
                    {
                        var items = srtParser.ParseStream(ms);
                        if (items != null)
                        {
                            enSubs = items.Select(s => new Sentence(TimeSpan.FromMilliseconds(s.StartTime),
                                TimeSpan.FromMilliseconds(s.EndTime), String.Join(" \n", s.PlaintextLines)));
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
                        var items = srtParser.ParseStream(ms);
                        if (items != null)
                        {
                            zhSubs = items.Select(s => new Sentence(TimeSpan.FromMilliseconds(s.StartTime),
                                TimeSpan.FromMilliseconds(s.EndTime), String.Join(" \n", s.PlaintextLines)));
                        }
                    }
                }
                catch (Exception)
                {
                    zhSubs = Enumerable.Empty<Sentence>();
                }
            }

            return (zhSubs, enSubs);
        }

        public MultiSubTitle Trans(string subtitle)
        {
            return new MultiSubTitle(string.Empty, subtitle);
        }
    }
}
