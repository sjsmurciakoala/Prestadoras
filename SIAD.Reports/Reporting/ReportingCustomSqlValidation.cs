using System.Text.RegularExpressions;
using DevExpress.DataAccess.ConnectionParameters;
using DevExpress.DataAccess.Web;
using DevExpress.DataAccess.Wizard.Services;

namespace SIAD.Reports;

public sealed class ReportingCustomQueryValidator : ICustomQueryValidator
{
    private static readonly Regex AllowedStartPattern =
        new(@"^\s*(select|with)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ForbiddenPattern =
        new(
            @"(--|/\*|\*/|;|\b(insert|update|delete|drop|alter|create|grant|revoke|truncate|call|execute|copy|merge|replace|comment|vacuum|analyze|refresh|reindex|security)\b|\binto\b)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    public bool Validate(DataConnectionParametersBase connectionParameters, string sql, ref string message)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            message = "La consulta SQL no puede estar vacia.";
            return false;
        }

        var normalizedSql = sql.Trim();
        if (!AllowedStartPattern.IsMatch(normalizedSql))
        {
            message = "Solo se permiten consultas SELECT o WITH ... SELECT.";
            return false;
        }

        if (ForbiddenPattern.IsMatch(normalizedSql))
        {
            message = "La consulta contiene instrucciones no permitidas para reporteria.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}

public sealed class ReportingSqlDataSourceWizardCustomizationService : ISqlDataSourceWizardCustomizationService
{
    private static readonly ICustomQueryValidator Validator = new ReportingCustomQueryValidator();

    public ICustomQueryValidator CustomQueryValidator => Validator;

    public bool IsCustomSqlDisabled => false;
}
