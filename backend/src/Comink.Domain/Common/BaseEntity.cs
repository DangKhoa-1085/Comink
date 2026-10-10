// Lớp cơ sở cho tất cả Entity, cung cấp khóa chính UUIDv7
namespace Comink.Domain.Common;

/// <summary>
/// Lớp cơ sở cho các entity có khóa chính UUID.
/// Khóa được sinh bằng UUIDv7 (tăng dần theo thời gian).
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}
