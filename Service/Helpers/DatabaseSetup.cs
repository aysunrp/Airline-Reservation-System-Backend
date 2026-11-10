using System.Data.Common;

namespace Service.Helpers;

/// <summary>Recognises a database that is missing a table or column because a migration has not been applied yet.</summary>
public static class DatabaseSetup
{
    /// <summary>What a person is told. It names no table, column or SQL text: those stay in the server log.</summary>
    public const string RequiredMessage = "A table that this feature needs is missing in the database: the latest database migration has not been applied. Please ask the administrator to apply it.";

    public static bool IsMissingTable(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is DbException && (ex.Message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
                                      || ex.Message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
