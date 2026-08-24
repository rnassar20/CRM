using System.ComponentModel.DataAnnotations;

namespace Crm.Api.Dtos;

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

public record RegisterRequest(
    [Required, MaxLength(200)] string FullName,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password);

public record UserDto(int Id, string FullName, string Email, string Role, bool IsActive, DateTime CreatedAt);

public record AuthResponse(string Token, DateTime ExpiresAtUtc, UserDto User);
