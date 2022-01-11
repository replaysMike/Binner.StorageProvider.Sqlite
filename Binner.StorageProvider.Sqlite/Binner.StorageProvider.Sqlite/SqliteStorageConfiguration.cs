namespace Binner.StorageProvider.Sqlite
{
    public class SqliteStorageConfiguration
    {
        public string ConnectionString { get; set; }

        public SqliteStorageConfiguration()
        {
        }

        public SqliteStorageConfiguration(IDictionary<string, string> config)
        {
            if (config.ContainsKey("ConnectionString"))
                ConnectionString = config["ConnectionString"];
        }
    }
}
