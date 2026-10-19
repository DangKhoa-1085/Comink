// DTO cho module Cuộc gọi (docs/05_api_contract.md mục 9).
namespace Comink.Application.Calls.Dtos;

/// <summary>Yêu cầu bắt đầu cuộc gọi: 1–1 (có `conversationId`) hoặc nhóm (có `participantIds`).</summary>
public record StartCallRequest(Guid? ConversationId, IReadOnlyList<Guid>? ParticipantIds, string Type);

/// <summary>Một người trong cuộc gọi.</summary>
public record CallParticipantDto(Guid UserId, string DisplayName, string Status, DateTimeOffset InvitedAt);

/// <summary>Thông tin một cuộc gọi.</summary>
public record CallDto(
    Guid Id,
    Guid ConversationId,
    string Type,
    string Status,
    string RoomName,
    Guid InitiatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AnsweredAt,
    DateTimeOffset? EndedAt,
    int? DurationSeconds,
    string? EndReason,
    IReadOnlyList<CallParticipantDto> Participants);

/// <summary>Token ngắn hạn để client vào room LiveKit.</summary>
public record CallTokenDto(string Url, string Token, string RoomName, DateTimeOffset ExpiresAt);

/// <summary>Một dòng lịch sử cuộc gọi của tôi.</summary>
public record CallSummaryDto(
    Guid Id,
    Guid ConversationId,
    string Type,
    string Status,
    string? EndReason,
    DateTimeOffset CreatedAt,
    int? DurationSeconds,
    Guid InitiatedBy,
    bool IWasInitiator);

/// <summary>Dữ liệu phát kèm sự kiện kết thúc cuộc gọi.</summary>
public record CallEndedDto(Guid CallId, string Status, string? EndReason, int? DurationSeconds);
