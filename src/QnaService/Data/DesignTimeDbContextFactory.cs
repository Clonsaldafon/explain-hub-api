using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QnaService.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<QnaDbContext>
{
    public QnaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<QnaDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=qnadb;Username=qna;Password=qna123");

        return new QnaDbContext(optionsBuilder.Options);
    }
}
