namespace SIAD.Core.DTOs.Contabilidad;

public record DiarioUpsertDto(
    string Code,
    string Name,
    bool IsActive,
    bool AllowsManual,
    string User,
    long? JournalId = null,
    string? Description = null,
    string? SequencePrefix = null,
    bool IsDefaultManual = false);
