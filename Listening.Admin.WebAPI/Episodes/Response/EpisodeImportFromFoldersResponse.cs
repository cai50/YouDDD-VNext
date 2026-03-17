namespace Listening.Admin.WebAPI.Episodes;

public record EpisodeImportFromFoldersResponse(
    int SuccessCount,
    int SkipCount,
    string[] ImportedFiles,
    string[] SkippedMessages);