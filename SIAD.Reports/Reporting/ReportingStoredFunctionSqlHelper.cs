using DevExpress.DataAccess;
using DevExpress.DataAccess.Sql;
using SIAD.Core.Constants;

namespace SIAD.Reports;

internal static class ReportingStoredFunctionSqlHelper
{
    public sealed record StoredFunctionArgument(string Name, string? PostgreSqlTypeName);

    public static string BuildSelectSql(string routineName, IEnumerable<string?> parameterNames)
        => BuildSelectSql(routineName, parameterNames.Select(parameterName => CreateArgument(parameterName)));

    public static string BuildSelectSql(string routineName, IEnumerable<StoredFunctionArgument> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routineName);
        ArgumentNullException.ThrowIfNull(arguments);

        var normalizedArguments = arguments
            .Select(argument => CreateArgument(argument.Name, argument.PostgreSqlTypeName))
            .ToArray();

        if (normalizedArguments.Length == 0)
        {
            return $"SELECT * FROM {routineName}()";
        }

        var sqlArguments = string.Join(", ", normalizedArguments.Select(FormatArgument));
        return $"SELECT * FROM {routineName}({sqlArguments})";
    }

    public static StoredFunctionArgument CreateArgument(string? parameterName, string? postgreSqlTypeName = null)
        => new(
            NormalizeParameterName(parameterName),
            string.IsNullOrWhiteSpace(postgreSqlTypeName) ? null : postgreSqlTypeName.Trim());

    public static StoredFunctionArgument CreateArgument(QueryParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        return CreateArgument(parameter.Name, ResolvePostgreSqlTypeName(parameter));
    }

    public static string? ResolvePostgreSqlTypeName(string? dataType)
        => dataType switch
        {
            ReportesWebConstants.DatasetParameterDataType.Text => "text",
            ReportesWebConstants.DatasetParameterDataType.Int64 => "bigint",
            ReportesWebConstants.DatasetParameterDataType.Decimal => "numeric",
            ReportesWebConstants.DatasetParameterDataType.Date => "date",
            ReportesWebConstants.DatasetParameterDataType.DateTime => "timestamp without time zone",
            ReportesWebConstants.DatasetParameterDataType.Boolean => "boolean",
            _ => null
        };

    private static string? ResolvePostgreSqlTypeName(QueryParameter parameter)
        => ResolvePostgreSqlTypeName(ResolveParameterClrType(parameter));

    private static string? ResolvePostgreSqlTypeName(Type? clrType)
    {
        var type = Nullable.GetUnderlyingType(clrType ?? typeof(object)) ?? clrType;
        if (type is null)
        {
            return null;
        }

        if (type == typeof(string))
        {
            return "text";
        }

        if (type == typeof(long))
        {
            return "bigint";
        }

        if (type == typeof(int))
        {
            return "integer";
        }

        if (type == typeof(short))
        {
            return "smallint";
        }

        if (type == typeof(decimal))
        {
            return "numeric";
        }

        if (type == typeof(double))
        {
            return "double precision";
        }

        if (type == typeof(float))
        {
            return "real";
        }

        if (type == typeof(bool))
        {
            return "boolean";
        }

        if (type == typeof(Guid))
        {
            return "uuid";
        }

        if (type == typeof(DateOnly))
        {
            return "date";
        }

        if (type == typeof(DateTime))
        {
            return "timestamp without time zone";
        }

        return null;
    }

    private static Type? ResolveParameterClrType(QueryParameter parameter)
    {
        if (parameter.Value is Expression expression && expression.ResultType is not null)
        {
            return expression.ResultType;
        }

        return parameter.Type == typeof(Expression) ? null : parameter.Type;
    }

    private static string FormatArgument(StoredFunctionArgument argument)
    {
        var placeholder = $"@{argument.Name}";
        return string.IsNullOrWhiteSpace(argument.PostgreSqlTypeName)
            ? placeholder
            : $"CAST({placeholder} AS {argument.PostgreSqlTypeName})";
    }

    public static string NormalizeParameterName(string? candidate, string fallbackName = "")
    {
        var normalized = string.IsNullOrWhiteSpace(candidate)
            ? fallbackName.Trim()
            : candidate.Trim();

        while (normalized.StartsWith("@", StringComparison.Ordinal) ||
               normalized.StartsWith(":", StringComparison.Ordinal) ||
               normalized.StartsWith("?", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("El nombre tecnico del parametro no puede estar vacio.");
        }

        return normalized;
    }

    public static QueryParameter CloneAsCustomSqlParameter(QueryParameter source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new QueryParameter
        {
            Name = NormalizeParameterName(source.Name),
            Type = source.Type,
            Value = source.Value
        };
    }
}
