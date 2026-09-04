using System.Drawing;
using DevExpress.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraPrinting.Shape;
using DevExpress.XtraReports.UI;
using SIAD.Core.Entities;

namespace SIAD.Reports;

/// <summary>
/// La presentación común de los estados financieros: membrete, bloque de título, cabeceras de
/// columna, filas de total y pie.
///
/// Existe porque los cuatro estados —situación financiera, resultados, flujo de efectivo y
/// cambios en el patrimonio— se entregan impresos con el mismo formato, y hasta ahora cada uno
/// lo resolvía por su cuenta con encabezados distintos. Cambiar el membrete significaba tocar
/// cuatro sitios y que el cuarto se olvidara.
///
/// El modelo es el juego de estados que firma la empresa: identidad centrada en tres líneas,
/// título del estado, la leyenda de moneda, columnas de año subrayadas, totales en negrita con
/// línea encima y el número de página solo.
/// </summary>
internal static class EstadoFinancieroLayout
{
    // Colores del membrete corporativo.
    private static readonly Color Verde = Color.FromArgb(140, 198, 63);
    private static readonly Color Azul = Color.FromArgb(43, 163, 199);
    private static readonly Color AzulOscuro = Color.FromArgb(27, 122, 158);

    /// <summary>Gris de las líneas de total; el negro puro compite con las cifras.</summary>
    private static readonly Color LineaTotal = Color.FromArgb(70, 70, 70);

    /// <summary>
    /// Negativos entre paréntesis y sin decimales, como en el juego impreso: <c>(2,968,891)</c>.
    /// El cero se deja como raya, que es lo que hace un estado financiero cuando no hay movimiento.
    /// </summary>
    public const string FormatoMonto = "{0:#,##0;(#,##0);-}";

    public const float AnchoContenido = 750f;

    // -------------------------------------------------------------------------
    // Membrete
    // -------------------------------------------------------------------------

    /// <summary>
    /// Pinta el membrete en los márgenes: las bandas de color arriba y abajo, el logo de la
    /// empresa y el número de página.
    ///
    /// Va en los márgenes y no en el encabezado del reporte a propósito: así se repite en todas
    /// las hojas sin empujar el contenido ni recalcular alturas por página.
    /// </summary>
    public static void AplicarMembrete(XtraReport report, cfg_company? empresa)
    {
        ArgumentNullException.ThrowIfNull(report);

        var superior = ObtenerBanda<TopMarginBand>(report);
        var inferior = ObtenerBanda<BottomMarginBand>(report);
        if (superior is null || inferior is null)
        {
            return;
        }

        superior.HeightF = 78f;
        inferior.HeightF = 58f;

        superior.Controls.Clear();
        inferior.Controls.Clear();

        // El logo va arriba a la derecha sobre BLANCO, y las bandas de color debajo. El logo que
        // guarda la empresa es la version oscura sobre fondo claro: puesto encima de la banda
        // azul no se leeria.
        var logo = LeerLogo(empresa);
        if (logo is not null)
        {
            superior.Controls.Add(new XRPictureBox
            {
                BoundsF = new RectangleF(AnchoContenido - 190f, 2f, 190f, 40f),
                Image = logo,
                Sizing = ImageSizeMode.ZoomImage,
                ImageAlignment = ImageAlignment.MiddleRight,
            });
        }
        else
        {
            // Sin logo cargado, el nombre ocupa su lugar para que el membrete no quede mudo.
            superior.Controls.Add(new XRLabel
            {
                BoundsF = new RectangleF(AnchoContenido - 260f, 8f, 260f, 28f),
                Font = new DXFont("Arial", 13f, DXFontStyle.Bold),
                ForeColor = AzulOscuro,
                Text = (empresa?.commercial_name ?? string.Empty).Trim(),
                TextAlignment = TextAlignment.MiddleRight,
            });
        }

        // Bandas: el verde cruza desde la izquierda y el azul remata a la derecha.
        superior.Controls.AddRange(
        [
            Banda(0f, 52f, AnchoContenido * 0.60f, 12f, Verde),
            Banda(AnchoContenido * 0.50f, 48f, AnchoContenido * 0.50f, 16f, Azul),
        ]);

        // Banda inferior: el espejo de la superior.
        inferior.Controls.AddRange(
        [
            Banda(0f, 26f, AnchoContenido * 0.42f, 16f, Azul),
            Banda(AnchoContenido * 0.30f, 34f, AnchoContenido * 0.70f, 14f, Verde),
        ]);

        // El juego impreso numera las hojas y nada más: ni fecha de generación ni "de N".
        inferior.Controls.Add(new XRPageInfo
        {
            BoundsF = new RectangleF(AnchoContenido - 60f, 4f, 60f, 16f),
            Font = new DXFont("Arial", 9f),
            ForeColor = Color.FromArgb(60, 60, 60),
            PageInfo = PageInfo.Number,
            TextAlignment = TextAlignment.MiddleRight,
            TextFormatString = "{0}",
        });
    }

    // -------------------------------------------------------------------------
    // Encabezado del estado
    // -------------------------------------------------------------------------

    /// <summary>
    /// Bloque de identidad y título, centrado: nombre comercial, razón social, plaza, el nombre
    /// del estado y la leyenda de moneda.
    ///
    /// Las tres primeras líneas salen del dataset y no de parámetros, para que el encabezado
    /// diga lo mismo que los datos que se están imprimiendo.
    /// </summary>
    public static ReportHeaderBand CrearEncabezado(string titulo, string leyendaMoneda = "(Expresado en lempiras)")
    {
        var banda = new ReportHeaderBand { HeightF = 104f };

        banda.Controls.AddRange(
        [
            LineaTitulo("[empresa_nombre]", 0f, 11f, DXFontStyle.Bold),
            LineaTitulo("[empresa_nombre_legal]", 15f, 11f, DXFontStyle.Bold),
            LineaTitulo("[empresa_direccion]", 30f, 11f, DXFontStyle.Bold),
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 45f, AnchoContenido, 15f),
                Font = new DXFont("Arial", 11f, DXFontStyle.Bold),
                Text = titulo,
                TextAlignment = TextAlignment.MiddleCenter,
            },
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 60f, AnchoContenido, 15f),
                Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
                Text = leyendaMoneda,
                TextAlignment = TextAlignment.MiddleCenter,
            },
        ]);

        return banda;
    }

    /// <summary>
    /// Cabecera de las columnas de importe: el rótulo de cada columna, subrayado y alineado a la
    /// derecha sobre sus cifras. Se repite en cada página.
    ///
    /// Los rótulos son EXPRESIONES, no texto: los años salen de la fecha del reporte, así que el
    /// encabezado no puede quedarse en un año fijo dentro del código.
    /// </summary>
    public static PageHeaderBand CrearCabeceraColumnas(
        float anchoDescripcion, float anchoImporte, params string[] expresionesRotulo)
    {
        // El PageHeaderBand ya sale en todas las páginas por definición; no lleva RepeatEveryPage.
        var banda = new PageHeaderBand { HeightF = 22f };

        var x = anchoDescripcion;
        foreach (var expresion in expresionesRotulo)
        {
            var etiqueta = new XRLabel
            {
                BoundsF = new RectangleF(x, 2f, anchoImporte, 16f),
                Font = new DXFont("Arial", 9.5f, DXFontStyle.Bold | DXFontStyle.Underline),
                TextAlignment = TextAlignment.MiddleRight,
                Padding = new PaddingInfo(0, 6, 0, 0),
            };
            etiqueta.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expresion));

            banda.Controls.Add(etiqueta);
            x += anchoImporte;
        }

        return banda;
    }

    /// <summary>
    /// Cabecera de dos niveles: un rótulo que abarca varias columnas —«AL 31 DE DICIEMBRE»,
    /// «VARIACIÓN»— y debajo el de cada una.
    ///
    /// Cada grupo es el título y las expresiones de sus columnas. Se dibuja con etiquetas y no
    /// con una tabla de celdas combinadas porque el ancho de cada columna tiene que coincidir al
    /// pixel con el de las cifras del detalle, y una tabla lo reparte por peso.
    /// </summary>
    public static PageHeaderBand CrearCabeceraAgrupada(
        float anchoDescripcion, float anchoImporte,
        params (string Titulo, string[] Columnas)[] grupos)
    {
        var banda = new PageHeaderBand { HeightF = 34f };

        var x = anchoDescripcion;
        foreach (var (titulo, columnas) in grupos)
        {
            var anchoGrupo = anchoImporte * columnas.Length;

            if (!string.IsNullOrWhiteSpace(titulo))
            {
                banda.Controls.Add(new XRLabel
                {
                    BoundsF = new RectangleF(x, 0f, anchoGrupo, 15f),
                    Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
                    Text = titulo,
                    TextAlignment = TextAlignment.MiddleCenter,
                });
            }

            var xColumna = x;
            foreach (var expresion in columnas)
            {
                var etiqueta = new XRLabel
                {
                    BoundsF = new RectangleF(xColumna, 16f, anchoImporte, 15f),
                    Font = new DXFont("Arial", 8.5f, DXFontStyle.Bold),
                    TextAlignment = TextAlignment.MiddleRight,
                    Padding = new PaddingInfo(0, 6, 0, 0),
                };
                etiqueta.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expresion));

                banda.Controls.Add(etiqueta);
                xColumna += anchoImporte;
            }

            x += anchoGrupo;
        }

        return banda;
    }

    /// <summary>
    /// Variación entre los dos ejercicios. Se calcula en el reporte y no en la base: los dos
    /// importes ya vienen en la misma fila, así que pedirle a la consulta que reste sería
    /// duplicar la regla en dos sitios.
    /// </summary>
    public static string ExpresionVariacionRelativa(string actual, string anterior)
        => $"{actual} - {anterior}";

    /// <summary>
    /// La variación en porcentaje. Con el ejercicio anterior en cero no hay porcentaje que
    /// calcular —sería una división por cero—, y el estado lo deja vacío.
    /// </summary>
    public static string ExpresionVariacionPorcentual(string actual, string anterior)
        => $"Iif({anterior} == 0, null, Round((({actual} - {anterior}) / {anterior}) * 100))";

    // -------------------------------------------------------------------------
    // Celdas
    // -------------------------------------------------------------------------

    /// <summary>Celda de importe: derecha, negativos entre paréntesis, sin bordes.</summary>
    public static XRTableCell CeldaImporte(string expresion, float peso)
    {
        var celda = new XRTableCell
        {
            Weight = peso,
            Borders = BorderSide.None,
            TextAlignment = TextAlignment.MiddleRight,
            Padding = new PaddingInfo(0, 6, 0, 0),
            TextFormatString = FormatoMonto,
        };

        celda.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expresion));
        return celda;
    }

    /// <summary>Celda de porcentaje: entero y sin separador de miles, como en el juego impreso.</summary>
    public static XRTableCell CeldaPorcentaje(string expresion, float peso)
    {
        var celda = new XRTableCell
        {
            Weight = peso,
            Borders = BorderSide.None,
            TextAlignment = TextAlignment.MiddleRight,
            Padding = new PaddingInfo(0, 6, 0, 0),
            TextFormatString = "{0:0;(0);}",
        };

        celda.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expresion));
        return celda;
    }

    /// <summary>
    /// Celda de concepto. La sangría se toma del dato (<c>nivel_indentacion</c>) en vez de
    /// fijarse aquí: es la configuración contable la que decide qué cuelga de qué.
    /// </summary>
    public static XRTableCell CeldaConcepto(string expresion, float peso, string? expresionSangria = null)
    {
        var celda = new XRTableCell
        {
            Weight = peso,
            Borders = BorderSide.None,
            TextAlignment = TextAlignment.MiddleLeft,
            Padding = new PaddingInfo(0, 8, 0, 0),
        };

        celda.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expresion));

        if (!string.IsNullOrWhiteSpace(expresionSangria))
        {
            celda.ExpressionBindings.Add(new ExpressionBinding(
                "BeforePrint", "Padding", $"Padding(8 + ({expresionSangria}) * 12, 6, 0, 0, 100)"));
        }

        return celda;
    }

    /// <summary>Pone en negrita las filas que son subtotal o total.</summary>
    public static void MarcarComoTotal(XRTableRow fila, string expresionEsTotal)
    {
        ArgumentNullException.ThrowIfNull(fila);

        foreach (XRTableCell celda in fila.Cells)
        {
            celda.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Font.Bold", expresionEsTotal));
        }
    }

    /// <summary>
    /// La línea que va ENCIMA de un total, sobre las columnas de importe.
    ///
    /// Es una línea propia y no el borde superior de las celdas porque enlazar <c>Borders</c> a
    /// una expresión obliga a producir el enum desde texto y no es fiable entre versiones;
    /// <c>Visible</c>, en cambio, se enlaza sin sorpresas.
    /// </summary>
    public static XRLine LineaSobreTotal(
        float anchoDescripcion, float anchoImporte, int columnas, string expresionEsTotal)
    {
        var linea = new XRLine
        {
            BoundsF = new RectangleF(anchoDescripcion, 0f, anchoImporte * columnas, 2f),
            ForeColor = LineaTotal,
            LineWidth = 1f,
        };

        linea.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Visible", expresionEsTotal));
        return linea;
    }

    /// <summary>
    /// Pie de grupo con la fila de suma: el rótulo a la izquierda y los totales alineados con
    /// sus columnas, en negrita y con línea encima.
    ///
    /// Las sumas las calcula el propio reporte con <c>XRSummary</c> sobre el grupo. Es lo que
    /// permite tener subtotales sin que la consulta devuelva filas de total: la base entrega
    /// cuentas y el reporte las suma por donde las agrupa.
    /// </summary>
    public static GroupFooterBand CrearPieDeGrupo(
        string expresionRotulo,
        float anchoDescripcion,
        float anchoImporte,
        bool conVariacion,
        params string[] camposASumar)
    {
        var banda = new GroupFooterBand { HeightF = 20f };

        var columnas = conVariacion ? camposASumar.Length + 1 : camposASumar.Length;
        banda.Controls.Add(LineaSobreTotal(anchoDescripcion, anchoImporte, columnas, "true"));

        var rotulo = new XRLabel
        {
            BoundsF = new RectangleF(0f, 3f, anchoDescripcion, 15f),
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
            Padding = new PaddingInfo(8, 0, 0, 0),
            TextAlignment = TextAlignment.MiddleLeft,
        };
        rotulo.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expresionRotulo));
        banda.Controls.Add(rotulo);

        var x = anchoDescripcion;
        foreach (var campo in camposASumar)
        {
            var celda = new XRLabel
            {
                BoundsF = new RectangleF(x, 3f, anchoImporte, 15f),
                Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
                TextAlignment = TextAlignment.MiddleRight,
                Padding = new PaddingInfo(0, 6, 0, 0),
                TextFormatString = FormatoMonto,
            };

            celda.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"sumSum({campo})"));
            celda.Summary = new XRSummary
            {
                Running = SummaryRunning.Group,
                Func = SummaryFunc.Sum,
                IgnoreNullValues = true,
            };

            banda.Controls.Add(celda);
            x += anchoImporte;
        }

        // La variacion de la fila de suma se expresa como SUMA DE DIFERENCIAS y no como resta de
        // sumas. El resultado es identico -sumar (a-b) es lo mismo que restar las sumas- pero el
        // motor solo resuelve el agregado cuando la celda tiene su XRSummary, y ese mecanismo
        // trabaja sobre UNA expresion: restar dos sumSum sueltos deja la celda en blanco.
        //
        // Por eso la columna porcentual no se pone aqui: es el cociente de dos totales, y eso no
        // cabe en un solo agregado. Antes que imprimir un porcentaje calculado de otra manera
        // -que no cuadraria con el de las lineas- la fila de suma lo deja vacio.
        if (conVariacion && camposASumar.Length == 2)
        {
            banda.Controls.Add(CeldaSumada(
                x, anchoImporte, $"{camposASumar[0]} - {camposASumar[1]}", FormatoMonto));
        }

        return banda;
    }

    /// <summary>Etiqueta cuyo valor es la suma del grupo sobre la expresion indicada.</summary>
    private static XRLabel CeldaSumada(float x, float ancho, string expresion, string formato)
    {
        var etiqueta = new XRLabel
        {
            BoundsF = new RectangleF(x, 3f, ancho, 15f),
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight,
            Padding = new PaddingInfo(0, 6, 0, 0),
            TextFormatString = formato,
        };

        etiqueta.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"sumSum({expresion})"));
        etiqueta.Summary = new XRSummary
        {
            Running = SummaryRunning.Group,
            Func = SummaryFunc.Sum,
            IgnoreNullValues = true,
        };

        return etiqueta;
    }

    // -------------------------------------------------------------------------
    // Utilidades
    // -------------------------------------------------------------------------

    private static XRLabel LineaTitulo(string expresion, float y, float tamano, DXFontStyle estilo)
    {
        var etiqueta = new XRLabel
        {
            BoundsF = new RectangleF(0f, y, AnchoContenido, 15f),
            Font = new DXFont("Arial", tamano, estilo),
            TextAlignment = TextAlignment.MiddleCenter,
        };

        etiqueta.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expresion));
        return etiqueta;
    }

    private static XRShape Banda(float x, float y, float ancho, float alto, Color color)
        => new()
        {
            BoundsF = new RectangleF(x, y, ancho, alto),
            Shape = new ShapeRectangle(),
            FillColor = color,
            ForeColor = color,
            LineWidth = 0,
            Stretch = true,
        };

    private static TBanda? ObtenerBanda<TBanda>(XtraReport report) where TBanda : Band, new()
    {
        foreach (Band banda in report.Bands)
        {
            if (banda is TBanda encontrada)
            {
                return encontrada;
            }
        }

        var nueva = new TBanda();
        report.Bands.Add(nueva);
        return nueva;
    }

    /// <summary>
    /// El logo vive en <c>cfg_company.logo</c> como bytes. Si no se puede decodificar se sigue
    /// sin él: un membrete sin logo es mejor que un reporte que no imprime.
    /// </summary>
    private static Image? LeerLogo(cfg_company? empresa)
    {
        if (empresa?.logo is null || empresa.logo.Length == 0)
        {
            return null;
        }

        try
        {
            // Image.FromStream deja la imagen ATADA al stream: si se cierra, la imagen queda
            // invalida y no dibuja nada —sin lanzar—. Por eso se copia a un Bitmap propio y
            // recien entonces se suelta el stream.
            using var memoria = new MemoryStream(empresa.logo);
            using var original = Image.FromStream(memoria);
            return new Bitmap(original);
        }
        catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or NotSupportedException)
        {
            return null;
        }
    }
}
