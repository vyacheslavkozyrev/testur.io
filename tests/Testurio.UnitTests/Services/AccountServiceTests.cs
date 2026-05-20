using Moq;
using Testurio.Api.DTOs;
using Testurio.Api.Services;
using Testurio.Core.Entities;
using Testurio.Core.Repositories;

namespace Testurio.UnitTests.Services;

public class AccountServiceTests
{
    private readonly Mock<IUserRepository> _repository = new();
    private readonly AccountService _sut;

    public AccountServiceTests()
    {
        _sut = new AccountService(_repository.Object);
    }

    private static UserDocument MakeUserDocument(string userId = "user-1", string? firstName = "Test", string? lastName = "User") =>
        new() { Id = userId, UserId = userId, FirstName = firstName, LastName = lastName };

    // ─── GetProfileAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetProfileAsync_ReturnsProfileDto_WhenUserExists()
    {
        var doc = MakeUserDocument();
        _repository.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var result = await _sut.GetProfileAsync("user-1");

        Assert.Equal("user-1", result.UserId);
        Assert.Equal("Test", result.FirstName);
        Assert.Equal("User", result.LastName);
    }

    [Fact]
    public async Task GetProfileAsync_ReturnsNullNames_WhenUserNotFound()
    {
        _repository.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDocument?)null);

        var result = await _sut.GetProfileAsync("user-1");

        Assert.Equal("user-1", result.UserId);
        Assert.Null(result.FirstName);
        Assert.Null(result.LastName);
    }

    // ─── UpdateProfileAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProfileAsync_UpsertAndReturnsDto_WhenUserExists()
    {
        var existing = MakeUserDocument(firstName: "Old", lastName: "Name");
        _repository.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repository.Setup(r => r.UpsertAsync(It.IsAny<UserDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDocument doc, CancellationToken _) => doc);

        var result = await _sut.UpdateProfileAsync("user-1", new UpdateProfileRequest { FirstName = "  New  ", LastName = "Name" });

        Assert.Equal("New", result.FirstName);
    }

    [Fact]
    public async Task UpdateProfileAsync_CreatesNewDocument_WhenUserNotFound()
    {
        _repository.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDocument?)null);
        _repository.Setup(r => r.UpsertAsync(It.IsAny<UserDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDocument doc, CancellationToken _) => doc);

        var result = await _sut.UpdateProfileAsync("user-1", new UpdateProfileRequest { FirstName = "Brand", LastName = "New" });

        Assert.Equal("Brand", result.FirstName);
        Assert.Equal("New", result.LastName);
        _repository.Verify(r => r.UpsertAsync(
            It.Is<UserDocument>(d => d.Id == "user-1" && d.UserId == "user-1"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_TrimsNames_BeforeUpsert()
    {
        _repository.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDocument?)null);
        _repository.Setup(r => r.UpsertAsync(It.IsAny<UserDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDocument doc, CancellationToken _) => doc);

        var result = await _sut.UpdateProfileAsync("user-1", new UpdateProfileRequest { FirstName = "  trimmed  " });

        Assert.Equal("trimmed", result.FirstName);
    }

    // ─── GetPreferencesAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetPreferencesAsync_ReturnsNull_WhenUserNotFound()
    {
        _repository.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDocument?)null);

        var result = await _sut.GetPreferencesAsync("user-1");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPreferencesAsync_ReturnsDto_WhenUserExists()
    {
        var doc = MakeUserDocument();
        doc.Language = "uk";
        doc.Theme = "dark";
        _repository.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var result = await _sut.GetPreferencesAsync("user-1");

        Assert.NotNull(result);
        Assert.Equal("uk", result.Language);
        Assert.Equal("dark", result.Theme);
    }

    // ─── UpdatePreferencesAsync ───────────────────────────────────────────────

    [Fact]
    public async Task UpdatePreferencesAsync_MergesFields_WhenUserExists()
    {
        var existing = MakeUserDocument();
        existing.Language = "en";
        existing.Theme = "light";
        _repository.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repository.Setup(r => r.UpsertAsync(It.IsAny<UserDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDocument doc, CancellationToken _) => doc);

        var result = await _sut.UpdatePreferencesAsync("user-1", new UpdatePreferencesRequest { Theme = "dark" });

        // Language unchanged; theme updated
        Assert.Equal("en", result.Language);
        Assert.Equal("dark", result.Theme);
    }

    [Fact]
    public async Task UpdatePreferencesAsync_CreatesDocument_WhenUserNotFound()
    {
        _repository.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDocument?)null);
        _repository.Setup(r => r.UpsertAsync(It.IsAny<UserDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDocument doc, CancellationToken _) => doc);

        var result = await _sut.UpdatePreferencesAsync("user-1", new UpdatePreferencesRequest { Language = "uk", Theme = "dark" });

        Assert.Equal("uk", result.Language);
        Assert.Equal("dark", result.Theme);
    }
}
