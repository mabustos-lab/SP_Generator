using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SP_Generator
{
    /// <summary>
    /// Contiene la lógica para generar el texto de los SPs.
    /// </summary>
    public class ProcedureGenerator
    {
        private readonly string _schemaName;
        private readonly string _tableName;
        private readonly List<ColumnSchema> _allColumns;
        private readonly List<ColumnSchema> _pkColumns;
        private readonly List<ColumnSchema> _writableColumns; // Columnas no-PK, no-Identity, no-Excluidas
        private readonly List<ColumnSchema> _allParamsForUpdate; // Writable + PKs
        private readonly string _procedureNameBase;
        private readonly string _author;
        private readonly string _creationDate;

        public ProcedureGenerator(string schemaName, string tableName, List<ColumnSchema> allColumns, string author)
        {
            _schemaName = schemaName;
            _tableName = tableName;
            _allColumns = allColumns;
            _author = author;
            _creationDate = DateTime.Now.ToString("yyyy-MM-dd");

            _procedureNameBase = $"usp_{_tableName}";

            // Filtrar columnas para diferentes operaciones
            _pkColumns = _allColumns.Where(c => c.IsPk).ToList();
            _writableColumns = _allColumns.Where(c => !c.IsPk && !c.IsIdentity && !c.IsExcluded).ToList();
            _allParamsForUpdate = _allColumns.Where(c => (!c.IsIdentity && !c.IsExcluded) || c.IsPk).ToList();
        }

        public string GetFileName(string action) => $"{_procedureNameBase}_{action}.sql";

        #region Generadores Principales

        public string GenerateCreateProcedure()
        {
            string procedureName = $"{_procedureNameBase}_Create";
            var inputParams = _allColumns.Where(c => !c.IsIdentity && !c.IsExcluded).ToList();

            var sb = new StringBuilder();
            sb.AppendLine(GenerateHeader(procedureName, inputParams, "Crea un nuevo registro en la tabla.", "Regresa un resulset (InsertedId) con el ID del nuevo registro"));
            sb.AppendLine($"CREATE PROCEDURE [{_schemaName}].[{procedureName}]");
            sb.AppendLine(FormatParamsList(inputParams));
            sb.AppendLine("--@Encryptable: WITH ENCRYPTION");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    SET NOCOUNT ON;");
            sb.AppendLine("    SET XACT_ABORT ON;");
            sb.AppendLine();
            sb.AppendLine("    BEGIN TRY");
            sb.AppendLine("        BEGIN TRANSACTION;");
            sb.AppendLine();
            sb.AppendLine($"        INSERT INTO [{_schemaName}].[{_tableName}] (");
            sb.AppendLine($"            {FormatColumnList(inputParams, "            ")}");
            sb.AppendLine("        )");
            sb.AppendLine("        VALUES (");
            sb.AppendLine($"            {FormatParamsList(inputParams, "            ", withTypes: false)}");
            sb.AppendLine("        );");
            sb.AppendLine();
            if (_allColumns.Count(x=>x.IsPk)==1)
            {
                sb.AppendLine("        SELECT SCOPE_IDENTITY() AS InsertedId;");
            }
            else
            {
                sb.AppendLine("        -- Opcional: Retornar el ID o el registro creado");
                sb.AppendLine("        -- SELECT SCOPE_IDENTITY() AS InsertedId;");
            }
            sb.AppendLine();
            sb.AppendLine("        COMMIT TRANSACTION;");
            sb.AppendLine("    END TRY");
            sb.AppendLine("    BEGIN CATCH");
            sb.AppendLine("        IF @@TRANCOUNT > 0");
            sb.AppendLine("            ROLLBACK TRANSACTION;");
            sb.AppendLine("        THROW;");
            sb.AppendLine("    END CATCH");
            sb.AppendLine("END;");
            return sb.ToString();
        }

        public string GenerateGetProcedure()
        {
            if (_pkColumns.Count == 0)
                return $"-- No se puede generar GET para {_tableName}: No tiene Clave Primaria (PK).";

            string procedureName = $"{_procedureNameBase}_Get";

            var sb = new StringBuilder();
            sb.AppendLine(GenerateHeader(procedureName, _pkColumns, "Obtiene un registro por su Clave Primaria.","Regresa un registro por si clave primaria."));
            sb.AppendLine($"CREATE PROCEDURE [{_schemaName}].[{procedureName}]");
            sb.AppendLine(FormatParamsList(_pkColumns));
            sb.AppendLine("--@Encryptable: WITH ENCRYPTION");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    SET NOCOUNT ON;");
            sb.AppendLine("    SET XACT_ABORT ON;");
            sb.AppendLine();
            sb.AppendLine("    BEGIN TRY");
            sb.AppendLine();
            sb.AppendLine($"        SELECT");
            sb.AppendLine($"            {FormatColumnList(_allColumns, "            ")}");
            sb.AppendLine($"        FROM [{_schemaName}].[{_tableName}]");
            sb.AppendLine($"        WHERE {FormatWhereClause(_pkColumns, "        ")}");
            sb.AppendLine();
            sb.AppendLine("    END TRY");
            sb.AppendLine("    BEGIN CATCH");
            sb.AppendLine("        THROW;");
            sb.AppendLine("    END CATCH");
            sb.AppendLine("END;");
            return sb.ToString();
        }

        public string GenerateUpdateProcedure()
        {
            if (_pkColumns.Count == 0)
                return $"-- No se puede generar UPDATE para {_tableName}: No tiene Clave Primaria (PK).";
            if (_writableColumns.Count == 0)
                return $"-- No se puede generar UPDATE para {_tableName}: No tiene columnas actualizables.";

            string procedureName = $"{_procedureNameBase}_Update";

            var sb = new StringBuilder();
            sb.AppendLine(GenerateHeader(procedureName, _allParamsForUpdate, "Actualiza un registro existente por su Clave Primaria.","Nada"));
            sb.AppendLine($"CREATE PROCEDURE [{_schemaName}].[{procedureName}]");
            sb.AppendLine(FormatParamsList(_allParamsForUpdate));
            sb.AppendLine("--@Encryptable: WITH ENCRYPTION");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    SET NOCOUNT ON;");
            sb.AppendLine("    SET XACT_ABORT ON;");
            sb.AppendLine();
            sb.AppendLine("    BEGIN TRY");
            sb.AppendLine("        BEGIN TRANSACTION;");
            sb.AppendLine();
            sb.AppendLine($"        UPDATE [{_schemaName}].[{_tableName}]");
            sb.AppendLine($"        SET");
            sb.AppendLine($"            {FormatSetList(_writableColumns, "            ")}");
            sb.AppendLine($"        WHERE {FormatWhereClause(_pkColumns, "        ")}");
            sb.AppendLine();
            sb.AppendLine("        COMMIT TRANSACTION;");
            sb.AppendLine("    END TRY");
            sb.AppendLine("    BEGIN CATCH");
            sb.AppendLine("        IF @@TRANCOUNT > 0");
            sb.AppendLine("            ROLLBACK TRANSACTION;");
            sb.AppendLine("        THROW;");
            sb.AppendLine("    END CATCH");
            sb.AppendLine("END;");
            return sb.ToString();
        }

        public string GenerateListProcedure()
        {
            string procedureName = $"{_procedureNameBase}_List";

            var sb = new StringBuilder();
            sb.AppendLine(GenerateHeader(procedureName, new List<ColumnSchema>(), "Obtiene una lista de todos los registros.", "Regresa un resulset de registros."));
            sb.AppendLine($"CREATE PROCEDURE [{_schemaName}].[{procedureName}]");
            sb.AppendLine("--@Encryptable: WITH ENCRYPTION");
            sb.AppendLine("AS");
            sb.AppendLine("BEGIN");
            sb.AppendLine("    SET NOCOUNT ON;");
            sb.AppendLine("    SET XACT_ABORT ON;");
            sb.AppendLine();
            sb.AppendLine("    BEGIN TRY");
            sb.AppendLine();
            sb.AppendLine("        -- TODO: Agregar filtros, paginación y ordenamiento según sea necesario.");
            sb.AppendLine();
            sb.AppendLine($"        SELECT");
            sb.AppendLine($"            {FormatColumnList(_allColumns, "            ")}");
            sb.AppendLine($"        FROM [{_schemaName}].[{_tableName}];");
            sb.AppendLine();
            sb.AppendLine("    END TRY");
            sb.AppendLine("    BEGIN CATCH");
            sb.AppendLine("        THROW;");
            sb.AppendLine("    END CATCH");
            sb.AppendLine("END;");
            return sb.ToString();
        }

        #endregion

        #region Helpers de Formato SQL

        private string GenerateHeader(string procedureName, List<ColumnSchema> parameters, string description,
            string returnProcedure = "[Descripción del conjunto de resultados]")
        {
            var sb = new StringBuilder();
            sb.AppendLine("/*******************************************************************************");
            sb.AppendLine($"-- Descripción: {description}");
            sb.AppendLine($"-- Autor:       {_author}");
            sb.AppendLine($"-- Fecha de Creación: {_creationDate}");
            sb.AppendLine("--");

            if (parameters.Count > 0)
            {
                sb.AppendLine("-- Parámetros:");
                foreach (var param in parameters)
                {
                    sb.AppendLine($"--   @{param.ColumnName}: [Descripción del parámetro]");
                }
            }
            else
            {
                sb.AppendLine("-- Parámetros: N/A");
            }

            sb.AppendLine("--");
            sb.AppendLine("-- Retorno:");
            sb.AppendLine($"--   {returnProcedure}");
            sb.AppendLine("--");
            sb.AppendLine("-- Historial de Modificaciones:");
            sb.AppendLine("--   Fecha       \t\tAutor        \t\tDescripción");
            sb.AppendLine("--   ----------  \t\t-----------  \t\t-------------------------------------------");
            sb.AppendLine($"--   {_creationDate} \t\t{_author} \t\t\tVersión inicial.");
            sb.AppendLine("*******************************************************************************/");
            return sb.ToString();
        }

        private string FormatParamsList(List<ColumnSchema> columns, string indent = "    ", bool withTypes = true)
        {
            if (columns.Count == 0) return "";

            var sb = new StringBuilder();
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                sb.Append(indent);

                if (withTypes)
                    sb.Append($"@{col.ColumnName} {col.DataType}");
                else
                    sb.Append($"@{col.ColumnName}");

                if (withTypes && col.IsNullable)
                    sb.Append(" = NULL");

                if (i < columns.Count - 1)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
            }
            return sb.ToString();
        }

        private string FormatColumnList(List<ColumnSchema> columns, string indent = "    ")
        {
            return string.Join(Environment.NewLine + indent + ", ", columns.Select(c => $"[{c.ColumnName}]"));
        }

        private string FormatSetList(List<ColumnSchema> columns, string indent = "    ")
        {
            return string.Join(Environment.NewLine + indent + ", ", columns.Select(c => $"[{c.ColumnName}] = @{c.ColumnName}"));
            // La coma extra al final se maneja quitándola o SQL Server la ignora en contextos modernos, 
            // pero para ser robustos, la quitamos del último.
            var lines = columns.Select(c => $"[{c.ColumnName}] = @{c.ColumnName}").ToList();
            return string.Join("," + Environment.NewLine + indent, lines);
        }

        private string FormatWhereClause(List<ColumnSchema> columns, string indent = "    ")
        {
            var conditions = columns.Select(c => $"[{c.ColumnName}] = @{c.ColumnName}").ToList();
            return string.Join(Environment.NewLine + indent + "AND ", conditions);
        }

        #endregion
    }
}
