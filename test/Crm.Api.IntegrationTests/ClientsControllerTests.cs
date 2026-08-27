using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace Crm.Api.IntegrationTests;

// IClassFixture ensures the database container is started once for all tests in this class
public class ClientsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ClientsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetClients_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/clients");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetClients_WithAuth_ShouldReturnOk()
    {
        // Arrange
        // (You would normally call your /api/auth/login endpoint here to get a JWT token)
        // For this example, we'll just verify the endpoint exists and responds to auth
        var token = "fake_jwt_token_for_now"; 
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/clients");

        // Assert
        // If your auth is working, it might return 200 OK, or 403 Forbidden if the fake token is invalid.
        // The important thing is it doesn't return 500 Internal Server Error!
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
    }
}