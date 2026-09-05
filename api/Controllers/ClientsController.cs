using Crm.Api.Data;
using Crm.Api.Dtos;
using Crm.Api.Models;
using Crm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize]
public class ClientsController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<string> ValidTypes = ["Pharmacy", "GiftShop", "DoctorClinic", "Hospital", "Other"];
    private static readonly HashSet<string> ValidStatuses = ["Potential", "Contacted", "Interested", "NotInterested", "Subscribed"];

    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientListItemDto>>> GetAll(
        [FromQuery] string? q,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Persons
            .AsNoTracking()
            .Include(p => p.CrmExtension)
            .Where(p => p.PersonType == 12)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(p =>
                EF.Functions.ILike(p.FirstName, $"%{term}%") ||
                EF.Functions.ILike(p.LastName, $"%{term}%") ||
                EF.Functions.ILike(p.Phone, $"%{term}%") ||
                EF.Functions.ILike(p.Email, $"%{term}%") ||
                EF.Functions.ILike($"{p.FirstName} {p.LastName}", $"%{term}%"));
        }
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(p => p.CrmExtension != null && p.CrmExtension.ClientType == type);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.CrmExtension != null && p.CrmExtension.Status == status);

        var total = await query.CountAsync();
        var persons = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var ids = persons.Select(p => p.Id).ToList();
        var clientSubs = await db.Subscriptions
            .AsNoTracking().Include(sub => sub.Plan)
            .Where(sub => ids.Contains(sub.ClientId))
            .ToListAsync();
        var latestSubs = clientSubs
            .GroupBy(sub => sub.ClientId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(sub => sub.ExpiryDate).First());

        var items = persons.Select(p =>
        {
            latestSubs.TryGetValue(p.Id, out var sub);
            return new ClientListItemDto(
                p.Id,
                $"{p.FirstName} {p.LastName}".Trim(),
                p.FirstName,
                p.Phone,
                p.Email,
                null,
                p.CrmExtension?.ClientType ?? "Unknown",
                p.CrmExtension?.Status ?? "Unknown",
                sub?.Id, sub?.Plan.Name, sub?.ExpiryDate, sub?.PaymentStatus.ToString());
        }).ToList();

        return Ok(new PagedResult<ClientListItemDto>(items, total, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientDetailDto>> GetById(int id)
    {
        var person = await db.Persons.AsNoTracking()
            .Include(p => p.CrmExtension)
            .FirstOrDefaultAsync(p => p.Id == id && p.PersonType == 12);
        if (person is null) return NotFound();

        var contacts = await db.PersonContacts.AsNoTracking()
            .Where(x => x.PersonId == id).OrderBy(x => x.FirstName).ToListAsync();
        var subs = await db.Subscriptions.AsNoTracking()
            .Include(x => x.Plan)
            .Where(x => x.ClientId == id).OrderByDescending(x => x.StartDate).ToListAsync();
        var interactions = await db.Interactions.AsNoTracking()
            .Where(x => x.ClientId == id).OrderByDescending(x => x.CreatedAt).Take(50).ToListAsync();
        var tickets = await db.Tickets.AsNoTracking()
            .Where(x => x.ClientId == id).OrderByDescending(x => x.UpdatedAt).ToListAsync();
        var followUps = await db.FollowUps.AsNoTracking()
            .Where(x => x.ClientId == id).OrderByDescending(x => x.ScheduledAt).Take(50).ToListAsync();

        var contactDtos = contacts.Select(c => new ClientContactDto(
            c.Id, id, $"{c.FirstName} {c.LastName}".Trim(), c.Phone, c.Email, c.JobPos, c.Connect == 1)).ToList();

        var paymentDtos = subs.Where(s => s.PaidAt is not null).OrderByDescending(s => s.PaidAt)
            .Select(s => new PaymentDto(s.Id, s.Plan.Name, s.Plan.Cycle.ToString(), s.StartDate, s.ExpiryDate, s.Price, s.PaymentMethod, s.PaidAt!.Value, s.LicenseKey)).ToList();

        var subDtos = subs.Select(s => new SubscriptionDto(
            s.Id, s.ClientId, "", "", s.PlanId, s.Plan.Name, s.Plan.Cycle.ToString(),
            s.StartDate, s.ExpiryDate, s.Price, s.PaymentStatus.ToString(),
            s.PaymentMethod, s.PaidAt, s.LicenseKey, s.LicenseKeyIssuedAt, s.Notes, s.CreatedAt)).ToList();

        var interactionDtos = interactions.Select(i => new InteractionDto(
            i.Id, i.ClientId, "", i.Type.ToString(), i.Outcome.ToString(), i.Notes, i.NextFollowUpAt,
            i.UserId, "", i.CreatedAt)).ToList();

        var ticketDtos = tickets.Select(t => new TicketDto(
            t.Id, t.ClientId, "", t.Title, t.Description, t.Priority.ToString(), t.Status.ToString(),
            t.AssignedToId, "", "", t.CreatedAt, t.UpdatedAt, t.ResolvedAt, t.ResolvedVersion, t.Comments.Count)).ToList();

        var followUpDtos = followUps.Select(f => new FollowUpDto(
            f.Id, f.ClientId, "", f.Title, f.Description, f.Type.ToString(), f.TicketId, "", f.ScheduledAt,
            f.Status.ToString(), f.AssignedToId, "", f.ReminderSentAt, f.CreatedAt)).ToList();

        return Ok(new ClientDetailDto(
            person.Id,
            $"{person.FirstName} {person.LastName}".Trim(),
            person.FirstName,
            person.Phone,
            person.Email,
            person.Street,
            null,
            person.CrmExtension?.ClientType ?? "Unknown",
            person.CrmExtension?.Status ?? "Unknown",
            person.Remarks,
            person.CreatedAt,
            contactDtos, paymentDtos, subDtos, interactionDtos, ticketDtos, followUpDtos));
    }

    [HttpGet("{id:int}/contacts")]
    public async Task<ActionResult<IReadOnlyList<ClientContactDto>>> GetContacts(int id)
    {
        if (!await db.Persons.AnyAsync(p => p.Id == id && p.PersonType == 12)) return NotFound();
        var contacts = await db.PersonContacts.AsNoTracking()
            .Where(x => x.PersonId == id).OrderBy(x => x.FirstName).ToListAsync();
        return Ok(contacts.Select(c => new ClientContactDto(
            c.Id, id, $"{c.FirstName} {c.LastName}".Trim(), c.Phone, c.Email, c.JobPos, c.Connect == 1)).ToList());
    }

    [HttpPost("{id:int}/contacts")]
    public async Task<ActionResult<ClientContactDto>> AddContact(int id, SaveClientContactRequest request)
    {
        if (!await db.Persons.AnyAsync(p => p.Id == id && p.PersonType == 12)) return NotFound();

        var contact = new PersonContact
        {
            PersonId = id,
            Seq = (short)(db.PersonContacts.Count(x => x.PersonId == id) + 1),
            FirstName = request.Name.Split(' ')[0],
            LastName = string.Join(' ', request.Name.Split(' ').Skip(1)),
            Phone = request.Phone,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email,
            JobPos = request.Notes,
            Connect = request.AllowWhatsApp ? (short)1 : (short)0,
            CreatedBy = User.GetUserId(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.PersonContacts.Add(contact);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetContacts), new { id }, new ClientContactDto(
            contact.Id, id, $"{contact.FirstName} {contact.LastName}".Trim(), contact.Phone, contact.Email, contact.JobPos, contact.Connect == 1));
    }

    [HttpPut("{id:int}/contacts/{contactId:int}")]
    public async Task<IActionResult> UpdateContact(int id, int contactId, SaveClientContactRequest request)
    {
        var contact = await db.PersonContacts.FirstOrDefaultAsync(x => x.Id == contactId && x.PersonId == id);
        if (contact is null) return NotFound();

        contact.FirstName = request.Name.Split(' ')[0];
        contact.LastName = string.Join(' ', request.Name.Split(' ').Skip(1));
        contact.Phone = request.Phone;
        contact.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email;
        contact.JobPos = request.Notes;
        contact.Connect = request.AllowWhatsApp ? (short)1 : (short)0;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}/contacts/{contactId:int}")]
    public async Task<IActionResult> DeleteContact(int id, int contactId)
    {
        var contact = await db.PersonContacts.FirstOrDefaultAsync(x => x.Id == contactId && x.PersonId == id);
        if (contact is null) return NotFound();
        db.PersonContacts.Remove(contact);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateClientRequest request)
    {
        if (!ValidTypes.Contains(request.Type)) return BadRequest($"Unknown client type '{request.Type}'. Allowed: {string.Join(", ", ValidTypes)}.");
        if (!ValidStatuses.Contains(request.Status)) return BadRequest($"Unknown status '{request.Status}'. Allowed: {string.Join(", ", ValidStatuses)}.");

        var person = new Person
        {
            FirstName = request.ContactPerson.Split(' ')[0],
            LastName = string.Join(' ', request.ContactPerson.Split(' ').Skip(1)),
            Phone = request.Phone,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email,
            Street = request.Address,
            Status = "1",
            PersonType = 12,
            ProfileId = 1, // default client profile (phcyid=1 in ew_profile)
            CreatedBy = User.GetUserId(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Persons.Add(person);
        await db.SaveChangesAsync();

        db.CrmClientExtensions.Add(new CrmClientExtension
        {
            PersonId = person.Id,
            ClientType = request.Type,
            Status = request.Status,
            CreatedBy = User.GetUserId(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        var clientId = person.Id;
        if (request.FirstContactAt is { } when)
        {
            db.FollowUps.Add(new FollowUp
            {
                ClientId = clientId,
                Title = $"First contact - {request.Name}",
                Description = "Initial outreach scheduled at client creation.",
                ScheduledAt = when,
                AssignedToId = User.GetUserId(),
                CreatedById = User.GetUserId()
            });
            await db.SaveChangesAsync();
        }

        return CreatedAtAction(nameof(GetById), new { id = clientId }, new { id = clientId });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateClientRequest request)
    {
        var person = await db.Persons.FirstOrDefaultAsync(p => p.Id == id && p.PersonType == 12);
        if (person is null) return NotFound();

        if (!ValidTypes.Contains(request.Type)) return BadRequest($"Unknown client type '{request.Type}'.");
        if (!ValidStatuses.Contains(request.Status)) return BadRequest($"Unknown status '{request.Status}'.");

        person.FirstName = request.ContactPerson.Split(' ')[0];
        person.LastName = string.Join(' ', request.ContactPerson.Split(' ').Skip(1));
        person.Phone = request.Phone;
        person.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email;
        person.Street = request.Address;
        person.Status = "1";
        await db.SaveChangesAsync();

        var ext = await db.CrmClientExtensions.FirstOrDefaultAsync(x => x.PersonId == id);
        if (ext is not null)
        {
            ext.ClientType = request.Type;
            ext.Status = request.Status;
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, ClientStatusRequest request)
    {
        if (!ValidStatuses.Contains(request.Status)) return BadRequest($"Unknown status '{request.Status}'.");

        var ext = await db.CrmClientExtensions.FirstOrDefaultAsync(x => x.PersonId == id);
        if (ext is null) return NotFound();

        ext.Status = request.Status;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var person = await db.Persons.FirstOrDefaultAsync(p => p.Id == id && p.PersonType == 12);
        if (person is null) return NotFound();
        db.Persons.Remove(person);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
