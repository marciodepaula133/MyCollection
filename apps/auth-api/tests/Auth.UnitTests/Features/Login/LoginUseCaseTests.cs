using Auth.Application.Abstractions;
using Auth.Application.Features.Login;
using Auth.Domain;
using MyCollection.Results;
using Xunit;

namespace Auth.UnitTests.Features.Login;

public class LoginUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private const string Password = "s3cret-pass";

    private readonly FakeUserRepository _users = new();
    private readonly FakePasswordHasher _passwordHasher = new();
    private readonly FakeTokenIssuer _tokenIssuer = new();
    private readonly FakeRefreshTokenGenerator _refreshTokenGenerator = new();
    private readonly FakeRefreshTokenStore _refreshTokens = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly LoginUseCase _useCase;

    public LoginUseCaseTests()
    {
        _useCase = new LoginUseCase(
            _users,
            _passwordHasher,
            _tokenIssuer,
            _refreshTokenGenerator,
            _refreshTokens,
            _unitOfWork,
            new FixedTimeProvider(Now));
    }

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("  USER@Example.COM  ")]
    public async Task Valid_credentials_issue_tokens_and_start_a_family(string email)
    {
        var user = _users.AddUser("user@example.com", _passwordHasher.Hash(Password));

        var result = await Login(email, Password);

        Assert.True(result.IsSuccess);
        Assert.Equal($"access:{user.Id}", result.Value.AccessToken);
        Assert.Equal(Now.AddMinutes(15), result.Value.ExpiresAt);
        Assert.Equal(Now, Assert.Single(_tokenIssuer.IssuedAt));

        var stored = Assert.Single(_refreshTokens.Added);
        Assert.Equal("raw-1", result.Value.RefreshToken);
        Assert.Equal("hash:raw-1", stored.TokenHash);
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal(Now.AddDays(30), stored.ExpiresAt);
        Assert.Equal(Now.AddDays(90), stored.FamilyExpiresAt);
        Assert.Equal(stored.ExpiresAt, result.Value.RefreshExpiresAt);
        Assert.Null(stored.ReplacedById);
        Assert.Null(stored.ConsumedAt);
        Assert.Null(stored.RevokedAt);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task Wrong_password_is_invalid_credentials_and_writes_nothing()
    {
        _users.AddUser("user@example.com", _passwordHasher.Hash(Password));

        var result = await Login("user@example.com", "wrong-pass");

        AssertInvalidCredentials(result);
        Assert.Equal(1, _passwordHasher.VerifyCount);
    }

    [Fact]
    public async Task Unknown_email_is_invalid_credentials_without_a_password_check()
    {
        var result = await Login("nobody@example.com", Password);

        AssertInvalidCredentials(result);
        Assert.Equal(0, _passwordHasher.VerifyCount);
    }

    [Fact]
    public async Task User_without_a_password_credential_is_invalid_credentials_without_a_password_check()
    {
        _users.AddUser("oauth@example.com", passwordHash: null);

        var result = await Login("oauth@example.com", Password);

        AssertInvalidCredentials(result);
        Assert.Equal(0, _passwordHasher.VerifyCount);
    }

    [Fact]
    public async Task Every_failure_returns_the_same_error()
    {
        _users.AddUser("user@example.com", _passwordHasher.Hash(Password));
        _users.AddUser("oauth@example.com", passwordHash: null);

        var wrongPassword = await Login("user@example.com", "wrong-pass");
        var unknownEmail = await Login("nobody@example.com", Password);
        var noCredential = await Login("oauth@example.com", Password);

        Assert.Same(LoginUseCase.InvalidCredentials, wrongPassword.Error);
        Assert.Same(LoginUseCase.InvalidCredentials, unknownEmail.Error);
        Assert.Same(LoginUseCase.InvalidCredentials, noCredential.Error);
    }

    [Fact]
    public async Task Second_login_starts_a_new_independent_family()
    {
        _users.AddUser("user@example.com", _passwordHasher.Hash(Password));

        var first = await Login("user@example.com", Password);
        var second = await Login("user@example.com", Password);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, _refreshTokens.Added.Count);
        Assert.NotEqual(_refreshTokens.Added[0].FamilyId, _refreshTokens.Added[1].FamilyId);
        Assert.NotEqual(_refreshTokens.Added[0].Id, _refreshTokens.Added[1].Id);
        Assert.NotEqual(first.Value.RefreshToken, second.Value.RefreshToken);
        Assert.Equal(2, _unitOfWork.CommitCount);
    }

    private Task<Result<LoginResult>> Login(string email, string password) =>
        _useCase.ExecuteAsync(new LoginCommand(email, password), TestContext.Current.CancellationToken);

    private void AssertInvalidCredentials(Result<LoginResult> result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal("invalid_credentials", result.Error!.Code);
        Assert.Equal(ErrorKind.Unauthorized, result.Error.Kind);
        Assert.Empty(_tokenIssuer.IssuedAt);
        Assert.Empty(_refreshTokens.Added);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<string, LoginCandidate> _byEmail = [];

        public User AddUser(string normalizedEmail, string? passwordHash)
        {
            var user = User.Register(normalizedEmail, "Test", "User", Now);
            _byEmail[normalizedEmail] = new LoginCandidate(user, passwordHash);
            return user;
        }

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(_byEmail.ContainsKey(normalizedEmail));

        public Task<LoginCandidate?> FindForLoginAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(_byEmail.GetValueOrDefault(normalizedEmail));

        public Task AddAsync(User user, string passwordHash, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Login never adds a user.");
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public int VerifyCount { get; private set; }

        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string passwordHash)
        {
            VerifyCount++;
            return passwordHash == Hash(password);
        }
    }

    private sealed class FakeTokenIssuer : ITokenIssuer
    {
        public List<DateTimeOffset> IssuedAt { get; } = [];

        public AccessToken Issue(User user, DateTimeOffset now)
        {
            IssuedAt.Add(now);
            return new AccessToken($"access:{user.Id}", now.AddMinutes(15));
        }
    }

    private sealed class FakeRefreshTokenGenerator : IRefreshTokenGenerator
    {
        private int _count;

        public GeneratedRefreshToken Generate()
        {
            var raw = $"raw-{++_count}";
            return new GeneratedRefreshToken(raw, $"hash:{raw}");
        }
    }

    private sealed class FakeRefreshTokenStore : IRefreshTokenStore
    {
        public List<RefreshToken> Added { get; } = [];

        public void Add(RefreshToken refreshToken) => Added.Add(refreshToken);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int CommitCount { get; private set; }

        public Task<CommitOutcome> CommitAsync(CancellationToken cancellationToken)
        {
            CommitCount++;
            return Task.FromResult(CommitOutcome.Committed);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
