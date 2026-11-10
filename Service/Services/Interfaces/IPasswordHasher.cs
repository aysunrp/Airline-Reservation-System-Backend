namespace Service.Services.Interfaces;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);

    /// <summary>True when a valid hash was made with fewer iterations than new hashes use, so it should be replaced.</summary>
    bool NeedsRehash(string passwordHash);
}
