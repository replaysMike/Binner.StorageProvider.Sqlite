using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
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
                // also add schema new columns added
                foreach (var columnProp in columnProps)
                    tableSchemas.Add(CreateTableColumnIfNotExists(tableProperty.Name, columnProp));
            }
            return tableSchemas;
        }

        private string GetColumnSchema(ExtendedProperty prop, bool includeDefaultValue = false)
        {
            var columnSchema = "";
            var defaultValue = "";
            var propExtendedType = prop.Type;
            var maxLength = GetMaxLength(prop);
            if (propExtendedType.IsCollection)
            {
                // store as string, data will be comma delimited
                columnSchema = $"{prop.Name} text";
                defaultValue = "''";
            }
            else
            {
                switch (propExtendedType)
                {
                    case var p when p.NullableBaseType == typeof(byte):
                        columnSchema = $"{prop.Name} tinyint";
                        defaultValue = "0";
                        break;
                    case var p when p.NullableBaseType == typeof(short):
                        columnSchema = $"{prop.Name} smallint";
                        defaultValue = "0";
                        break;
                    case var p when p.NullableBaseType == typeof(int):
                        columnSchema = $"{prop.Name} integer";
                        defaultValue = "0";
                        break;
                    case var p when p.NullableBaseType == typeof(long):
                        // columnSchema = $"{prop.Name} bigint";
                        columnSchema = $"{prop.Name} integer"; // this is needed for primary keys that use long, sqlite limitation
                        defaultValue = "0";
                        break;
                    case var p when p.NullableBaseType == typeof(double):
                        columnSchema = $"{prop.Name} float";
                        defaultValue = "0";
                        break;
                    case var p when p.NullableBaseType == typeof(decimal):
                        columnSchema = $"{prop.Name} decimal(18, 3)";
                        defaultValue = "0";
                        break;
                    case var p when p.NullableBaseType == typeof(string):
                        if (maxLength != "max")
                            columnSchema = $"{prop.Name} nvarchar({maxLength})";
                        else
                            columnSchema = $"{prop.Name} text";
                        defaultValue = "0";
                        break;
                    case var p when p.NullableBaseType == typeof(DateTime):
                        columnSchema = $"{prop.Name} datetime";
                        defaultValue = "GETUTCDATE()";
                        break;
                    case var p when p.NullableBaseType == typeof(TimeSpan):
                        columnSchema = $"{prop.Name} time";
                        defaultValue = "GETUTCDATE()";
                        break;
                    case var p when p.NullableBaseType == typeof(Guid):
                        columnSchema = $"{prop.Name} nvarchar(36)";
                        defaultValue = "NEWID()";
                        break;
                    case var p when p.NullableBaseType == typeof(bool):
                        columnSchema = $"{prop.Name} integer";
                        defaultValue = "0";
                        break;
                    case var p when p.NullableBaseType == typeof(byte[]):
                        columnSchema = $"{prop.Name} blob";
                        defaultValue = "(x'')";
                        break;
                    case var p when p.NullableBaseType.IsEnum:
                        columnSchema = $"{prop.Name} integer";
                        defaultValue = "0";
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported data type: {prop.Type}");
                }
            }
            if (prop.CustomAttributes.ToList().Any(x => x.AttributeType == typeof(KeyAttribute)))
            {
                columnSchema += " PRIMARY KEY";
            }
            else if (propExtendedType.Type != typeof(string) && !propExtendedType.IsNullable &&
                     !propExtendedType.IsCollection)
            {
                if (includeDefaultValue)
                    columnSchema += $" NOT NULL DEFAULT {defaultValue}";
                else
                    columnSchema += " NOT NULL";
            }

            return columnSchema;
        }

        private string GetMaxLength(ExtendedProperty prop)
        {
            var maxLengthAttr = prop.CustomAttributes.ToList().FirstOrDefault(x => x.AttributeType == typeof(MaxLengthAttribute));
            var maxLength = "max";
            if (maxLengthAttr != null)
            {
                maxLength = maxLengthAttr.ConstructorArguments.First().Value?.ToString() ?? "max";
            }
            return maxLength;
        }

        private string CreateTableColumnIfNotExists(string tableName, ExtendedProperty columnProp)
        {
            var columnSchema = GetColumnSchema(columnProp, true);
            return $@"SELECT CASE (SELECT count(*) FROM pragma_table_info('{tableName}') c WHERE c.name = '{columnProp.Name}') WHEN 0 THEN 
    ALTER TABLE {tableName} ADD {columnSchema};
END";
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
