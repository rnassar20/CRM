using System.Net;
using System.Net.Http.Json;
using Crm.Api.Dtos;
using FluentAssertions;
using Xunit;

namespace Crm.Api.IntegrationTests;

public class ClientsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HttpClient _authClient;

    public ClientsControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _authClient = factory.CreateClient();
    }

    [Fact]
    public async Task GetClients_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/clients");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetClients_WithValidToken_ReturnsPagedClients()
    {
        AuthorizeWithAdmin();
        var response = await _client.GetAsync("/api/clients?pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<ClientListItemDto>>();
        body.Should().NotBeNull();
        body!.Items.Should().NotBeEmpty();
        body.Total.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetClients_SearchFiltersResults()
    {
        AuthorizeWithAdmin();
        var response = await _client.GetAsync("/api/clients?q=Al-Shifa");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<ClientListItemDto>>();
        body!.Items.Should().OnlyContain(c => c.Name.Contains("Al-Shifa"));
    }

    [Fact]
    public async Task GetClientById_NotFound_Returns404()
    {
        AuthorizeWithAdmin();
        var response = await _client.GetAsync("/api/clients/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateClient_WithValidData_Returns201()
    {
        AuthorizeWithAdmin();
        var response = await _client.PostAsJsonAsync("/api/clients", new
        {
            name = "Test Pharmacy",
            contactPerson = "Test Person",
            phone = "+15550001",
            email = "test@example.com",
            city = "Cairo",
            type = "Pharmacy",
            status = "Potential",
            notes = "created by integration test"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateClient_WithUnknownType_ReturnsBadRequest()
    {
        AuthorizeWithAdmin();
        var response = await _client.PostAsJsonAsync("/api/clients", new
        {
            name = "Bad Type",
            contactPerson = "p",
            phone = "+15550002",
            type = "NonExistent",
            status = "Potential"
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private void AuthorizeWithAdmin()
    {
        var token = LoginTokenAsync("admin@crm.local", "Admin@123").GetAwaiter().GetResult();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> LoginTokenAsync(string email, string password)
    {
        var response = await _authClient.PostAsJsonAsync("/api/auth/login",
            new { email, password });
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }
}