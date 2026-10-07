using Auth.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auth.Infrastructure.Entities;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public const string EmailIndexName = "ix_users_email";

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(254).IsRequired();
        builder.Property(u => u.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(u => u.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(2048);
        builder.Property(u => u.EmailVerifiedAt).HasColumnName("email_verified_at");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName(EmailIndexName);
    }
}

public class UserCredentialConfiguration : IEntityTypeConfiguration<UserCredential>
{
    public void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        builder.ToTable("user_credentials");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.UserId).HasColumnName("user_id");
        builder.Property(c => c.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.HasIndex(c => c.UserId).IsUnique().HasDatabaseName("ix_user_credentials_user_id");
        builder.HasOne<User>().WithOne().HasForeignKey<UserCredential>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
