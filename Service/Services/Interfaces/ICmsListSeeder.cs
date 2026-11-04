namespace Service.Services.Interfaces;

public interface ICmsListSeeder
{
    /// <summary>Adds the home cards and the support questions the first time (returns how many rows were added).</summary>
    Task<int> SeedAsync(CancellationToken cancellationToken = default);
}
