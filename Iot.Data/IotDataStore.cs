using Microsoft.EntityFrameworkCore;

namespace Iot.Data;

public static class IotDataStore
{
    public static AppDbContext CreateDbContext(string databasePath)
    {
		string databaseDirectory = $"Data Source={databasePath}";
		var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(databaseDirectory)
            .Options;
        var dbContext = new AppDbContext(options);
        dbContext.Database.Migrate();
        return dbContext;
    }
}
