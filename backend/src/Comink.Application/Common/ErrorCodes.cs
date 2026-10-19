// Mã lỗi dùng chung giữa backend, web và app.
// Backend trả mã lỗi (tiếng Anh); client tự dịch sang vi/en — xem docs/05_api_contract.md mục 5.
namespace Comink.Application.Common;

/// <summary>
/// Hằng số mã lỗi. Là hợp đồng chung, không đổi giá trị chuỗi khi đã phát hành.
/// </summary>
public static class ErrorCodes
{
    // --- Chung ---
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string InternalError = "INTERNAL_ERROR";

    // --- Xác thực ---
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    public const string AuthAccountLocked = "AUTH_ACCOUNT_LOCKED";
    public const string AuthRefreshTokenInvalid = "AUTH_REFRESH_TOKEN_INVALID";
    public const string AuthRefreshTokenReused = "AUTH_REFRESH_TOKEN_REUSED";
    public const string AuthEmailAlreadyExists = "AUTH_EMAIL_ALREADY_EXISTS";
    public const string AuthUserNameAlreadyExists = "AUTH_USERNAME_ALREADY_EXISTS";

    // --- Mạng xã hội: bạn bè và theo dõi (Giai đoạn 2) ---
    public const string SocialCannotFriendSelf = "SOCIAL_CANNOT_FRIEND_SELF";
    public const string SocialCannotFollowSelf = "SOCIAL_CANNOT_FOLLOW_SELF";
    public const string SocialAlreadyFriends = "SOCIAL_ALREADY_FRIENDS";
    public const string SocialFriendRequestNotFound = "SOCIAL_FRIEND_REQUEST_NOT_FOUND";
    public const string SocialNotRequestAddressee = "SOCIAL_NOT_REQUEST_ADDRESSEE";

    // --- Mạng xã hội: bài viết và bình luận (Giai đoạn 2) ---
    public const string PostNotFound = "POST_NOT_FOUND";
    public const string PostContentRequired = "POST_CONTENT_REQUIRED";
    public const string PostTooManyMedia = "POST_TOO_MANY_MEDIA";
    public const string PostMediaNotOwned = "POST_MEDIA_NOT_OWNED";
    public const string CommentNotFound = "COMMENT_NOT_FOUND";
    public const string CommentReplyDepthExceeded = "COMMENT_REPLY_DEPTH_EXCEEDED";

    // --- Media (Giai đoạn 2) ---
    public const string MediaUnsupportedType = "MEDIA_UNSUPPORTED_TYPE";
    public const string MediaTooLarge = "MEDIA_TOO_LARGE";
    public const string MediaNotFound = "MEDIA_NOT_FOUND";

    /// <summary>Tệp không thuộc người gửi hoặc sai ngữ cảnh sử dụng (dùng cho tin nhắn).</summary>
    public const string MediaNotOwned = "MEDIA_NOT_OWNED";

    // --- Nhắn tin (Giai đoạn 3) — docs/05_api_contract.md mục 8.8 ---
    public const string ConversationNotFound = "CONVERSATION_NOT_FOUND";
    public const string MessagingCannotMessageSelf = "MESSAGING_CANNOT_MESSAGE_SELF";
    public const string MessagingNotFriends = "MESSAGING_NOT_FRIENDS";
    public const string MessageContentRequired = "MESSAGE_CONTENT_REQUIRED";
    public const string MessageTooManyAttachments = "MESSAGE_TOO_MANY_ATTACHMENTS";
    public const string MessageClientIdRequired = "MESSAGE_CLIENT_ID_REQUIRED";
    public const string MessageInvalidCursor = "MESSAGE_INVALID_CURSOR";

    // --- Cuộc gọi (Giai đoạn 4) — docs/05_api_contract.md mục 9.7 ---
    public const string CallNotFound = "CALL_NOT_FOUND";
    public const string CallAlreadyActive = "CALL_ALREADY_ACTIVE";
    public const string CallAlreadyEnded = "CALL_ALREADY_ENDED";
    public const string CallNotRinging = "CALL_NOT_RINGING";
    public const string CallProviderUnavailable = "CALL_PROVIDER_UNAVAILABLE";
}
