using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace tests;



// Tester GraphQL-laget gennem HTTP (POST /graphql), præcis som en frontend ville gøre.
// Kræver ikke RabbitMQ: GraphQL bruger kun IProfileService.
[Collection("Profile host")]
public class GraphQLTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public GraphQLTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "test-only-signing-key-at-least-32-bytes!");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "bizcord-auth");
        Environment.SetEnvironmentVariable("Jwt__Audience", "internal-api");
        _factory = factory;
    }

    private async Task<JsonElement> PostAsync(string query, object? variables = null)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/graphql", new { query, variables });
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task CreateProfile_ThenQueryById_ReturnsOnlyRequestedFields()
    {
        var created = await PostAsync(
            "mutation($n: String!) { createProfile(input: { displayName: $n }) { id displayName } }",
            new { n = "GraphQL Tester" });

        var id = created.GetProperty("data").GetProperty("createProfile").GetProperty("id").GetString()!;

        // Klienten beder kun om displayName -> får kun displayName (ingen over-fetching)
        var result = await PostAsync(
            "query($id: String!) { profileById(id: $id) { displayName } }",
            new { id });

        var profile = result.GetProperty("data").GetProperty("profileById");
        Assert.Equal("GraphQL Tester", profile.GetProperty("displayName").GetString());
        Assert.False(profile.TryGetProperty("isBanned", out _));
    }

    [Fact]
    public async Task Profiles_ReturnsCreatedProfile()
    {
        await PostAsync(
            "mutation { createProfile(input: { displayName: \"List Me\" }) { id } }");

        var result = await PostAsync("{ profiles { displayName isBanned } }");

        var names = result.GetProperty("data").GetProperty("profiles")
            .EnumerateArray()
            .Select(p => p.GetProperty("displayName").GetString());

        Assert.Contains("List Me", names);
    }

    [Fact]
    public async Task ProfileById_UnknownId_ReturnsNull()
    {
        var result = await PostAsync(
            "{ profileById(id: \"does-not-exist\") { displayName } }");

        Assert.Equal(JsonValueKind.Null,
            result.GetProperty("data").GetProperty("profileById").ValueKind);
    }

    [Fact]
    public async Task CreateProfile_EmptyName_ReturnsGraphQLError()
    {
        var result = await PostAsync(
            "mutation { createProfile(input: { displayName: \"  \" }) { id } }");

        var error = result.GetProperty("errors")[0];
        Assert.Equal("DISPLAYNAME_REQUIRED",
            error.GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateAndDelete_Profile_Works()
    {
        var created = await PostAsync(
            "mutation { createProfile(input: { displayName: \"Old\" }) { id } }");
        var id = created.GetProperty("data").GetProperty("createProfile").GetProperty("id").GetString()!;

        var updated = await PostAsync(
            "mutation($id: String!) { updateProfile(input: { id: $id, displayName: \"New\" }) { displayName } }",
            new { id });
        Assert.Equal("New",
            updated.GetProperty("data").GetProperty("updateProfile").GetProperty("displayName").GetString());

        var deleted = await PostAsync(
            "mutation($id: String!) { deleteProfile(id: $id) }", new { id });
        Assert.True(deleted.GetProperty("data").GetProperty("deleteProfile").GetBoolean());
    }
}
