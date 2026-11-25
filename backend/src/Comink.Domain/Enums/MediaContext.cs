// Enum MediaContext — tệp được tải lên để dùng ở đâu (docs/03_database_design.md mục 4).
namespace Comink.Domain.Enums;

/// <summary>Mục đích tải lên của một tài sản media.</summary>
public enum MediaContext
{
    Post,
    Message,
    Avatar,
    Cover,
    Album
}
