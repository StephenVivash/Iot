namespace Iot.Data;

public static class DatabasePaths
{
    private const string DatabaseFileName = "Iot.Data.db";

    public static string GetDatabasePath()
    {
		//return Path.Combine(Directory.GetCurrentDirectory(), DatabaseFileName);
		return Path.Combine(@"C:\Src\Iot\Iot.Data", DatabaseFileName);
    }

    public static string GetConnectionString()
    {
        return $"Data Source={GetDatabasePath()}";
    }
}
