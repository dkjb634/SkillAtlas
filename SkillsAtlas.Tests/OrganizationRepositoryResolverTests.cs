using System.Net;
using System.Text;
using SkillsAtlas;
using Xunit;

namespace SkillsAtlas.Tests;

public sealed class OrganizationRepositoryResolverTests
{
    [Theory]
    [InlineData("https://github.com/dotnet", "dotnet")]
    [InlineData("https://github.com/dotnet/", "dotnet")]
    public void TryGetGitHubOrganization_ExtractsOrganizationName(string url, string expected)
    {
        Assert.True(OrganizationRepositoryResolver.TryGetGitHubOrganization(url, out var organization));
        Assert.Equal(expected, organization);
    }

    [Theory]
    [InlineData("https://github.com/dotnet/runtime")]
    [InlineData("https://gitlab.com/dotnet")]
    public void TryGetGitHubOrganization_RejectsNonOrganizationUrls(string url)
    {
        Assert.False(OrganizationRepositoryResolver.TryGetGitHubOrganization(url, out _));
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNonArchivedRepositoriesAndRequestsAllPages()
    {
        var handler = new StubHandler(request =>
        {
            var json = request.RequestUri!.Query.Contains("page=1")
                ? "[{\"clone_url\":\"https://github.com/acme/one.git\",\"archived\":false}]"
                : "[]";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });
        using var client = new HttpClient(handler);
        var resolver = new OrganizationRepositoryResolver(client);

        var repositories = await resolver.ResolveAsync("https://github.com/acme");

        Assert.Equal(["https://github.com/acme/one.git"], repositories);
        Assert.Equal(2, handler.Requests.Count);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responseFactory(request));
        }
    }
}
