using Testurio.Api.DTOs;

namespace Testurio.Api.Services;

public interface IAccountService
{
    Task<AccountProfileDto> GetProfileAsync(string userId, CancellationToken cancellationToken = default);
    Task<AccountProfileDto> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
    Task<AccountPreferencesDto?> GetPreferencesAsync(string userId, CancellationToken cancellationToken = default);
    Task<AccountPreferencesDto> UpdatePreferencesAsync(string userId, UpdatePreferencesRequest request, CancellationToken cancellationToken = default);
}
