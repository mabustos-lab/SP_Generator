using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SP_Generator
{
    /// <summary>
    /// Modelo para almacenar la información de cada columna.
    /// </summary>
    public record ColumnSchema(
        string ColumnName,
        string DataType,
        bool IsPk,
        bool IsNullable,
        bool IsIdentity,
        bool IsExcluded
    );
}
