namespace Iot.Data;

public static class DatabasePaths
{
    private static string DatabasePath = "";

	public static void Set(string path)
	{
		//return Path.Combine(Directory.GetCurrentDirectory(), DatabaseFileName);
		DatabasePath = path;
	}
	//public static string GetDatabasePath()
    //{
	//	//return Path.Combine(Directory.GetCurrentDirectory(), DatabaseFileName);
	//	return Path.Combine(@"C:\Src\Iot\Iot.Data", DatabaseFileName);
    //}

    public static string GetConnectionString()
    {
        return $"Data Source={DatabasePath}";
    }
}
