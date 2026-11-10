using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repository.Data;
using static AirlineReservationSystem.Tests.ApiTestHelpers;

namespace AirlineReservationSystem.Tests;

/// <summary>
/// Creating a support request failed with a bare server error on a database that did not have the support tables (the migration AddSupportTickets
/// had not been applied). The failing request was POST /api/support/tickets, at the first read of SupportTickets.
/// </summary>
public class SupportSetupTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SupportSetupTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WithoutTheSupportTables_EveryEndpointSaysTheSetupIsMissing_NotAGenericError()
    {
        var email = UniqueEmail("setup");
        var customer = _factory.CreateClient();
        customer.WithToken((await customer.RegisterAsync(email)).AccessToken);
        var admin = await _factory.CreateAdminClientAsync();

        // The state of a database created before the migration AddSupportTickets.
        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlRawAsync("DROP TABLE SupportMessages; DROP TABLE SupportTickets;");
        }

        var responses = new[]
        {
            await customer.PostAsJsonAsync("/api/support/tickets", new { category = "Booking", subject = "Seat change", message = "I need help with my seat selection, please." }, Json),
            await customer.GetAsync("/api/support/tickets"),
            await customer.GetAsync("/api/support/tickets/1"),
            await admin.GetAsync("/api/admin/support/tickets"),
            await admin.GetAsync("/api/admin/support/tickets/1")
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            var body = await response.ReadJsonAsync();
            Assert.Equal("database_setup_required", body.GetProperty("code").GetString());
            Assert.Contains("migration", body.GetProperty("detail").GetString());
            // Nothing of the SQL error reaches the page.
            Assert.DoesNotContain("SupportTickets", body.GetRawText());
            Assert.DoesNotContain("SqlException", body.GetRawText());
        }

        // The rest of the site is not affected.
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync("/api/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync("/api/support/info")).StatusCode);
    }
}
