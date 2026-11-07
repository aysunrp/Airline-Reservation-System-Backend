namespace Service.Helpers;

/// <summary>Rows to ignore when checking whether a media relative path is still referenced.</summary>
public sealed record SiteMediaReferenceExclusions(
    int? ExceptCountryId = null,
    int? ExceptCityId = null);
