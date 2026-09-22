using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pege.Data
{
    public sealed class DesignTimeDataContextFactory : IDesignTimeDbContextFactory<DataContext>
    {
        public DataContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<DataContext>()
                .UseSqlite("Data Source=storage/pege.db")
                .Options;

            return new DataContext(options);
        }
    }
}