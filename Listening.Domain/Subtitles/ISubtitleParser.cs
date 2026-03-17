using Listening.Domain.ValueObjects;
using Zack.DomainCommons.Models;

namespace Listening.Domain.Subtitles
{
    interface ISubtitleParser
    {
        /// <summary>
        /// 本解析器是否能够解析typeName这个类型的字幕
        /// </summary>
        /// <param name="typeName"></param>
        /// <returns></returns>
        bool Accept(string typeName);

        /// <summary>
        /// 解析这个字幕subtitle
        /// </summary>
        /// <param name="subtitle"></param>
        /// <returns></returns>
        IEnumerable<Sentence> Parse(string subtitle, bool IsEng);

        (IEnumerable<Sentence>, IEnumerable<Sentence>) ParseV2(MultiSubTitle subtitle);

        MultiSubTitle Trans(string subtitle);
    }
}
