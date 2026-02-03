using Microsoft.EntityFrameworkCore;
using AINewsHub.Core.Entities;

namespace AINewsHub.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<UserTagPreference> UserTagPreferences => Set<UserTagPreference>();
    public DbSet<TestHistory> TestHistories => Set<TestHistory>();
    public DbSet<QuestionSet> QuestionSets => Set<QuestionSet>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<ArticleTag> ArticleTags => Set<ArticleTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User - Snowflake ID unique index
        modelBuilder.Entity<User>()
            .HasIndex(u => u.SnowflakeId)
            .IsUnique();

        // Article - URL unique index
        modelBuilder.Entity<Article>()
            .HasIndex(a => a.Url)
            .IsUnique();

        // ArticleTag - composite key
        modelBuilder.Entity<ArticleTag>()
            .HasKey(at => new { at.ArticleId, at.TagId });

        modelBuilder.Entity<ArticleTag>()
            .HasOne(at => at.Article)
            .WithMany(a => a.ArticleTags)
            .HasForeignKey(at => at.ArticleId);

        modelBuilder.Entity<ArticleTag>()
            .HasOne(at => at.Tag)
            .WithMany(t => t.ArticleTags)
            .HasForeignKey(at => at.TagId);

        // UserTagPreference
        modelBuilder.Entity<UserTagPreference>()
            .HasOne(p => p.User)
            .WithMany(u => u.TagPreferences)
            .HasForeignKey(p => p.UserId);

        modelBuilder.Entity<UserTagPreference>()
            .HasOne(p => p.Tag)
            .WithMany(t => t.UserPreferences)
            .HasForeignKey(p => p.TagId);

        // Unique constraint: User can only have one preference per tag
        modelBuilder.Entity<UserTagPreference>()
            .HasIndex(p => new { p.UserId, p.TagId })
            .IsUnique();

        // TestHistory
        modelBuilder.Entity<TestHistory>()
            .HasOne(t => t.User)
            .WithMany(u => u.TestHistories)
            .HasForeignKey(t => t.UserId);

        modelBuilder.Entity<TestHistory>()
            .HasOne(t => t.QuestionSet)
            .WithMany(q => q.TestHistories)
            .HasForeignKey(t => t.QuestionSetId);

        // Question
        modelBuilder.Entity<Question>()
            .HasOne(q => q.QuestionSet)
            .WithMany(qs => qs.Questions)
            .HasForeignKey(q => q.QuestionSetId);

        // Article - Source
        modelBuilder.Entity<Article>()
            .HasOne(a => a.Source)
            .WithMany(s => s.Articles)
            .HasForeignKey(a => a.SourceId);

        // Tag - unique name
        modelBuilder.Entity<Tag>()
            .HasIndex(t => t.Name)
            .IsUnique();

        // Source - unique URL
        modelBuilder.Entity<Source>()
            .HasIndex(s => s.Url)
            .IsUnique();
    }
}
