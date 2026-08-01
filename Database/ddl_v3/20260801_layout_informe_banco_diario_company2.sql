-- =============================================================================
-- Layout DISEÑADO del Informe de banco diario (company_id = 2).
--
-- El primer viewer persistió la plantilla esqueleto de ReportTemplateFactory
-- (2,185 páginas de placeholder). Este script publica un diseño real:
-- horizontal carta, encabezado de empresa dinámico, título con el rango
-- EFECTIVO ([periodo_titulo] — muestra el tope de 31 días), tabla de detalle
-- (fecha, recibo, clave, cliente, canal, forma, banco, cuenta, caja, monto)
-- y totales al pie (recibos, efectivo, banco, total general).
--
-- Estructura espejada del layout publicado del estado de flujos de efectivo;
-- SqlDataSource embebido SIN parámetros de conexión (FromAppConfig, regla del
-- repo). Reemplaza TODAS las versiones existentes del layout del informe
-- 'banco-diario' (esqueletos previos incluidos). Idempotente.
-- =============================================================================

BEGIN;

DO $$
DECLARE
    v_company_id bigint := 2;
    v_informe_id bigint;
    v_layout text := $repx$<?xml version="1.0" encoding="utf-8"?>
<XtraReportsLayoutSerializer SerializerVersion="25.2.4.0" Ref="1" ControlType="DevExpress.XtraReports.UI.XtraReport, DevExpress.XtraReports.v25.2, Version=25.2.4.0, Culture=neutral, PublicKeyToken=b88d1754d700e49a" Name="banco-diario" DisplayName="Informe de banco diario" Margins="40, 40, 35, 35" Landscape="true" PageWidthF="1100" PageHeightF="850" Version="25.2" DataMember="banco_diario" DataSource="#Ref-0">
  <Parameters>
    <Item1 Ref="3" Visible="false" Description="Empresa del encabezado" ValueInfo="Empresa de Agua y Saneamiento S.A de C.V" AllowNull="true" Name="HeaderCompanyName" />
    <Item2 Ref="4" Visible="false" Description="Datos fiscales/contacto del encabezado" ValueInfo="" AllowNull="true" Name="HeaderCompanyInfoLine" />
    <Item3 Ref="5" Visible="false" Description="Direccion del encabezado" ValueInfo="" AllowNull="true" Name="HeaderCompanyAddress" />
    <Item4 Ref="7" Visible="false" Description="Empresa actual" ValueInfo="2" Name="CompanyId" Type="#Ref-6" />
    <Item5 Ref="9" Description="Fecha desde" ValueInfo="2026-07-31" Name="FechaDesde" Type="#Ref-8" />
    <Item6 Ref="10" Description="Fecha hasta" ValueInfo="2026-07-31" Name="FechaHasta" Type="#Ref-8" />
  </Parameters>
  <Bands>
    <Item1 Ref="11" ControlType="TopMarginBand" HeightF="35" />
    <Item2 Ref="12" ControlType="BottomMarginBand" HeightF="35" />
    <Item3 Ref="13" ControlType="ReportHeaderBand" HeightF="96">
      <Controls>
        <Item1 Ref="14" ControlType="XRLabel" TextAlignment="MiddleCenter" SizeF="1020,22" LocationFloat="0,0" Font="Arial, 11pt, style=Bold">
          <ExpressionBindings>
            <Item1 Ref="15" EventName="BeforePrint" PropertyName="Text" Expression="?HeaderCompanyName" />
          </ExpressionBindings>
        </Item1>
        <Item2 Ref="16" ControlType="XRLabel" TextAlignment="MiddleCenter" SizeF="1020,24" LocationFloat="0,24" Font="Arial, 12pt, style=Bold">
          <ExpressionBindings>
            <Item1 Ref="17" EventName="BeforePrint" PropertyName="Text" Expression="Upper([periodo_titulo])" />
          </ExpressionBindings>
        </Item2>
        <Item3 Ref="18" ControlType="XRLabel" TextAlignment="MiddleCenter" SizeF="1020,16" LocationFloat="0,52" Font="Arial, 8pt" ForeColor="DimGray">
          <ExpressionBindings>
            <Item1 Ref="19" EventName="BeforePrint" PropertyName="Text" Expression="?HeaderCompanyInfoLine" />
          </ExpressionBindings>
        </Item3>
        <Item4 Ref="20" ControlType="XRLabel" TextAlignment="MiddleCenter" SizeF="1020,16" LocationFloat="0,68" Font="Arial, 8pt" ForeColor="DimGray">
          <ExpressionBindings>
            <Item1 Ref="21" EventName="BeforePrint" PropertyName="Text" Expression="?HeaderCompanyAddress" />
          </ExpressionBindings>
        </Item4>
      </Controls>
    </Item3>
    <Item4 Ref="22" ControlType="PageHeaderBand" HeightF="26">
      <Controls>
        <Item1 Ref="23" ControlType="XRTable" TextAlignment="MiddleCenter" SizeF="1020,24" LocationFloat="0,0" Font="Arial, 8.5pt, style=Bold" BackColor="Gainsboro" BorderWidth="1" Borders="Bottom">
          <Rows>
            <Item1 Ref="24" ControlType="XRTableRow" Weight="1">
              <Cells>
                <Item1 Ref="25" ControlType="XRTableCell" Weight="80" Text="Fecha" />
                <Item2 Ref="26" ControlType="XRTableCell" Weight="110" Text="Recibo" />
                <Item3 Ref="27" ControlType="XRTableCell" Weight="85" Text="Clave" />
                <Item4 Ref="28" ControlType="XRTableCell" Weight="220" Text="Cliente" TextAlignment="MiddleLeft" Padding="4,0,0,0,96" />
                <Item5 Ref="29" ControlType="XRTableCell" Weight="75" Text="Canal" />
                <Item6 Ref="30" ControlType="XRTableCell" Weight="70" Text="Forma" />
                <Item7 Ref="31" ControlType="XRTableCell" Weight="110" Text="Banco" />
                <Item8 Ref="32" ControlType="XRTableCell" Weight="100" Text="Cuenta" />
                <Item9 Ref="33" ControlType="XRTableCell" Weight="80" Text="Caja" />
                <Item10 Ref="34" ControlType="XRTableCell" Weight="90" Text="Monto" TextAlignment="MiddleRight" Padding="0,4,0,0,96" />
              </Cells>
            </Item1>
          </Rows>
        </Item1>
      </Controls>
    </Item4>
    <Item5 Ref="35" ControlType="DetailBand" HeightF="20">
      <Controls>
        <Item1 Ref="36" ControlType="XRTable" SizeF="1020,20" LocationFloat="0,0" Font="Arial, 8pt" BorderWidth="0">
          <Rows>
            <Item1 Ref="37" ControlType="XRTableRow" Weight="1">
              <Cells>
                <Item1 Ref="38" ControlType="XRTableCell" Weight="80" TextAlignment="MiddleCenter" TextFormatString="{0:dd/MM/yyyy}">
                  <ExpressionBindings>
                    <Item1 Ref="39" EventName="BeforePrint" PropertyName="Text" Expression="[fecha]" />
                  </ExpressionBindings>
                </Item1>
                <Item2 Ref="40" ControlType="XRTableCell" Weight="110" TextAlignment="MiddleCenter">
                  <ExpressionBindings>
                    <Item1 Ref="41" EventName="BeforePrint" PropertyName="Text" Expression="[numero_recibo]" />
                  </ExpressionBindings>
                </Item2>
                <Item3 Ref="42" ControlType="XRTableCell" Weight="85" TextAlignment="MiddleCenter">
                  <ExpressionBindings>
                    <Item1 Ref="43" EventName="BeforePrint" PropertyName="Text" Expression="[cliente_clave]" />
                  </ExpressionBindings>
                </Item3>
                <Item4 Ref="44" ControlType="XRTableCell" Weight="220" TextAlignment="MiddleLeft" Padding="4,0,0,0,96">
                  <ExpressionBindings>
                    <Item1 Ref="45" EventName="BeforePrint" PropertyName="Text" Expression="[cliente_nombre]" />
                  </ExpressionBindings>
                </Item4>
                <Item5 Ref="46" ControlType="XRTableCell" Weight="75" TextAlignment="MiddleCenter">
                  <ExpressionBindings>
                    <Item1 Ref="47" EventName="BeforePrint" PropertyName="Text" Expression="[canal]" />
                  </ExpressionBindings>
                </Item5>
                <Item6 Ref="48" ControlType="XRTableCell" Weight="70" TextAlignment="MiddleCenter">
                  <ExpressionBindings>
                    <Item1 Ref="49" EventName="BeforePrint" PropertyName="Text" Expression="[forma_pago]" />
                  </ExpressionBindings>
                </Item6>
                <Item7 Ref="50" ControlType="XRTableCell" Weight="110" TextAlignment="MiddleLeft" Padding="4,0,0,0,96">
                  <ExpressionBindings>
                    <Item1 Ref="51" EventName="BeforePrint" PropertyName="Text" Expression="[banco]" />
                  </ExpressionBindings>
                </Item7>
                <Item8 Ref="52" ControlType="XRTableCell" Weight="100" TextAlignment="MiddleCenter">
                  <ExpressionBindings>
                    <Item1 Ref="53" EventName="BeforePrint" PropertyName="Text" Expression="[cuenta_bancaria]" />
                  </ExpressionBindings>
                </Item8>
                <Item9 Ref="54" ControlType="XRTableCell" Weight="80" TextAlignment="MiddleCenter">
                  <ExpressionBindings>
                    <Item1 Ref="55" EventName="BeforePrint" PropertyName="Text" Expression="[caja]" />
                  </ExpressionBindings>
                </Item9>
                <Item10 Ref="56" ControlType="XRTableCell" Weight="90" TextAlignment="MiddleRight" Padding="0,4,0,0,96" TextFormatString="{0:n2}" Borders="Bottom" BorderWidth="1" BorderColor="Gainsboro">
                  <ExpressionBindings>
                    <Item1 Ref="57" EventName="BeforePrint" PropertyName="Text" Expression="[monto]" />
                  </ExpressionBindings>
                </Item10>
              </Cells>
            </Item1>
          </Rows>
        </Item1>
      </Controls>
    </Item5>
    <Item6 Ref="58" ControlType="ReportFooterBand" HeightF="96">
      <Controls>
        <Item1 Ref="59" ControlType="XRLabel" Text="Recibos:" TextAlignment="MiddleRight" SizeF="120,18" LocationFloat="640,8" Font="Arial, 8.5pt" />
        <Item2 Ref="60" ControlType="XRLabel" TextAlignment="MiddleRight" SizeF="250,18" LocationFloat="770,8" Font="Arial, 8.5pt">
          <Summary Ref="61" Running="Report" />
          <ExpressionBindings>
            <Item1 Ref="62" EventName="BeforePrint" PropertyName="Text" Expression="sumCount([numero_recibo])" />
          </ExpressionBindings>
        </Item2>
        <Item3 Ref="63" ControlType="XRLabel" Text="Total efectivo:" TextAlignment="MiddleRight" SizeF="120,18" LocationFloat="640,28" Font="Arial, 8.5pt" />
        <Item4 Ref="64" ControlType="XRLabel" TextAlignment="MiddleRight" SizeF="250,18" LocationFloat="770,28" Font="Arial, 8.5pt" TextFormatString="{0:n2}">
          <Summary Ref="65" Running="Report" />
          <ExpressionBindings>
            <Item1 Ref="66" EventName="BeforePrint" PropertyName="Text" Expression="sumSum(Iif([forma_pago] == &apos;EFECTIVO&apos;, [monto], 0))" />
          </ExpressionBindings>
        </Item4>
        <Item5 Ref="67" ControlType="XRLabel" Text="Total banco:" TextAlignment="MiddleRight" SizeF="120,18" LocationFloat="640,48" Font="Arial, 8.5pt" />
        <Item6 Ref="68" ControlType="XRLabel" TextAlignment="MiddleRight" SizeF="250,18" LocationFloat="770,48" Font="Arial, 8.5pt" TextFormatString="{0:n2}">
          <Summary Ref="69" Running="Report" />
          <ExpressionBindings>
            <Item1 Ref="70" EventName="BeforePrint" PropertyName="Text" Expression="sumSum(Iif([forma_pago] == &apos;BANCO&apos;, [monto], 0))" />
          </ExpressionBindings>
        </Item6>
        <Item7 Ref="71" ControlType="XRLabel" Text="TOTAL GENERAL:" TextAlignment="MiddleRight" SizeF="120,20" LocationFloat="640,70" Font="Arial, 9pt, style=Bold" />
        <Item8 Ref="72" ControlType="XRLabel" TextAlignment="MiddleRight" SizeF="250,20" LocationFloat="770,70" Font="Arial, 9pt, style=Bold" TextFormatString="{0:n2}" Borders="Top" BorderWidth="1">
          <Summary Ref="73" Running="Report" />
          <ExpressionBindings>
            <Item1 Ref="74" EventName="BeforePrint" PropertyName="Text" Expression="sumSum([monto])" />
          </ExpressionBindings>
        </Item8>
      </Controls>
    </Item6>
    <Item7 Ref="75" ControlType="PageFooterBand" HeightF="24">
      <Controls>
        <Item1 Ref="76" ControlType="XRPageInfo" PageInfo="DateTime" TextFormatString="Generado: {0:dd/MM/yyyy HH:mm}" TextAlignment="MiddleLeft" SizeF="260,20" LocationFloat="0,0" Font="Arial, 8pt" />
        <Item2 Ref="77" ControlType="XRPageInfo" TextFormatString="Pagina {0} de {1}" TextAlignment="MiddleRight" SizeF="200,20" LocationFloat="820,0" Font="Arial, 8pt" />
      </Controls>
    </Item7>
  </Bands>
  <ComponentStorage>
    <Item1 Ref="0" ObjectType="DevExpress.DataAccess.Sql.SqlDataSource,DevExpress.DataAccess.v25.2" Name="banco_diarioDataSource" Base64="PFNxbERhdGFTb3VyY2UgTmFtZT0iYmFuY29fZGlhcmlvRGF0YVNvdXJjZSI+PENvbm5lY3Rpb24gTmFtZT0iRGVmYXVsdENvbm5lY3Rpb24iIEZyb21BcHBDb25maWc9InRydWUiIC8+PFF1ZXJ5IFR5cGU9IkN1c3RvbVNxbFF1ZXJ5IiBOYW1lPSJiYW5jb19kaWFyaW8iPjxQYXJhbWV0ZXIgTmFtZT0icF9jb21wYW55X2lkIiBUeXBlPSJEZXZFeHByZXNzLkRhdGFBY2Nlc3MuRXhwcmVzc2lvbiI+KFN5c3RlbS5JbnQ2NCkoP0NvbXBhbnlJZCk8L1BhcmFtZXRlcj48UGFyYW1ldGVyIE5hbWU9InBfZmVjaGFfZGVzZGUiIFR5cGU9IkRldkV4cHJlc3MuRGF0YUFjY2Vzcy5FeHByZXNzaW9uIj4oU3lzdGVtLkRhdGVUaW1lKSg/RmVjaGFEZXNkZSk8L1BhcmFtZXRlcj48UGFyYW1ldGVyIE5hbWU9InBfZmVjaGFfaGFzdGEiIFR5cGU9IkRldkV4cHJlc3MuRGF0YUFjY2Vzcy5FeHByZXNzaW9uIj4oU3lzdGVtLkRhdGVUaW1lKSg/RmVjaGFIYXN0YSk8L1BhcmFtZXRlcj48U3FsPlNFTEVDVCAqIEZST00gcHVibGljLnJlcF9iYW5jb19kaWFyaW8oQ0FTVChAcF9jb21wYW55X2lkIEFTIGJpZ2ludCksIENBU1QoQHBfZmVjaGFfZGVzZGUgQVMgZGF0ZSksIENBU1QoQHBfZmVjaGFfaGFzdGEgQVMgZGF0ZSkpPC9TcWw+PC9RdWVyeT48UmVzdWx0U2NoZW1hPjxEYXRhU2V0IE5hbWU9ImJhbmNvX2RpYXJpb0RhdGFTb3VyY2UiPjxWaWV3IE5hbWU9ImJhbmNvX2RpYXJpbyI+PEZpZWxkIE5hbWU9ImZpbGFfb3JkZW4iIFR5cGU9IkludDY0IiAvPjxGaWVsZCBOYW1lPSJmZWNoYSIgVHlwZT0iRGF0ZVRpbWUiIC8+PEZpZWxkIE5hbWU9Im51bWVyb19yZWNpYm8iIFR5cGU9IlN0cmluZyIgLz48RmllbGQgTmFtZT0iY2xpZW50ZV9jbGF2ZSIgVHlwZT0iU3RyaW5nIiAvPjxGaWVsZCBOYW1lPSJjbGllbnRlX25vbWJyZSIgVHlwZT0iU3RyaW5nIiAvPjxGaWVsZCBOYW1lPSJjYW5hbCIgVHlwZT0iU3RyaW5nIiAvPjxGaWVsZCBOYW1lPSJmb3JtYV9wYWdvIiBUeXBlPSJTdHJpbmciIC8+PEZpZWxkIE5hbWU9ImJhbmNvIiBUeXBlPSJTdHJpbmciIC8+PEZpZWxkIE5hbWU9ImN1ZW50YV9iYW5jYXJpYSIgVHlwZT0iU3RyaW5nIiAvPjxGaWVsZCBOYW1lPSJjYWphIiBUeXBlPSJTdHJpbmciIC8+PEZpZWxkIE5hbWU9ImNhamVybyIgVHlwZT0iU3RyaW5nIiAvPjxGaWVsZCBOYW1lPSJtb250byIgVHlwZT0iRGVjaW1hbCIgLz48RmllbGQgTmFtZT0iZW1wcmVzYV9ub21icmUiIFR5cGU9IlN0cmluZyIgLz48RmllbGQgTmFtZT0icGVyaW9kb190aXR1bG8iIFR5cGU9IlN0cmluZyIgLz48RmllbGQgTmFtZT0iZmVjaGFfZGVzZGUiIFR5cGU9IkRhdGVUaW1lIiAvPjxGaWVsZCBOYW1lPSJmZWNoYV9oYXN0YSIgVHlwZT0iRGF0ZVRpbWUiIC8+PEZpZWxkIE5hbWU9ImZlY2hhX3JlcG9ydGUiIFR5cGU9IkRhdGVUaW1lIiAvPjxGaWVsZCBOYW1lPSJmZWNoYV9yZXBvcnRlX3RleHRvIiBUeXBlPSJTdHJpbmciIC8+PC9WaWV3PjwvRGF0YVNldD48L1Jlc3VsdFNjaGVtYT48Q29ubmVjdGlvbk9wdGlvbnMgQ2xvc2VDb25uZWN0aW9uPSJ0cnVlIiAvPjwvU3FsRGF0YVNvdXJjZT4=" />
  </ComponentStorage>
  <ObjectStorage>
    <Item1 ObjectType="DevExpress.XtraReports.Serialization.ObjectStorageInfo, DevExpress.XtraReports.v25.2" Ref="6" Content="System.Int64" Type="System.Type" />
    <Item2 ObjectType="DevExpress.XtraReports.Serialization.ObjectStorageInfo, DevExpress.XtraReports.v25.2" Ref="8" Content="System.DateTime" Type="System.Type" />
  </ObjectStorage>
</XtraReportsLayoutSerializer>
$repx$;
BEGIN
    SELECT informe_id INTO v_informe_id
    FROM public.rep_catalogo_informe
    WHERE company_id = v_company_id AND codigo = 'banco-diario';

    IF v_informe_id IS NULL THEN
        RAISE EXCEPTION 'No existe el informe banco-diario. Ejecute antes 20260731_registro_informe_banco_diario_company2.sql';
    END IF;

    -- Fuera los esqueletos previos (DRAFT/PUBLISHED del template factory).
    DELETE FROM public.rep_reporte_layout
    WHERE company_id = v_company_id AND informe_id = v_informe_id;

    INSERT INTO public.rep_reporte_layout (
        company_id, informe_id, version_num, estado, layout_xml,
        created_at, created_by, published_at, published_by
    )
    VALUES (
        v_company_id, v_informe_id, 1, 'PUBLISHED', v_layout,
        now(), 'banco-diario-layout', now(), 'banco-diario-layout'
    );
END $$;

COMMIT;
