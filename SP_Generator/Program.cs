

using SP_Generator;
using System.Configuration;
using System.Text;

public class Program
{
    // --- CONFIGURACIÓN ---
    // Columnas a excluir de los parámetros de INSERT y UPDATE
    // Estas columnas SÍ aparecerán en los SELECT (Get, List)
    private const string ExcludedColumns = "CreatedBy,CreatedDate,ModifiedBy,ModifiedDate,Concurrence,rowguid";
    private const string DefaultAuthor = "mabg";
    public static async Task Main(string[] args)
    {
        string connString = ConfigurationManager.ConnectionStrings["MainDataBase"].ConnectionString;

        Console.WriteLine("--- Generador de Procedimientos Almacenados CRUD ---");

        try
        {
            // 1. Obtener entradas del usuario
            Console.Write($"Connection String (Default: '{connString}'): ");
            string connectionString = ReadOrDefault( connString);

            Console.Write("Schema (ej. dbo): ");
            string schemaName = ReadOrDefault( "dbo");

            Console.Write("Table Name (ej. ProfitabilityRating): ");
            string tableName = ReadOrDefault("");

            string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Generated_SPs");
            Console.Write($"Salida de archivos: ({outputDir})");
            outputDir = ReadOrDefault(outputDir);

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("La Connection String y el Table Name no pueden estar vacíos.");
            }

            // 2. Cargar el esquema de la tabla
            var schemaLoader = new SchemaLoader(connectionString);
            var columns = await schemaLoader.GetTableSchemaAsync(schemaName, tableName, ExcludedColumns);

            if (columns.Count == 0)
            {
                throw new InvalidOperationException($"No se encontró la tabla '{schemaName}.{tableName}' o no tiene columnas.");
            }

            Console.WriteLine($"Esquema cargado. {columns.Count} columnas encontradas.");

            // 3. Generar los procedimientos
            var generator = new ProcedureGenerator(schemaName, tableName, columns, DefaultAuthor);

            var procedures = new Dictionary<string, string>
            {
                { generator.GetFileName("Create"), generator.GenerateCreateProcedure() },
                { generator.GetFileName("Get"), generator.GenerateGetProcedure() },
                { generator.GetFileName("Update"), generator.GenerateUpdateProcedure() },
                { generator.GetFileName("List"), generator.GenerateListProcedure() }
            };

            // 4. Guardar archivos
            
            Directory.CreateDirectory(outputDir);

            foreach (var sp in procedures)
            {
                string filePath = Path.Combine(outputDir, sp.Key);
                await File.WriteAllTextAsync(filePath, sp.Value, Encoding.UTF8);
                Console.WriteLine($"Generado: {filePath}");
            }

            Console.WriteLine($"\n¡Éxito! Se generaron {procedures.Count} archivos en la carpeta '{outputDir}'.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nError: {ex.Message}");
            Console.ResetColor();
        }
    }
    static string ReadOrDefault(string defaultValue)
    {
        string input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input;
    }

}
