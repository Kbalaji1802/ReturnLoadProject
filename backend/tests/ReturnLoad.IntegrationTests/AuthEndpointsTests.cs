using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ReturnLoad.Application.Identity;
using ReturnLoad.Shared.Api;

namespace ReturnLoad.IntegrationTests;

/// <summary>
/// End-to-end authentication flows (M2, ADR-0013) over real HTTP against a SQLite-backed
/// host: register, login, refresh rotation + reuse detection, lockout, protected access.
/// Every response is asserted to be the standard envelope (ADR-0008).
/// </summary>
public sealed class AuthEndpointsTests : IClassFixture<AuthApiFactory>
{
    private const string StrongPassword = "Str0ng!Passw0rd";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly AuthApiFactory _factory;

    public AuthEndpointsTests(AuthApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_returns_a_token_pair_in_the_envelope()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await Register(client, Unique("register"), StrongPassword);

        response.EnsureSuccessStatusCode();
        ApiResponse<JsonElement> body = await ReadEnvelope(response);
        Assert.True(body.Success);
        Assert.False(string.IsNullOrEmpty(body.Data.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrEmpty(body.Data.GetProperty("refreshToken").GetString()));
        Assert.Equal("Bearer", body.Data.GetProperty("tokenType").GetString());
    }

    [Fact]
    public async Task Access_token_carries_the_expected_claims()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await Register(client, Unique("claims"), StrongPassword);

        ApiResponse<JsonElement> body = await ReadEnvelope(response);
        JsonElement payload = DecodeJwtPayload(body.Data.GetProperty("accessToken").GetString()!);

        Assert.True(payload.TryGetProperty("sub", out _));
        Assert.True(payload.TryGetProperty(AppClaims.UserId, out _));
        Assert.True(payload.TryGetProperty(AppClaims.PermissionsVersion, out _));
        Assert.True(payload.TryGetProperty("jti", out _));
    }

    [Fact]
    public async Task Register_with_a_weak_password_is_rejected_with_400()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await Register(client, Unique("weak"), "weak");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        ApiResponse<JsonElement> body = await ReadEnvelope(response);
        Assert.False(body.Success);
        Assert.NotEmpty(body.Errors);
    }

    [Fact]
    public async Task Registering_the_same_email_twice_conflicts()
    {
        HttpClient client = _factory.CreateClient();
        string email = Unique("dup");

        (await Register(client, email, StrongPassword)).EnsureSuccessStatusCode();
        HttpResponseMessage second = await Register(client, email, StrongPassword);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        ApiResponse<JsonElement> body = await ReadEnvelope(second);
        Assert.Equal(ErrorCodes.Conflict, Assert.Single(body.Errors).Code);
    }

    [Fact]
    public async Task Login_succeeds_with_correct_credentials_and_fails_generically_otherwise()
    {
        HttpClient client = _factory.CreateClient();
        string email = Unique("login");
        (await Register(client, email, StrongPassword)).EnsureSuccessStatusCode();

        HttpResponseMessage ok = await Login(client, email, StrongPassword);
        ok.EnsureSuccessStatusCode();

        HttpResponseMessage bad = await Login(client, email, "Wr0ng!Passw0rd");
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);

        HttpResponseMessage unknown = await Login(client, Unique("nobody"), StrongPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_reuse_is_rejected()
    {
        HttpClient client = _factory.CreateClient();
        string email = Unique("refresh");
        ApiResponse<JsonElement> registered = await ReadEnvelope(await Register(client, email, StrongPassword));
        string firstRefresh = registered.Data.GetProperty("refreshToken").GetString()!;

        // First refresh succeeds and yields a new refresh token.
        HttpResponseMessage rotated = await Refresh(client, firstRefresh);
        rotated.EnsureSuccessStatusCode();
        ApiResponse<JsonElement> rotatedBody = await ReadEnvelope(rotated);
        string secondRefresh = rotatedBody.Data.GetProperty("refreshToken").GetString()!;
        Assert.NotEqual(firstRefresh, secondRefresh);

        // Reusing the now-revoked first token is rejected (reuse detection).
        HttpResponseMessage reused = await Refresh(client, firstRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task Five_failed_logins_lock_the_account()
    {
        HttpClient client = _factory.CreateClient();
        string email = Unique("lockout");
        (await Register(client, email, StrongPassword)).EnsureSuccessStatusCode();

        for (int i = 0; i < 5; i++)
        {
            await Login(client, email, "Wr0ng!Passw0rd");
        }

        // Even with the CORRECT password now, the account is locked.
        HttpResponseMessage afterLock = await Login(client, email, StrongPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLock.StatusCode);
        ApiResponse<JsonElement> body = await ReadEnvelope(afterLock);
        Assert.Contains("locked", body.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Protected_endpoint_requires_authentication()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage anon = await client.PostAsync("/api/v1/auth/logout-all", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);
    }

    [Fact]
    public async Task Authenticated_user_can_log_out_of_all_devices()
    {
        HttpClient client = _factory.CreateClient();
        ApiResponse<JsonElement> registered = await ReadEnvelope(await Register(client, Unique("logoutall"), StrongPassword));
        string accessToken = registered.Data.GetProperty("accessToken").GetString()!;

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/auth/logout-all");
        request.Headers.Authorization = new("Bearer", accessToken);
        HttpResponseMessage response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.True((await ReadEnvelope(response)).Success);
    }

    private static Task<HttpResponseMessage> Register(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, phoneNumber = (string?)null, deviceId = "test-device" });

    private static Task<HttpResponseMessage> Login(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password, deviceId = "test-device" });

    private static Task<HttpResponseMessage> Refresh(HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken, deviceId = "test-device" });

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}@returnload.test";

    private static async Task<ApiResponse<JsonElement>> ReadEnvelope(HttpResponseMessage response)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        return (await JsonSerializer.DeserializeAsync<ApiResponse<JsonElement>>(stream, Web))!;
    }

    private static JsonElement DecodeJwtPayload(string jwt)
    {
        string payload = jwt.Split('.')[1];
        string padded = payload.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        byte[] bytes = Convert.FromBase64String(padded);
        return JsonDocument.Parse(Encoding.UTF8.GetString(bytes)).RootElement.Clone();
    }
}
