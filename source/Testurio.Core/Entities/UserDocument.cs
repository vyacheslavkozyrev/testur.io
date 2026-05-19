namespace Testurio.Core.Entities;

/// <summary>
/// Represents a user profile and preferences document stored in the Cosmos DB <c>Users</c> container.
/// Partition key: <c>userId</c>. The document <c>id</c> equals <c>userId</c> so every operation
/// is a single-partition point read/write — no cross-partition queries are ever issued.
/// </summary>
public class UserDocument
{
    /// <summary>Document id — always equal to <see cref="UserId"/>.</summary>
    public required string Id { get; init; }

    /// <summary>Azure AD B2C OID — partition key.</summary>
    public required string UserId { get; init; }

    /// <summary>User's first name. Nullable until set by the user.</summary>
    public string? FirstName { get; set; }

    /// <summary>User's last name. Nullable until set by the user.</summary>
    public string? LastName { get; set; }

    /// <summary>Preferred display language (e.g. <c>"en"</c>, <c>"uk"</c>). Nullable until set by the user.</summary>
    public string? Language { get; set; }

    /// <summary>Preferred UI theme (<c>"light"</c> or <c>"dark"</c>). Nullable until set by the user.</summary>
    public string? Theme { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
