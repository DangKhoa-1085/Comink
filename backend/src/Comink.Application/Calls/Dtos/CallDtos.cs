// DTO cho module Cuộc gọi (docs/05_api_contract.md mục 9).
namespace Comink.Application.Calls.Dtos;

/// <summary>
/// Yêu cầu bắt đầu cuộc gọi **trong một hội thoại** (1–1 hoặc nhóm).
/// <para>
/// Quyết định D6 (2026-10-23): bỏ nhánh tạo "hội thoại nhóm ẩn" — gọi nhóm chỉ bắt đầu từ
/// hội thoại nhóm thật, đúng như Messenger. Mọi thành viên còn lại đều nhận `CallIncoming`.
/// </para>
/// </summary>
public record StartCallRequest(Guid ConversationId, string Type);

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

/// <summary>Người còn lại trong hội thoại 1–1 (dùng cho lịch sử cuộc gọi).</summary>
public record CallPeerDto(Guid UserId, string DisplayName, string? AvatarUrl);

/// <summary>
/// Một dòng lịch sử cuộc gọi của tôi.
/// <para>
/// Quyết định D6 (2026-11-21): chỉ **thêm** trường so với bản GĐ 4 để client cũ không vỡ.
/// </para>
/// </summary>
public record CallSummaryDto(
    Guid Id,
    Guid ConversationId,
    string Type,
    string Status,
    string? EndReason,
    DateTimeOffset CreatedAt,
    int? DurationSeconds,
    Guid InitiatedBy,
    bool IWasInitiator,
    string MyStatus,
    bool IsMissed,
    string ConversationType,
    string? ConversationTitle,
    CallPeerDto? Peer,
    string InitiatorDisplayName,
    DateTimeOffset? AnsweredAt,
    DateTimeOffset? EndedAt);

/// <summary>Dữ liệu phát kèm sự kiện kết thúc cuộc gọi.</summary>
public record CallEndedDto(Guid CallId, string Status, string? EndReason, int? DurationSeconds);
