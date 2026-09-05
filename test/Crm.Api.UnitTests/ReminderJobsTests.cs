using Crm.Api.Models;
using Crm.Api.Services;
using FluentAssertions;

namespace Crm.Api.UnitTests;

public class ReminderJobsTests
{
    private static Person MakeClient()
        => new() { FirstName = "Al", LastName = "Shifa", Phone = "+201000000001" };

    private static PersonContact MakeContact(string phone, string first, string last, short connect)
        => new()
        {
            Phone = phone,
            FirstName = first,
            LastName = last,
            Connect = connect
        };

    [Fact]
    public void Recipients_WhenNoContacts_ReturnsOnlyPrimaryPhone()
    {
        var client = MakeClient();
        var result = ReminderJobs.Recipients(client).ToList();

        result.Should().HaveCount(1);
        result[0].Phone.Should().Be("+201000000001");
        result[0].Name.Should().BeNull(); // no personalised name for the primary phone
    }

    [Fact]
    public void Recipients_AddsOptedInContacts_WithNames()
    {
        var client = MakeClient();
        client.Contacts.Add(MakeContact("+201000000002", "Khaled", "Mansour", connect: 1));
        client.Contacts.Add(MakeContact("+201000000003", "Nour", "El-Sayed", connect: 1));
        client.Contacts.Add(MakeContact("+201000000004", "Skipped", "Person", connect: 0)); // not opted in

        var result = ReminderJobs.Recipients(client).ToList();

        result.Should().HaveCount(3);
        result[1].Phone.Should().Be("+201000000002");
        result[1].Name.Should().Be("Khaled Mansour");
        result[2].Phone.Should().Be("+201000000003");
        result[2].Name.Should().Be("Nour El-Sayed");
    }

    [Fact]
    public void Recipients_IgnoresContacts_WithNoPhone_OrOptOut()
    {
        var client = MakeClient();
        client.Contacts.Add(MakeContact("", "NoPhone", "", connect: 1));       // empty phone
        client.Contacts.Add(MakeContact("+201000000005", "OptOut", "", connect: 0)); // not opted in

        var result = ReminderJobs.Recipients(client).ToList();

        result.Should().HaveCount(1); // only the primary phone
    }

    [Fact]
    public void Recipients_TrimsPhones_AndJoinsFirstLastNames()
    {
        var client = MakeClient();
        client.Contacts.Add(new PersonContact { Phone = "  +201000000006  ", FirstName = "  Mona ", LastName = "  Adel  ", Connect = 1 });

        var result = ReminderJobs.Recipients(client).Single(c => c.Phone == "+201000000006");
        result.Phone.Should().Be("+201000000006");
        result.Name.Should().Be("Mona Adel");
    }
}
