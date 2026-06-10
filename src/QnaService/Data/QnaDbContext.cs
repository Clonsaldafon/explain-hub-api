using Microsoft.EntityFrameworkCore;
using QnaService.Models;

namespace QnaService.Data;

public class QnaDbContext : DbContext
{
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<QuestionLike> QuestionLikes => Set<QuestionLike>();
    public DbSet<AnswerLike> AnswerLikes => Set<AnswerLike>();
    public DbSet<QuestionView> QuestionViews => Set<QuestionView>();
    public DbSet<MediaAttachment> MediaAttachments => Set<MediaAttachment>();

    public QnaDbContext(DbContextOptions<QnaDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Question>()
            .HasIndex(q => q.CreatedAt);

        modelBuilder.Entity<Question>()
            .HasMany(q => q.Answers)
            .WithOne(a => a.Question)
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Question>()
            .HasMany(q => q.Likes)
            .WithOne(l => l.Question)
            .HasForeignKey(l => l.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Question>()
            .HasMany(q => q.Views)
            .WithOne(v => v.Question)
            .HasForeignKey(v => v.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Question>()
            .HasMany(q => q.Attachments)
            .WithOne(a => a.Question)
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Answer>()
            .HasIndex(a => a.QuestionId);

        modelBuilder.Entity<Answer>()
            .HasMany(a => a.Likes)
            .WithOne(l => l.Answer)
            .HasForeignKey(l => l.AnswerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Answer>()
            .HasMany(a => a.Attachments)
            .WithOne(m => m.Answer)
            .HasForeignKey(m => m.AnswerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<QuestionLike>()
            .HasIndex(l => new { l.QuestionId, l.UserId })
            .IsUnique();

        modelBuilder.Entity<AnswerLike>()
            .HasIndex(l => new { l.AnswerId, l.UserId })
            .IsUnique();

        modelBuilder.Entity<MediaAttachment>()
            .HasIndex(m => m.QuestionId);

        modelBuilder.Entity<MediaAttachment>()
            .HasIndex(m => m.AnswerId);
    }
}
