using Geofarmland.Server.Domain.Entities;

namespace Geofarmland.Server.Features.Enterprise;

public static class EnterpriseMapping
{
    public static EnterpriseOrganizationDto ToDto(this OrganizationAccessInfo access) => new(
        access.Organization.Id,
        access.Organization.Name,
        access.Organization.Slug,
        access.Organization.Country,
        access.Organization.CurrencyCode,
        access.Organization.TimeZone,
        access.Role.ToString());

    public static string ToLabel(this object value) => value.ToString() ?? string.Empty;
}
