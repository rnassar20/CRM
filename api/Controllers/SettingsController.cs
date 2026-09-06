using Crm.Api.Data;
using Crm.Api.Dtos;
using Crm.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController(AppDbContext db) : ControllerBase
{
    /// <summary>Distinct settings categories (the ew_set "page" column) with row counts.</summary>
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<SettingCategoryDto>>> GetCategories()
    {
        // GroupBy().Count() is translatable, but the record constructor is not — so select into an
        // anonymous shape and map to the DTO in memory.
        var cats = await db.EwSets.AsNoTracking()
            .GroupBy(e => e.Page)
            .Select(g => new { Page = g.Key, Count = g.Count() })
            .OrderBy(c => c.Page)
            .ToListAsync();
        return Ok(cats.Select(c => new SettingCategoryDto(c.Page, c.Count)).ToList());
    }

    /// <summary>All settings in a category, each with its derived "used" flag (linked by any plan).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SettingDto>>> GetAll([FromQuery] string? page)
    {
        var query = db.EwSets.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(page)) query = query.Where(e => e.Page == page.Trim());

        // Project into an anonymous type (EF-translatable), then map to the DTO in memory.
        // Two round-trips but guaranteed translatable and keeps the "used" check simple.
        var rows = await query
            .OrderBy(e => e.Page).ThenBy(e => e.Pscode)
            .Select(e => new
            {
                e.Page, e.Pscode, e.Uscode, e.Description,
                Active = e.Status != null && e.Status != 0,
                e.Usref, e.Descref
            })
            .ToListAsync();

        var usedKeys = new HashSet<string>(
            await db.PlanSettings.AsNoTracking()
                .Select(ps => ps.Page + ":" + ps.Pscode)
                .ToListAsync());

        var items = rows
            .Select(x => new SettingDto(
                x.Page, x.Pscode, x.Uscode, x.Description, x.Active,
                x.Usref, x.Descref,
                usedKeys.Contains(x.Page + ":" + x.Pscode)))
            .ToList();
        return Ok(items);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SettingDto>> Create([FromQuery] string page, SaveSettingRequest request)
    {
        var pageKey = NormalizePage(page);
        if (pageKey is null) return BadRequest("Category (page) is required and must be ≤ 5 characters.");
        var pscode = NormalizeCode(request.Pscode, "Pscode");
        if (pscode is null) return BadRequest("Pscode is required and must be ≤ 5 characters.");

        var exists = await db.EwSets.AnyAsync(e => e.Page == pageKey && e.Pscode == pscode);
        if (exists) return Conflict($"'{pageKey}:{pscode}' already exists.");

        var item = new EwSet
        {
            Page = pageKey,
            Pscode = pscode,
            Uscode = NormalizeCode(request.Uscode, "Uscode"),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = (short)(request.Active == false ? 0 : 1),
            Usref = request.Usref,
            Descref = request.Descref
        };
        db.EwSets.Add(item);
        await db.SaveChangesAsync();
        var dto = ToDto(item, isUsed: false);
        return CreatedAtAction(nameof(GetAll), dto);
    }

    [HttpPut("{page}/{pscode}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string page, string pscode, SaveSettingRequest request)
    {
        var item = await db.EwSets
            .FirstOrDefaultAsync(e => e.Page == page && e.Pscode == pscode);
        if (item is null) return NotFound();

        var newCode = NormalizeCode(request.Pscode, "Pscode");
        if (newCode is null) return BadRequest("Pscode is required and must be ≤ 5 characters.");

        // If the system code changes, move the row (and any plan links) to the new key.
        if (newCode != item.Pscode)
        {
            var exists = await db.EwSets.AnyAsync(e => e.Page == item.Page && e.Pscode == newCode);
            if (exists) return Conflict($"'{item.Page}:{newCode}' already exists.");

            var links = await db.PlanSettings
                .Where(ps => ps.Page == item.Page && ps.Pscode == item.Pscode)
                .ToListAsync();
            foreach (var link in links) link.Pscode = newCode;

            // Delete + reinsert so the composite key (Page, Pscode) moves.
            db.EwSets.Remove(item);
            item = new EwSet { Page = item.Page, Pscode = newCode, Uscode = item.Uscode, Description = item.Description, Status = item.Status, Usref = item.Usref, Descref = item.Descref };
            db.EwSets.Add(item);
        }

        item.Uscode = NormalizeCode(request.Uscode, "Uscode");
        item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        item.Status = (short)(request.Active == false ? 0 : 1);
        item.Usref = request.Usref;
        item.Descref = request.Descref;

        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Quick toggle of the Active flag (Status column) from the list row.</summary>
    [HttpPatch("{page}/{pscode}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleStatus(string page, string pscode, [FromBody] bool active)
    {
        var item = await db.EwSets.FirstOrDefaultAsync(e => e.Page == page && e.Pscode == pscode);
        if (item is null) return NotFound();
        item.Status = (short)(active ? 1 : 0);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{page}/{pscode}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string page, string pscode)
    {
        var item = await db.EwSets.FirstOrDefaultAsync(e => e.Page == page && e.Pscode == pscode);
        if (item is null) return NotFound();

        // A setting that is linked by any plan is "in use" and cannot be removed.
        var inUse = await db.PlanSettings.AnyAsync(ps => ps.Page == item.Page && ps.Pscode == item.Pscode);
        if (inUse) return BadRequest($"'{item.Page}:{item.Pscode}' is in use by one or more plans. Unlink it from those plans first.");

        db.EwSets.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- plan ↔ setting association ----------

    /// <summary>Enumerate the settings a plan currently includes.</summary>
    [HttpGet("/api/plans/{planId:int}/settings")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<PlanSettingDto>>> GetPlanSettings(int planId)
    {
        var plan = await db.Plans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == planId);
        if (plan is null) return NotFound();
        var links = await db.PlanSettings.AsNoTracking()
            .Where(ps => ps.PlanId == planId)
            .OrderBy(ps => ps.Page).ThenBy(ps => ps.Pscode)
            .ToListAsync();
        return Ok(links.Select(l => new PlanSettingDto(planId, plan.Name, l.Page, l.Pscode)).ToList());
    }

    /// <summary>Replace the full set of settings a plan includes.</summary>
    [HttpPut("/api/plans/{planId:int}/settings")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetPlanSettings(int planId, SavePlanSettingsRequest request)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == planId);
        if (plan is null) return NotFound();

        // Parse "page:pscode" keys, ignore malformed entries, and keep only ones that exist.
        var parsed = new List<(string Page, string Pscode)>();
        foreach (var raw in request.Settings)
        {
            var parts = raw.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;
            var page = parts[0].Trim();
            var code = parts[1].Trim();
            if (page.Length is < 1 or > 5 || code.Length is < 1 or > 5) continue;
            parsed.Add((page, code));
        }
        parsed = parsed.Distinct().ToList();

        var dbLinks = await db.PlanSettings.Where(ps => ps.PlanId == planId).ToListAsync();
        db.PlanSettings.RemoveRange(dbLinks);

        if (parsed.Count > 0)
        {
            var pages = parsed.Select(p => p.Page).Distinct().ToList();
            var existing = await db.EwSets
                .Where(e => pages.Contains(e.Page))
                .Select(e => new { e.Page, e.Pscode })
                .ToListAsync();
            var valid = parsed
                .Where(p => existing.Any(e => e.Page == p.Page && e.Pscode == p.Pscode))
                .Select(p => new PlanSetting { PlanId = planId, Page = p.Page, Pscode = p.Pscode })
                .ToList();
            db.PlanSettings.AddRange(valid);
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- per-setting plan association ----------

    /// <summary>The plans currently linked to a single settings item.</summary>
    [HttpGet("{page}/{pscode}/plans")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<PlanOptionDto>>> GetLinkedPlans(string page, string pscode)
    {
        var exists = await db.EwSets.AnyAsync(e => e.Page == page && e.Pscode == pscode);
        if (!exists) return NotFound();
        // Join into an anonymous shape (translatable), then map to the DTO in memory.
        var links = await db.PlanSettings.AsNoTracking()
            .Where(ps => ps.Page == page && ps.Pscode == pscode)
            .Join(db.Plans.AsNoTracking(), ps => ps.PlanId, p => p.Id, (ps, p) => new { p.Id, p.Name })
            .OrderBy(x => x.Name)
            .ToListAsync();
        return Ok(links.Select(x => new PlanOptionDto(x.Id, x.Name)).ToList());
    }

    /// <summary>Link a settings item to a plan.</summary>
    [HttpPost("{page}/{pscode}/plans/{planId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> LinkPlan(string page, string pscode, int planId)
    {
        var exists = await db.EwSets.AnyAsync(e => e.Page == page && e.Pscode == pscode);
        if (!exists) return NotFound();
        var plan = await db.Plans.AnyAsync(p => p.Id == planId);
        if (!plan) return NotFound("Plan not found.");

        var already = await db.PlanSettings.AnyAsync(ps => ps.PlanId == planId && ps.Page == page && ps.Pscode == pscode);
        if (!already)
            db.PlanSettings.Add(new PlanSetting { PlanId = planId, Page = page, Pscode = pscode });
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Unlink a settings item from a plan.</summary>
    [HttpDelete("{page}/{pscode}/plans/{planId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UnlinkPlan(string page, string pscode, int planId)
    {
        var link = await db.PlanSettings
            .FirstOrDefaultAsync(ps => ps.PlanId == planId && ps.Page == page && ps.Pscode == pscode);
        if (link is null) return NoContent(); // already unlinked — treat as success
        db.PlanSettings.Remove(link);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- helpers ----------

    private static SettingDto ToDto(EwSet e, bool isUsed) =>
        new(e.Page, e.Pscode, e.Uscode, e.Description, e.Status != null && e.Status != 0, e.Usref, e.Descref, isUsed);

    private static string? NormalizePage(string? raw) => NormalizeCode(raw, "Page");

    private static string? NormalizeCode(string? raw, string _)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        return t.Length > 5 ? null : t;
    }
}
