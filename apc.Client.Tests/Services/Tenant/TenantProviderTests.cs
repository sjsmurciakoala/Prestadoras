using System;
using System.Security.Claims;
using System.Threading.Tasks;
using apc.Client.Services.Tenant;
using Microsoft.AspNetCore.Components.Authorization;
using SIAD.Core.Constants;
using Xunit;

namespace apc.Client.Tests.Services.Tenant;

public class TenantProviderTests
{
    [Fact]
    public async Task GetCompanyIdAsync_ReturnsValueFromClaim()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(TenantClaimTypes.CompanyId, "25")
        }, authenticationType: "test"));
        var provider = new TenantProvider(new TestAuthenticationStateProvider(principal));

        // Act
        var result = await provider.GetCompanyIdAsync();

        // Assert
        Assert.Equal(25, result);
    }

    [Fact]
    public async Task GetCompanyIdAsync_WithoutClaim_ThrowsInvalidOperationException()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "test"));
        var provider = new TenantProvider(new TestAuthenticationStateProvider(principal));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetCompanyIdAsync().AsTask());
    }

    private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly AuthenticationState state;

        public TestAuthenticationStateProvider(ClaimsPrincipal principal)
        {
            state = new AuthenticationState(principal);
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(state);
    }
}
