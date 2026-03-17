using Zack.DomainCommons.Models;

namespace Listening.Domain.ValueObjects;
public record Sentence(TimeSpan StartTime, TimeSpan EndTime, string Value);

public record SentenceV2(TimeSpan StartTime, TimeSpan EndTime, MultiSubTitle Value);
