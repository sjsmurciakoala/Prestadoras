namespace SIAD.Core.Constants;

public static class ReportesWebConstants
{
    public const string TipoOrigenReporte = "REPORT";
    public const string CodigoReporteBancosTransacciones = "bancos-transacciones";
    public const string CodigoDatasetBancosTransacciones = "bancos-transacciones";
    public const string OrigenDatasetBancosTransacciones = "public.rep_bancos_transacciones";
    public const string CodigoReporteBalanceComprobacion = "balance-comprobacion";
    public const string CodigoDatasetBalanceComprobacion = "balance-comprobacion";
    public const string OrigenDatasetBalanceComprobacion = "public.rep_balance_comprobacion";
    public const string CodigoReporteEstadoSituacionFinanciera = "estado-situacion-financiera";
    public const string CodigoDatasetEstadoSituacionFinanciera = "estado-situacion-financiera";
    public const string OrigenDatasetEstadoSituacionFinanciera = "public.rep_estado_situacion_financiera";
    public const string CodigoReporteEstadoResultados = "estado-resultados";
    public const string CodigoDatasetEstadoResultados = "estado-resultados";
    public const string OrigenDatasetEstadoResultados = "public.rep_estado_resultados";
    public const string DefaultReportingConnectionName = "DefaultConnection";

    public static class LayoutStatus
    {
        public const string Draft = "DRAFT";
        public const string Published = "PUBLISHED";
        public const string Archived = "ARCHIVED";
    }

    public static class LayoutMode
    {
        public const string Draft = "draft";
        public const string Published = "published";
    }

    public static class DatasetSourceType
    {
        public const string StoredProcedure = "STORED_PROCEDURE";
        public const string View = "VIEW";
        public const string Sql = "SQL";
    }

    public static class DatasetParameterDataType
    {
        public const string Text = "TEXT";
        public const string Int64 = "INT64";
        public const string Decimal = "DECIMAL";
        public const string Date = "DATE";
        public const string DateTime = "DATETIME";
        public const string Boolean = "BOOLEAN";
    }

    public static class DatasetParameterValueSource
    {
        public const string Report = "REPORT";
        public const string CurrentCompany = "CURRENT_COMPANY";
        public const string Fixed = "FIXED";
    }

    public static string NormalizeCode(string codigo)
        => string.IsNullOrWhiteSpace(codigo)
            ? string.Empty
            : codigo.Trim().ToLowerInvariant();

    public static bool IsValidCode(string codigo)
    {
        var normalized = NormalizeCode(codigo);
        if (string.IsNullOrWhiteSpace(normalized) || !char.IsLetterOrDigit(normalized[0]))
        {
            return false;
        }

        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    public static string BuildViewerRoute(string codigo)
        => $"/informes/reportes/{NormalizeCode(codigo)}/viewer";

    public static string BuildDesignerRoute(string codigo)
        => $"/informes/reportes/{NormalizeCode(codigo)}/designer";

    public static string BuildReportName(string codigo, string mode)
        => $"{NormalizeCode(codigo)}?mode={mode}";

    public static bool IsValidDatasetSourceType(string value)
        => value is DatasetSourceType.StoredProcedure
            or DatasetSourceType.View
            or DatasetSourceType.Sql;

    public static bool IsValidDatasetParameterDataType(string value)
        => value is DatasetParameterDataType.Text
            or DatasetParameterDataType.Int64
            or DatasetParameterDataType.Decimal
            or DatasetParameterDataType.Date
            or DatasetParameterDataType.DateTime
            or DatasetParameterDataType.Boolean;

    public static bool IsValidDatasetParameterValueSource(string value)
        => value is DatasetParameterValueSource.Report
            or DatasetParameterValueSource.CurrentCompany
            or DatasetParameterValueSource.Fixed;
}
