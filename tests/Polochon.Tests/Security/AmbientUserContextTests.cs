using System.Security.Claims;
using Polochon.Security;
using Xunit;

namespace Polochon.Tests.Security
{
    /// <summary>
    /// Tests for <see cref="AmbientUserContext"/>: the principal set at an entry point flows
    /// through awaits and is restored when its scope ends.
    /// </summary>
    public sealed class AmbientUserContextTests
    {
        private static ClaimsPrincipal CreatePrincipal(string name)
            => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "Test"));

        /// <summary>No principal is set by default.</summary>
        [Fact(DisplayName = "Current is null when no principal was set")]
        public void CurrentWhenNothingSetIsNull()
        {
            Assert.Null(AmbientUserContext.Current);
        }

        /// <summary>Disposing the scope restores the absence of principal.</summary>
        [Fact(DisplayName = "Use sets the principal until the scope is disposed")]
        public void UseSetsPrincipalUntilDisposed()
        {
            var principal = CreatePrincipal("alice");

            using (AmbientUserContext.Use(principal))
            {
                Assert.Same(principal, AmbientUserContext.Current);
            }

            Assert.Null(AmbientUserContext.Current);
        }

        /// <summary>Nested scopes restore the outer principal, not null.</summary>
        [Fact(DisplayName = "Nested Use restores the outer principal when disposed")]
        public void NestedUseRestoresOuterPrincipal()
        {
            var outer = CreatePrincipal("outer");
            var inner = CreatePrincipal("inner");

            using (AmbientUserContext.Use(outer))
            {
                using (AmbientUserContext.Use(inner))
                {
                    Assert.Same(inner, AmbientUserContext.Current);
                }

                Assert.Same(outer, AmbientUserContext.Current);
            }
        }

        /// <summary>Disposing a scope twice does not clobber a principal set afterwards.</summary>
        [Fact(DisplayName = "Disposing a scope twice is a no-op")]
        public void DisposeTwiceIsNoOp()
        {
            var first = CreatePrincipal("first");
            var second = CreatePrincipal("second");

            var scope = AmbientUserContext.Use(first);
            scope.Dispose();

            using (AmbientUserContext.Use(second))
            {
                scope.Dispose();
                Assert.Same(second, AmbientUserContext.Current);
            }
        }

        /// <summary>The principal flows into awaited code and child tasks.</summary>
        [Fact(DisplayName = "Principal flows across awaits and into child tasks")]
        public async Task PrincipalFlowsAcrossAwaits()
        {
            var principal = CreatePrincipal("alice");

            using (AmbientUserContext.Use(principal))
            {
                await Task.Yield();
                var seenInChild = await Task.Run(() => AmbientUserContext.Current);

                Assert.Same(principal, AmbientUserContext.Current);
                Assert.Same(principal, seenInChild);
            }
        }

        /// <summary>A principal set in a child flow does not leak into its parent.</summary>
        [Fact(DisplayName = "Principal set in a child task does not leak to the caller")]
        public async Task PrincipalSetInChildDoesNotLeak()
        {
            await Task.Run(() => AmbientUserContext.Use(CreatePrincipal("leak")));

            Assert.Null(AmbientUserContext.Current);
        }

        /// <summary>A null principal is rejected.</summary>
        [Fact(DisplayName = "Use throws when principal is null")]
        public void UseWhenPrincipalNullThrows()
        {
            _ = Assert.Throws<ArgumentNullException>(() => AmbientUserContext.Use(null!));
        }
    }
}
