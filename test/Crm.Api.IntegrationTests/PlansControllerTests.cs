using System.Net;
using System.Net.Http.Json;
using Crm.Api.Dtos;
using FluentAssertions;
using Xunit;

namespace Crm.Api.IntegrationTests;

public class PlansControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly HttpClient _authClient;
    private readonly CustomWebApplicationFactory _factory;

    public PlansControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _authClient = factory.CreateClient();
    }

    [Fact]
    public async Task GetPlans_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/plans");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPlans_WithValidToken_ReturnsPlans()
    {
        AuthorizeWithAdmin();
        var response = await _client.GetAsync("/api/plans");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<PlanDto>>();
        body.Should().NotBeNull();
        body!.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreatePlan_WithoutAdminRole_ReturnsForbidden()
    {
        var adminToken = await LoginTokenAsync("admin@crm.local", "Admin@123");
        var agentToken = await CreateAgentAndReturnTokenAsync(adminToken, "agent_plans@x.example");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", agentToken);
        var response = await _client.PostAsJsonAsync("/api/plans", new
        {
            name = "No Access Plan",
            cycle = "Monthly",
            price = 100m
        });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreatePlan_WithAdminRole_ReturnsCreated()
    {
        AuthorizeWithAdmin();
        var response = await _client.PostAsJsonAsync("/api/plans", new
        {
            name = "Integration Test Plan",
            cycle = "Monthly",
            price = 50m,
            isActive = true
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<PlanDto>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("Integration Test Plan");
        body.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreatePlan_WithUnknownCycle_ReturnsBadRequest()
    {
        AuthorizeWithAdmin();
        var response = await _client.PostAsJsonAsync("/api/plans", new
        {
            name = "Bad Cycle Plan",
            cycle = "Weekly",
            price = 100m
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<string> CreateAgentAndReturnTokenAsync(string adminToken, string email)
    {
        using var regClient = _factory.CreateClient();
        regClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        await regClient.PostAsJsonAsync("/api/auth/register",
            new { fullName = "Test Agent", email, password = "Password1" });
        var loginResponse = await _authClient.PostAsJsonAsync("/api/auth/login",
            new { email, password = "Password1" });
        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    private void AuthorizeWithAdmin()
    {
        var token = LoginTokenAsync("admin@crm.local", "Admin@123").GetAwaiter().GetResult();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> LoginTokenAsync(string email, string password)
    {
        var response = await _authClient.PostAsJsonAsync("/api/auth/login",
            new { email, password });
        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException($"Login failed: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }
}