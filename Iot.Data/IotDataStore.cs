using Microsoft.EntityFrameworkCore;

namespace Iot.Data;

public static class IotDataStore
{
	public static AppDbContext CreateDbContext(string dataBasePath)
	{
		var options = CreateDbContextOptions(dataBasePath);
		var dbContext = new AppDbContext(options);
		dbContext.Database.Migrate();
		return dbContext;
	}

	public static DbContextOptions<AppDbContext> CreateDbContextOptions(string dataBasePath)
	{
		string? directory = Path.GetDirectoryName(dataBasePath);
		if (!string.IsNullOrWhiteSpace(directory))
			Directory.CreateDirectory(directory);

		string connectionString = $"Data Source={dataBasePath}";
		return new DbContextOptionsBuilder<AppDbContext>()
			.UseSqlite(connectionString)
			.Options;
	}
}
