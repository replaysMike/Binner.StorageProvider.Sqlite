using System.Collections.Generic;

namespace Binner.StorageProvider.Sqlite
{
    public class SqliteStorageConfiguration
    {
        public string ConnectionString { get; set; } = string.Empty;

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
