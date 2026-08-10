using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ReadyToGoTravel.Consumer.Customers;
using ReadyToGoTravel.Consumer.Locale;
using ReadyToGoTravel.Consumer.Persistence;
using ReadyToGoTravel.Consumer.Travellers;
using ReadyToGoTravel.Consumer.Trips;

namespace ReadyToGoTravel.Consumer.Http;

public static class ConsumerEndpoints
{
    public static RouteGroupBuilder MapConsumerEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/locales", () =>
            Results.Ok(new[] { new LocaleResponse("en-AU", "English (Australia)", "AUD") }));
        group.MapGet("/privacy/sensitive-traveller-storage", () =>
            Results.Ok(new SensitiveTravellerStorageResponse(
                Enabled: false,
                Categories: ["dateOfBirth", "identityDocuments"])));

        var protectedGroup = group.MapGroup(string.Empty).RequireAuthorization("consumer");
        protectedGroup.MapGet("/me", GetProfileAsync);
        protectedGroup.MapPut("/me", UpsertProfileAsync);
        protectedGroup.MapPost("/me/close", CloseAccountAsync);
        protectedGroup.MapGet("/trips", ListTripsAsync);
        protectedGroup.MapPost("/trips", CreateTripAsync);
        protectedGroup.MapGet("/trips/{tripId:guid}", GetTripAsync);
        protectedGroup.MapPost("/trips/{tripId:guid}/archive", ArchiveTripAsync);
        protectedGroup.MapGet("/travellers", ListTravellersAsync);
        protectedGroup.MapPost("/travellers", CreateTravellerAsync);
        protectedGroup.MapDelete("/travellers/{travellerId:guid}", RemoveTravellerAsync);

        return group;
    }

    private static async Task<IResult> GetProfileAsync(
        ClaimsPrincipal principal,
        ConsumerDbContext database,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue("sub")!;
        var customer = await database.Customers
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Subject == subject, cancellationToken);

        return customer is null
            ? ConsumerHttpResults.Problem(context, StatusCodes.Status404NotFound, "profile_not_found", "Profile not found")
            : Results.Ok(ToResponse(customer, principal));
    }

    private static async Task<IResult> UpsertProfileAsync(
        UpsertProfileRequest request,
        ClaimsPrincipal principal,
        ConsumerDbContext database,
        TimeProvider timeProvider,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue("sub")!;
        var customer = await database.Customers
            .SingleOrDefaultAsync(item => item.Subject == subject, cancellationToken);

        if (customer is null)
        {
            var creation = Customer.Create(subject, request.PreferredLocale, request.AdultConfirmed, timeProvider);
            if (!creation.IsSuccess)
            {
                return ConsumerHttpResults.Problem(
                    context,
                    StatusCodes.Status400BadRequest,
                    creation.ErrorCode!,
                    "Profile could not be created");
            }

            customer = creation.Value!;
            database.Customers.Add(customer);
        }
        else
        {
            if (customer.Status is not CustomerStatus.Active)
            {
                return ConsumerHttpResults.Problem(
                    context,
                    StatusCodes.Status403Forbidden,
                    "profile_inactive",
                    "Profile is not active");
            }

            var update = customer.UpdateLocale(request.PreferredLocale, timeProvider);
            if (!update.IsSuccess)
            {
                return ConsumerHttpResults.Problem(
                    context,
                    StatusCodes.Status400BadRequest,
                    update.ErrorCode!,
                    "Profile could not be updated");
            }
        }

        await database.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(customer, principal));
    }

    private static async Task<IResult> CloseAccountAsync(
        ClaimsPrincipal principal,
        ConsumerDbContext database,
        TimeProvider timeProvider,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue("sub")!;
        var customer = await database.Customers.SingleOrDefaultAsync(item => item.Subject == subject, cancellationToken);
        if (customer is null)
        {
            return ConsumerHttpResults.Problem(context, StatusCodes.Status404NotFound, "profile_not_found", "Profile not found");
        }

        // Idempotent: closing an already-closed account is a no-op 204, not an error, matching
        // Customer.Close's own idempotent behaviour.
        customer.Close(timeProvider);
        await database.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> ListTripsAsync(
        ClaimsPrincipal principal,
        ConsumerDbContext database,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var customer = await FindActiveCustomerAsync(principal, database, cancellationToken);
        if (customer is null)
        {
            return ProfileRequired(context);
        }

        var trips = await database.Trips
            .AsNoTracking()
            .Where(trip => trip.CustomerId == customer.Id)
            .OrderBy(trip => trip.StartDate)
            .ThenBy(trip => trip.CreatedAt)
            .Take(100)
            .Select(trip => ToResponse(trip))
            .ToArrayAsync(cancellationToken);
        return Results.Ok(trips);
    }

    private static async Task<IResult> CreateTripAsync(
        CreateTripRequest request,
        ClaimsPrincipal principal,
        ConsumerDbContext database,
        TimeProvider timeProvider,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var customer = await FindActiveCustomerAsync(principal, database, cancellationToken);
        if (customer is null)
        {
            return ProfileRequired(context);
        }

        var creation = Trip.Create(
            customer.Id,
            request.Title,
            request.PrimaryDestination,
            request.StartDate,
            request.EndDate,
            timeProvider);
        if (!creation.IsSuccess)
        {
            return ConsumerHttpResults.Problem(
                context,
                StatusCodes.Status400BadRequest,
                creation.ErrorCode!,
                "Trip could not be created");
        }

        database.Trips.Add(creation.Value!);
        await database.SaveChangesAsync(cancellationToken);
        var response = ToResponse(creation.Value!);
        return Results.Created($"/api/v1/trips/{response.Id}", response);
    }

    private static async Task<IResult> GetTripAsync(
        Guid tripId,
        ClaimsPrincipal principal,
        ConsumerDbContext database,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var customer = await FindActiveCustomerAsync(principal, database, cancellationToken);
        if (customer is null)
        {
            return ProfileRequired(context);
        }

        var trip = await database.Trips
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == tripId && item.CustomerId == customer.Id,
                cancellationToken);
        return trip is null
            ? ConsumerHttpResults.Problem(context, StatusCodes.Status404NotFound, "trip_not_found", "Trip not found")
            : Results.Ok(ToResponse(trip));
    }

    private static async Task<IResult> ArchiveTripAsync(
        Guid tripId,
        ClaimsPrincipal principal,
        ConsumerDbContext database,
        TimeProvider timeProvider,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var customer = await FindActiveCustomerAsync(principal, database, cancellationToken);
        if (customer is null)
        {
            return ProfileRequired(context);
        }

        var trip = await database.Trips.SingleOrDefaultAsync(
            item => item.Id == tripId && item.CustomerId == customer.Id,
            cancellationToken);
        if (trip is null)
        {
            return ConsumerHttpResults.Problem(context, StatusCodes.Status404NotFound, "trip_not_found", "Trip not found");
        }

        trip.Archive(timeProvider);
        await database.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(trip));
    }

    private static async Task<IResult> ListTravellersAsync(
        ClaimsPrincipal principal,
        ConsumerDbContext database,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var customer = await FindActiveCustomerAsync(principal, database, cancellationToken);
        if (customer is null)
        {
            return ProfileRequired(context);
        }

        var travellers = await database.Travellers
            .AsNoTracking()
            .Where(traveller => traveller.CustomerId == customer.Id)
            .OrderBy(traveller => traveller.FamilyName)
            .ThenBy(traveller => traveller.GivenName)
            .Take(100)
            .Select(traveller => ToResponse(traveller))
            .ToArrayAsync(cancellationToken);
        return Results.Ok(travellers);
    }

    private static async Task<IResult> CreateTravellerAsync(
        CreateTravellerRequest request,
        ClaimsPrincipal principal,
        ConsumerDbContext database,
        TimeProvider timeProvider,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var customer = await FindActiveCustomerAsync(principal, database, cancellationToken);
        if (customer is null)
        {
            return ProfileRequired(context);
        }

        var creation = Traveller.Create(
            customer.Id,
            request.GivenName,
            request.FamilyName,
            request.RelationshipLabel,
            request.IsMinor,
            request.GuardianAuthorityConfirmed,
            timeProvider);
        if (!creation.IsSuccess)
        {
            return ConsumerHttpResults.Problem(
                context,
                StatusCodes.Status400BadRequest,
                creation.ErrorCode!,
                "Traveller could not be created");
        }

        database.Travellers.Add(creation.Value!);
        await database.SaveChangesAsync(cancellationToken);
        var response = ToResponse(creation.Value!);
        return Results.Created($"/api/v1/travellers/{response.Id}", response);
    }

    private static async Task<IResult> RemoveTravellerAsync(
        Guid travellerId,
        ClaimsPrincipal principal,
        ConsumerDbContext database,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var customer = await FindActiveCustomerAsync(principal, database, cancellationToken);
        if (customer is null)
        {
            return ProfileRequired(context);
        }

        var traveller = await database.Travellers.SingleOrDefaultAsync(
            item => item.Id == travellerId && item.CustomerId == customer.Id,
            cancellationToken);
        if (traveller is not null)
        {
            database.Travellers.Remove(traveller);
            await database.SaveChangesAsync(cancellationToken);
        }

        return Results.NoContent();
    }

    private static Task<Customer?> FindActiveCustomerAsync(
        ClaimsPrincipal principal,
        ConsumerDbContext database,
        CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue("sub")!;
        return database.Customers.SingleOrDefaultAsync(
            customer => customer.Subject == subject && customer.Status == CustomerStatus.Active,
            cancellationToken);
    }

    private static IResult ProfileRequired(HttpContext context) =>
        ConsumerHttpResults.Problem(
            context,
            StatusCodes.Status409Conflict,
            "profile_required",
            "An active customer profile is required");

    private static CustomerResponse ToResponse(Customer customer, ClaimsPrincipal principal) =>
        new(
            customer.Id,
            customer.Status.ToString(),
            customer.PreferredLocale,
            customer.DisplayCurrency,
            customer.AdultConfirmedAt,
            principal.FindFirstValue("email"));

    private static TripResponse ToResponse(Trip trip) =>
        new(
            trip.Id,
            trip.Title,
            trip.PrimaryDestination,
            trip.StartDate,
            trip.EndDate,
            trip.Status.ToString(),
            trip.CreatedAt,
            trip.UpdatedAt);

    private static TravellerResponse ToResponse(Traveller traveller) =>
        new(
            traveller.Id,
            traveller.GivenName,
            traveller.FamilyName,
            traveller.RelationshipLabel,
            traveller.IsMinor,
            traveller.GuardianAuthorityConfirmedAt,
            traveller.CreatedAt);
}
