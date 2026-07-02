using System.Drawing;
using DevExpress.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.Parameters;
using DevExpress.XtraReports.UI;
using SIAD.Core.Entities;

namespace SIAD.Reports;

internal static class ReportCompanyHeaderParameters
{
    public const string CompanyName = "HeaderCompanyName";
    public const string CompanyInfoLine = "HeaderCompanyInfoLine";
    public const string CompanyAddress = "HeaderCompanyAddress";

    public static void Apply(XtraReport report, cfg_company? company)
    {
        ArgumentNullException.ThrowIfNull(report);

        var companyName = ResolveCompanyName(company);
        var infoLine = BuildInfoLine(company);
        var address = company?.address?.Trim() ?? string.Empty;

        UpsertStringParameter(report, CompanyName, "Empresa del encabezado", companyName);
        UpsertStringParameter(report, CompanyInfoLine, "Datos fiscales/contacto del encabezado", infoLine);
        UpsertStringParameter(report, CompanyAddress, "Direccion del encabezado", address);
    }

    public static ReportHeaderBand CreateHeaderBand(float width, string reportTitle, string? subtitle = null)
    {
        var hasSubtitle = !string.IsNullOrWhiteSpace(subtitle);
        var header = new ReportHeaderBand { HeightF = hasSubtitle ? 124f : 104f };

        var companyLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 0f, width, 24f),
            Font = new DXFont("Arial", 13f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        companyLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"?{CompanyName}"));

        var titleLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 26f, width, 24f),
            Font = new DXFont("Arial", 12f, DXFontStyle.Bold),
            Text = reportTitle,
            TextAlignment = TextAlignment.MiddleCenter
        };

        var infoLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 52f, width, 18f),
            Font = new DXFont("Arial", 8.5f),
            ForeColor = Color.DimGray,
            TextAlignment = TextAlignment.MiddleCenter
        };
        infoLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"?{CompanyInfoLine}"));

        var addressLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 70f, width, 18f),
            Font = new DXFont("Arial", 8.5f),
            ForeColor = Color.DimGray,
            TextAlignment = TextAlignment.MiddleCenter
        };
        addressLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"?{CompanyAddress}"));

        header.Controls.AddRange([companyLabel, titleLabel, infoLabel, addressLabel]);

        if (hasSubtitle)
        {
            header.Controls.Add(new XRLabel
            {
                BoundsF = new RectangleF(0f, 92f, width, 24f),
                Font = new DXFont("Arial", 8.5f),
                ForeColor = Color.DimGray,
                Multiline = true,
                Text = subtitle!.Trim(),
                TextAlignment = TextAlignment.MiddleCenter
            });
        }

        return header;
    }

    public static void PrependTo(ReportHeaderBand header, float width, string reportTitle, string? subtitle = null)
    {
        ArgumentNullException.ThrowIfNull(header);

        var prefix = CreateHeaderBand(width, reportTitle, subtitle);
        var offset = prefix.HeightF;

        foreach (var control in header.Controls.Cast<XRControl>().ToList())
        {
            control.TopF += offset;
        }

        header.HeightF += offset;
        header.Controls.AddRange(prefix.Controls.Cast<XRControl>().ToArray());
    }

    private static void UpsertStringParameter(XtraReport report, string name, string description, string value)
    {
        var parameter = report.Parameters
            .Cast<Parameter>()
            .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));

        if (parameter is null)
        {
            parameter = new Parameter { Name = name };
            report.Parameters.Add(parameter);
        }

        parameter.Description = description;
        parameter.Type = typeof(string);
        parameter.AllowNull = true;
        parameter.Visible = false;
        parameter.Value = value;
    }

    private static string ResolveCompanyName(cfg_company? company)
        => FirstNonEmpty(company?.legal_name, company?.commercial_name, company?.code, "EMPRESA");

    private static string BuildInfoLine(cfg_company? company)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(company?.tax_id))
        {
            parts.Add($"RTN: {company.tax_id.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(company?.phone))
        {
            parts.Add($"Tel: {company.phone.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(company?.email))
        {
            parts.Add(company.email.Trim());
        }

        return string.Join(" | ", parts);
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
