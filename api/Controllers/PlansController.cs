using Crm.Api.Data;
using Crm.Api.Dtos;
using Crm.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Controllers;

[ApiController]
[Route("api/plans")]
[Authorize]
public class PlansController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlanDto>>> GetAll([FromQuery] bool includeInactive = true)
    {
        var query = db.Plans.AsNoTracking().AsQueryable();
        if (!includeInactive) query = query.Where(p => p.IsActive);
        var plans = await query.OrderBy(p => p.Price).ToListAsync();
        return Ok(plans.Select(Mappers.ToDto).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PlanDto>> Create(SavePlanRequest request)
    {
        var plan = new SubscriptionPlan
        {
            Name = request.Name.Trim(),
            DurationDays = request.DurationDays,
            Price = request.Price,
            IsActive = request.IsActive
        };
        db.Plans.Add(plan);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), plan.ToDto());
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, SavePlanRequest request)
    {
        var plan = await db.Plans.FindAsync(id);
        if (plan is null) return NotFound();

        plan.Name = request.Name.Trim();
        plan.DurationDays = request.DurationDays;
        plan.Price = request.Price;
        plan.IsActive = request.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var plan = await db.Plans.FindAsync(id);
        if (plan is null) return NotFound();
        try
        {
            db.Plans.Remove(plan);
            await db.SaveChangesAsync();
            return NoContent();
        }
        catch (DbUpdateException)
        {
            return BadRequest("Plan already has subscriptions. Deactivate it instead of deleting.");
        }
    }
}
