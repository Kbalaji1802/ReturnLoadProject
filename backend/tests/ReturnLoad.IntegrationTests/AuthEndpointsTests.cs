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
    public async Task Register_defaults_to_the_driver_role()
    {
        HttpClient client = _factory.CreateClient();
        ApiResponse<JsonElement> body = await ReadEnvelope(await Register(client, Unique("role-default"), StrongPassword));

        JsonElement payload = DecodeJwtPayload(body.Data.GetProperty("accessToken").GetString()!);
        Assert.Equal(Roles.Driver, payload.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Register_as_load_owner_grants_the_shipper_role()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = Unique("loadowner"),
            password = StrongPassword,
            phoneNumber = (string?)null,
            deviceId = "test-device",
            accountType = (int)AccountType.LoadOwner,
        });

        response.EnsureSuccessStatusCode();
        JsonElement payload = DecodeJwtPayload((await ReadEnvelope(response)).Data.GetProperty("accessToken").GetString()!);
        Assert.Equal(Roles.Shipper, payload.GetProperty("role").GetString());
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

    [Fact]
    public async Task Drivers_me_returns_404_when_the_account_has_no_driver_profile()
    {
        HttpClient client = _factory.CreateClient();
        string token = (await ReadEnvelope(await Register(client, Unique("nodriver"), StrongPassword)))
            .Data.GetProperty("accessToken").GetString()!;

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/drivers/me");
        request.Headers.Authorization = new("Bearer", token);
        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Generic_document_upload_is_forbidden_for_a_non_staff_user()
    {
        // A self-registered account is a Driver, not internal staff, so it cannot attach a
        // document to an arbitrary owner id — it must use driver-upload (self-scoped). S1.
        HttpClient client = _factory.CreateClient();
        string token = (await ReadEnvelope(await Register(client, Unique("notstaff"), StrongPassword)))
            .Data.GetProperty("accessToken").GetString()!;

        using HttpRequestMessage request = new(HttpMethod.Post, "/api/v1/documents/upload");
        request.Headers.Authorization = new("Bearer", token);
        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Registering_with_a_name_and_mobile_creates_the_profile()
    {
        // A load owner who signs up with enough detail must be able to act immediately. Without
        // this, only the seeded demo shipper had a UserProfile and every real signup was told to
        // "complete your profile" with no endpoint that could.
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage registration = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = Unique("owner-with-name"),
            password = StrongPassword,
            phoneNumber = "9800000001",
            deviceId = "test-device",
            accountType = (int)AccountType.LoadOwner,
            fullName = "Profiled Owner",
        });
        registration.EnsureSuccessStatusCode();
        string token = (await ReadEnvelope(registration)).Data.GetProperty("accessToken").GetString()!;

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/profile/me");
        request.Headers.Authorization = new("Bearer", token);
        HttpResponseMessage response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("Profiled Owner", (await ReadEnvelope(response)).Data.GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task An_account_without_a_profile_can_create_one()
    {
        // The backfill path for accounts that already exist — they registered before the name
        // was collected, so they need a way to complete the profile without re-registering.
        HttpClient client = _factory.CreateClient();
        string token = (await ReadEnvelope(await Register(client, Unique("owner-no-name"), StrongPassword)))
            .Data.GetProperty("accessToken").GetString()!;

        using HttpRequestMessage before = new(HttpMethod.Get, "/api/v1/profile/me");
        before.Headers.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(before)).StatusCode);

        using HttpRequestMessage create = new(HttpMethod.Post, "/api/v1/profile")
        {
            Content = JsonContent.Create(new { fullName = "Backfilled Owner", mobile = "9800000002", email = (string?)null }),
        };
        create.Headers.Authorization = new("Bearer", token);
        (await client.SendAsync(create)).EnsureSuccessStatusCode();

        using HttpRequestMessage after = new(HttpMethod.Get, "/api/v1/profile/me");
        after.Headers.Authorization = new("Bearer", token);
        HttpResponseMessage response = await client.SendAsync(after);

        response.EnsureSuccessStatusCode();
        Assert.Equal("Backfilled Owner", (await ReadEnvelope(response)).Data.GetProperty("fullName").GetString());
    }

    [Fact]
    public async Task Creating_a_second_profile_conflicts()
    {
        HttpClient client = _factory.CreateClient();
        string token = (await ReadEnvelope(await Register(client, Unique("owner-dupe"), StrongPassword)))
            .Data.GetProperty("accessToken").GetString()!;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            using HttpRequestMessage create = new(HttpMethod.Post, "/api/v1/profile")
            {
                Content = JsonContent.Create(new { fullName = "Only Once", mobile = "9800000003", email = (string?)null }),
            };
            create.Headers.Authorization = new("Bearer", token);
            HttpResponseMessage response = await client.SendAsync(create);

            if (attempt == 0)
            {
                response.EnsureSuccessStatusCode();
            }
            else
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            }
        }
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
