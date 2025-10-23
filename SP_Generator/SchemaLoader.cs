using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SP_Generator
{
    /// <summary>
    /// Carga el esquema de la tabla desde la base de datos.
    /// </summary>
    public class SchemaLoader
    {
        private readonly string _connectionString;

        // Esta consulta para obtener la metadata de la tabla:
        // 1. Añadido filtro por TABLE_SCHEMA.
        // 2. Añadido `IsIdentity`.
        // 3. Parametrizada la lista de columnas excluidas.
        private const string SchemaQuery = @"
        SELECT
            c.TABLE_SCHEMA,
            c.TABLE_NAME,
            c.COLUMN_NAME,
            c.ORDINAL_POSITION,
            DATA_TYPE = CASE 
                WHEN c.DATA_TYPE IN ('char','varchar','nchar','nvarchar') 
                    THEN CONCAT(c.DATA_TYPE, '(', 
                                CASE WHEN c.CHARACTER_MAXIMUM_LENGTH = -1 
                                     THEN 'MAX' 
                                     ELSE CAST(c.CHARACTER_MAXIMUM_LENGTH AS NVARCHAR(10)) END, 
                                ')')
                WHEN c.DATA_TYPE IN ('decimal','numeric') 
                    THEN CONCAT(c.DATA_TYPE, '(', c.NUMERIC_PRECISION, ',', c.NUMERIC_SCALE, ')')
                ELSE c.DATA_TYPE 
            END,
            Is_PK = CAST(ISNULL(pk.IsPk, 0) AS BIT),
            IS_NULLABLE = CAST(CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS BIT),
            IsIdentity = CAST(COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS BIT),
            IS_EXCLUDED = CAST(CASE WHEN xc.ColumnName IS NOT NULL THEN 1 ELSE 0 END AS BIT)
        FROM 
            INFORMATION_SCHEMA.COLUMNS c
        LEFT JOIN (
            SELECT 
                SCHEMA_NAME(t.schema_id) AS TableSchema,
                t.name AS TableName,
                c.name AS ColumnName,
                1 AS IsPk
            FROM 
                sys.tables t
            INNER JOIN 
                sys.indexes i ON t.object_id = i.object_id
            INNER JOIN 
                sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            INNER JOIN 
                sys.columns c ON ic.object_id = c.object_id AND c.column_id = ic.column_id
            WHERE 
                i.is_primary_key = 1
        ) pk ON c.TABLE_SCHEMA = pk.TableSchema AND c.TABLE_NAME = pk.TableName AND c.COLUMN_NAME = pk.ColumnName
        LEFT JOIN (
            SELECT value AS ColumnName FROM STRING_SPLIT(@ExcludedColumns, ',')
        ) xc ON c.COLUMN_NAME = xc.ColumnName
        WHERE 
            c.TABLE_SCHEMA = @SchemaName
            AND c.TABLE_NAME = @TableName
        ORDER BY 
            c.ORDINAL_POSITION;
    ";

        public SchemaLoader(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<ColumnSchema>> GetTableSchemaAsync(string schemaName, string tableName, string excludedColumns)
        {
            var columns = new List<ColumnSchema>();
            using (var connection = new SqlConnection(_connectionString))
            {
                using (var command = new SqlCommand(SchemaQuery, connection))
                {
                    command.Parameters.AddWithValue("@SchemaName", schemaName);
                    command.Parameters.AddWithValue("@TableName", tableName);
                    command.Parameters.AddWithValue("@ExcludedColumns", excludedColumns);

                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            columns.Add(new ColumnSchema(
                                ColumnName: (string)reader["COLUMN_NAME"],
                                DataType: (string)reader["DATA_TYPE"],
                                IsPk: (bool)reader["Is_PK"],
                                IsNullable: (bool)reader["IS_NULLABLE"],
                                IsIdentity: (bool)reader["IsIdentity"],
                                IsExcluded: (bool)reader["IS_EXCLUDED"]
                            ));
                        }
                    }
                }
            }
            return columns;
        }
    }
}
