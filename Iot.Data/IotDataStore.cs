using Microsoft.EntityFrameworkCore;

namespace Iot.Data;

public static class IotDataStore
{
    public static AppDbContext CreateMigratedDbContext()
    {
        //Directory.CreateDirectory(Path.GetDirectoryName(DatabasePaths.GetDatabasePath()) ?? Directory.GetCurrentDirectory());

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(DatabasePaths.GetConnectionString())
            .Options;

        var dbContext = new AppDbContext(options);
        dbContext.Database.Migrate();
        return dbContext;
    }
}
