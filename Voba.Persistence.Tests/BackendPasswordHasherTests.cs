using Voba.Services;
using Xunit;

namespace Voba.Persistence.Tests;

public sealed class BackendPasswordHasherTests
{
    [Fact]
    public void Verifies_existing_bcrypt_hashes()
    {
        var existingHash = BCrypt.Net.BCrypt.HashPassword("existing password", BCrypt.Net.BCrypt.GenerateSalt());
        var hasher = new BackendPasswordHasher();

        Assert.True(hasher.Verify("existing password", existingHash));
        Assert.False(hasher.Verify("wrong password", existingHash));
    }
}
