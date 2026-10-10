using Auth.Application.Abstractions;
using Auth.Application.Features.Register;
using Auth.Domain;
using MyCollection.Results;
using Xunit;

namespace Auth.UnitTests.Features.Register;

public class RegisterUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeUserRepository _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly RegisterUseCase _useCase;

    public RegisterUseCaseTests()
    {
        _useCase = new RegisterUseCase(_users, new FakePasswordHasher(), _unitOfWork, new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Happy_path_stages_user_with_hashed_password_and_commits()
    {
        var result = await _useCase.ExecuteAsync(
            new RegisterCommand("new@user.com", "s3cret-pass", "New", "User"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("new@user.com", result.Value.Email);
        Assert.Equal("New", result.Value.FirstName);
        Assert.Equal("User", result.Value.LastName);

        var (user, hash) = Assert.Single(_users.Added);
        Assert.Equal(result.Value.Id, user.Id);
        Assert.Equal("New", user.FirstName);
        Assert.Equal("User", user.LastName);
        Assert.Equal("hashed:s3cret-pass", hash);
        Assert.NotEqual("s3cret-pass", hash);
        Assert.Null(user.EmailVerifiedAt);
        Assert.Equal(Now, user.CreatedAt);
        Assert.Equal(Now, user.UpdatedAt);
        Assert.Equal(1, _unitOfWork.CommitCount);
    }

    [Theory]
    [InlineData(" Foo@Bar.com ", "foo@bar.com")]
    [InlineData("FOO@BAR.COM", "foo@bar.com")]
    public async Task Email_is_trimmed_and_lower_cased(string input, string expected)
    {
        var result = await _useCase.ExecuteAsync(
            new RegisterCommand(input, "s3cret-pass", "First", "Last"),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Value.Email);
        Assert.Equal(expected, Assert.Single(_users.Added).User.Email);
    }

    [Fact]
    public async Task Existing_email_is_rejected_before_staging_or_committing()
    {
        _users.ExistingEmails.Add("dup@user.com");

        var result = await _useCase.ExecuteAsync(
            new RegisterCommand(" DUP@user.com ", "s3cret-pass", "First", "Last"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("email_already_registered", result.Error!.Code);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        Assert.Empty(_users.Added);
        Assert.Equal(0, _unitOfWork.CommitCount);
    }

    [Fact]
    public async Task Unique_violation_at_commit_from_a_concurrent_register_is_the_same_conflict()
    {
        _unitOfWork.Outcome = CommitOutcome.UniqueViolation;

        var result = await _useCase.ExecuteAsync(
            new RegisterCommand("dup@user.com", "s3cret-pass", "First", "Last"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("email_already_registered", result.Error!.Code);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public HashSet<string> ExistingEmails { get; } = [];

        public List<(User User, string PasswordHash)> Added { get; } = [];

        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingEmails.Contains(normalizedEmail));

        public Task AddAsync(User user, string passwordHash, CancellationToken cancellationToken)
        {
            Added.Add((user, passwordHash));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public CommitOutcome Outcome { get; set; } = CommitOutcome.Committed;

        public int CommitCount { get; private set; }

        public Task<CommitOutcome> CommitAsync(CancellationToken cancellationToken)
        {
            CommitCount++;
            return Task.FromResult(Outcome);
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hashed:{password}";

        public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
