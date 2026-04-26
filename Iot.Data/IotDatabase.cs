using Microsoft.EntityFrameworkCore;

namespace Iot.Data;

public sealed class IotDatabase
{
	private readonly DbContextOptions<AppDbContext> _options;

	public IotDatabase(string dataBasePath)
	{
		_options = IotDataStore.CreateDbContextOptions(dataBasePath);
		using AppDbContext dbContext = CreateDbContext();
		dbContext.Database.Migrate();
	}

	public AppDbContext CreateDbContext() => new(_options);
}
