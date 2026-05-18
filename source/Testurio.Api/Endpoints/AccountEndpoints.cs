using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Testurio.Api.DTOs;
using Testurio.Api.Middleware;
using Testurio.Api.Services;

namespace Testurio.Api.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder v1)
    {
        var account = v1.MapGroup("/account");

        account.MapGet("/profile", GetProfileAsync).WithName("GetAccountProfile");
        account.MapPatch("/profile", UpdateProfileAsync).WithName("UpdateAccountProfile")
            .AddEndpointFilter<ValidationFilter<UpdateProfileRequest>>();
        account.MapGet("/preferences", GetPreferencesAsync).WithName("GetAccountPreferences");
        account.MapPatch("/preferences", UpdatePreferencesAsync).WithName("UpdateAccountPreferences")
            .AddEndpointFilter<ValidationFilter<UpdatePreferencesRequest>>();

        return v1;
    }

    private static async Task<Ok<AccountProfileDto>> GetProfileAsync(
        ClaimsPrincipal user,
        IAccountService accountService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var profile = await accountService.GetProfileAsync(userId, cancellationToken);
        return TypedResults.Ok(profile);
    }

    private static async Task<Ok<AccountProfileDto>> UpdateProfileAsync(
        UpdateProfileRequest request,
        ClaimsPrincipal user,
        IAccountService accountService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var profile = await accountService.UpdateProfileAsync(userId, request, cancellationToken);
        return TypedResults.Ok(profile);
    }

    private static async Task<Results<Ok<AccountPreferencesDto>, NotFound>> GetPreferencesAsync(
        ClaimsPrincipal user,
        IAccountService accountService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var prefs = await accountService.GetPreferencesAsync(userId, cancellationToken);
        return prefs is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(prefs);
    }

    private static async Task<Ok<AccountPreferencesDto>> UpdatePreferencesAsync(
        UpdatePreferencesRequest request,
        ClaimsPrincipal user,
        IAccountService accountService,
        CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var prefs = await accountService.UpdatePreferencesAsync(userId, request, cancellationToken);
        return TypedResults.Ok(prefs);
    }
}
