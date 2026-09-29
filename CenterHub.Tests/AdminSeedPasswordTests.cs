using CenterHub.Data;
using Xunit;

namespace CenterHub.Tests;

public class AdminSeedPasswordTests
{
    private const string Allowed = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%^&*";

    [Fact]
    public void Generate_DefaultLength_Returns12Characters()
    {
        var password = AdminSeedPassword.Generate();

        Assert.Equal(12, password.Length);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(20)]
    public void Generate_CustomLength_ReturnsRequestedLength(int length)
    {
        var password = AdminSeedPassword.Generate(length);

        Assert.Equal(length, password.Length);
    }

    [Fact]
    public void Generate_ContainsOnlyUnambiguousAllowedCharacters()
    {
        var password = AdminSeedPassword.Generate(50);

        Assert.All(password, c => Assert.Contains(c, Allowed));
    }

    [Fact]
    public void Generate_CalledTwice_ProducesDifferentValues()
    {
        var first = AdminSeedPassword.Generate();
        var second = AdminSeedPassword.Generate();

        Assert.NotEqual(first, second);
    }
}
