using Testurio.Api.DTOs;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;

namespace Testurio.Api.Services;

public class AccountService(IUserRepository userRepository) : IAccountService
{
    public async Task<AccountProfileDto> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var doc = await userRepository.GetByUserIdAsync(userId, cancellationToken);
        return new AccountProfileDto(userId, doc?.DisplayName);
    }

    public async Task<AccountProfileDto> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await userRepository.GetByUserIdAsync(userId, cancellationToken);

        var doc = existing is not null
            ? existing
            : new UserDocument { Id = userId, UserId = userId };

        doc.DisplayName = request.DisplayName.Trim();
        doc.UpdatedAt = DateTimeOffset.UtcNow;

        var saved = await userRepository.UpsertAsync(doc, cancellationToken);
        return new AccountProfileDto(saved.UserId, saved.DisplayName);
    }

    public async Task<AccountPreferencesDto?> GetPreferencesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var doc = await userRepository.GetByUserIdAsync(userId, cancellationToken);
        if (doc is null) return null;
        return new AccountPreferencesDto(doc.Language, doc.Theme);
    }

    public async Task<AccountPreferencesDto> UpdatePreferencesAsync(string userId, UpdatePreferencesRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await userRepository.GetByUserIdAsync(userId, cancellationToken);

        var doc = existing is not null
            ? existing
            : new UserDocument { Id = userId, UserId = userId };

        if (request.Language is not null)
            doc.Language = request.Language;
        if (request.Theme is not null)
            doc.Theme = request.Theme;

        doc.UpdatedAt = DateTimeOffset.UtcNow;

        var saved = await userRepository.UpsertAsync(doc, cancellationToken);
        return new AccountPreferencesDto(saved.Language, saved.Theme);
    }
}
