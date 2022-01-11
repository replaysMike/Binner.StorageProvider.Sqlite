using System.ComponentModel.DataAnnotations;
using TypeSupport;
using TypeSupport.Extensions;

namespace Binner.StorageProvider.Sqlite
{
    public class SqliteSchemaGenerator<T>
    {
        private string _dbName;
        private ICollection<ExtendedProperty> _tables;

        public SqliteSchemaGenerator(string databaseName)
        {
            _dbName = databaseName;
            var properties = typeof(T).GetProperties(PropertyOptions.HasGetter);
            _tables = properties.Where(x => x.Type.IsCollection).ToList();
        }

        public string CreateTableSchemaIfNotExists() => $@"{string.Join("\r\n", GetTableSchemas())}";

        private ICollection<string> GetTableSchemas()
        {
            var tableSchemas = new List<string>();
            foreach (var tableProperty in _tables)
            {
                var tableExtendedType = tableProperty.Type;
                var columnProps = tableExtendedType.ElementType.GetProperties(PropertyOptions.HasGetter);
                var tableSchema = new List<string>();
                foreach (var columnProp in columnProps)
                    tableSchema.Add(GetColumnSchema(columnProp));
                tableSchemas.Add(CreateTableIfNotExists(tableProperty.Name, string.Join(",\r\n", tableSchema)));
            }
            return tableSchemas;
        }

        private string GetColumnSchema(ExtendedProperty prop)
        {
            var columnSchema = "";
            var propExtendedType = prop.Type;
            var maxLength = GetMaxLength(prop);
            if (propExtendedType.IsCollection)
            {
                // store as string, data will be comma delimited
                columnSchema = $"{prop.Name} text";
            }
            else
            {
                switch (propExtendedType)
                {
                    case var p when p.NullableBaseType == typeof(byte):
                        columnSchema = $"{prop.Name} tinyint";
                        break;
                    case var p when p.NullableBaseType == typeof(short):
                        columnSchema = $"{prop.Name} smallint";
                        break;
                    case var p when p.NullableBaseType == typeof(int):
                        columnSchema = $"{prop.Name} integer";
                        break;
                    case var p when p.NullableBaseType == typeof(long):
                        // columnSchema = $"{prop.Name} bigint";
                        columnSchema = $"{prop.Name} integer"; // this is needed for primary keys that use long, sqlite limitation
                        break;
                    case var p when p.NullableBaseType == typeof(double):
                        columnSchema = $"{prop.Name} float";
                        break;
                    case var p when p.NullableBaseType == typeof(decimal):
                        columnSchema = $"{prop.Name} decimal(18, 3)";
                        break;
                    case var p when p.NullableBaseType == typeof(string):
                        if (maxLength != "max")
                            columnSchema = $"{prop.Name} nvarchar({maxLength})";
                        else
                            columnSchema = $"{prop.Name} text";
                        break;
                    case var p when p.NullableBaseType == typeof(DateTime):
                        columnSchema = $"{prop.Name} datetime";
                        break;
                    case var p when p.NullableBaseType == typeof(TimeSpan):
                        columnSchema = $"{prop.Name} time";
                        break;
                    case var p when p.NullableBaseType == typeof(byte[]):
                        columnSchema = $"{prop.Name} blob";
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported data type: {prop.Type}");
                }
            }
            if (prop.CustomAttributes.ToList().Any(x => x.AttributeType == typeof(KeyAttribute)))
            {
                columnSchema = columnSchema + " PRIMARY KEY";
            }
            else if (propExtendedType.Type != typeof(string) && !propExtendedType.IsNullable && !propExtendedType.IsCollection)
                columnSchema = columnSchema + " NOT NULL";
            return columnSchema;
        }

        private string GetMaxLength(ExtendedProperty prop)
        {
            var maxLengthAttr = prop.CustomAttributes.ToList().FirstOrDefault(x => x.AttributeType == typeof(MaxLengthAttribute));
            var maxLength = "max";
            if (maxLengthAttr != null)
            {
                maxLength = maxLengthAttr.ConstructorArguments.First().Value.ToString();
            }
            return maxLength;
        }

        private string CreateTableIfNotExists(string tableName, string tableSchema)
        {
            return $@"CREATE TABLE IF NOT EXISTS {tableName} (
    {tableSchema}
);
";
        }
    }
}
