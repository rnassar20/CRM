using System.Net;
using System.Net.Http.Json;
using Crm.Api.Dtos;
using FluentAssertions;
using Xunit;

namespace Crm.Api.IntegrationTests;

public class AuthFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HttpClient _authClient;

    public AuthFlowTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _authClient = factory.CreateClient();
    }

    [Fact]
    public async Task Clients_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/clients");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Register_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _authClient.PostAsJsonAsync("/api/auth/register",
            new { fullName = "X", email = "x@x.example", password = "Password1" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithSeedAdminCredentials_ReturnsToken()
    {
        var response = await _authClient.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@crm.local", password = "Admin@123" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrEmpty();
        body.User.Email.Should().Be("admin@crm.local");
        body.User.Role.Should().Be("Admin");
        body.User.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsBadRequest()
    {
        var response = await _authClient.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@crm.local", password = "wrong" });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task Login_TruncatesAndNormalisesEmail()
    {
        var response = await _authClient.PostAsJsonAsync("/api/auth/login",
            new { email = "  ADMIN@Crm.Local  ", password = "Admin@123" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Me_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsBadRequest()
    {
        var token = await LoginTokenAsync("admin@crm.local", "Admin@123");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "wrong", newPassword = "NewPass1234" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_WithShortNewPassword_ReturnsBadRequest()
    {
        var token = await LoginTokenAsync("admin@crm.local", "Admin@123");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = "Admin@123", newPassword = "short" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_NewAgentAccount_ReturnsToken()
    {
        var adminToken = await LoginTokenAsync("admin@crm.local", "Admin@123");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Jane Agent", email = "jane@x.example", password = "Password1" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        body.Should().NotBeNull();
        body!.User.Role.Should().Be("Agent");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        var adminToken = await LoginTokenAsync("admin@crm.local", "Admin@123");
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        await _client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "First", email = "dup@x.example", password = "Password1" });
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Second", email = "dup@x.example", password = "Password1" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<string> LoginTokenAsync(string email, string password)
    {
        var response = await _authClient.PostAsJsonAsync("/api/auth/login",
            new { email, password });
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }
}