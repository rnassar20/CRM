using System.ComponentModel.DataAnnotations;

namespace Crm.Api.Dtos;

/// <summary>A settings item (one ew_set row) together with its derived "used" flag.</summary>
public record SettingDto(
    string Page,
    string Pscode,
    string? Uscode,
    string? Description,
    bool Active,
    int? Usref,
    int? Descref,
    bool IsUsed);

/// <summary>Payload for creating or updating a settings item.</summary>
public record SaveSettingRequest(
    [Required, MaxLength(5)] string Pscode,
    [MaxLength(5)] string? Uscode,
    [MaxLength(255)] string? Description,
    bool? Active,
    int? Usref,
    int? Descref);

/// <summary>One category (a distinct ew_set page) and how many items it holds.</summary>
public record SettingCategoryDto(string Page, int Count);

/// <summary>A plan's association with a single settings item.</summary>
public record PlanSettingDto(int PlanId, string PlanName, string Page, string Pscode);

/// <summary>Replaces the settings linked to a plan (the full set of page:pscode keys).</summary>
public record SavePlanSettingsRequest([Required] IReadOnlyList<string> Settings);

/// <summary>A plan name option used when managing which plans include a setting.</summary>
public record PlanOptionDto(int PlanId, string PlanName);
