namespace SIAD.Core.DTOs.Contabilidad;

public record DiarioDto(
    long JournalId,
    string Code,
    string Name,
    string? Description,
    string? SequencePrefix,
    long LastSequence,
    bool IsActive,
    bool AllowsManual,
    bool IsDefaultManual);
