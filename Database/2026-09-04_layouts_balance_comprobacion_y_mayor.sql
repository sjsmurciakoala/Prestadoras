-- El balance de comprobacion y el mayor analitico publican su diseno nuevo (2026-09-04).
--
-- POR QUE HACE FALTA UN SCRIPT Y NO BASTA PUBLICAR EL BINARIO:
--
-- CompanyReportStorageWebExtension.GetData lee el layout de rep_reporte_layout y solo llama a
-- ReportTemplateFactory cuando NO hay ninguno guardado. Los dos tienen layout publicado desde
-- junio y julio, asi que el rediseno hecho en codigo no se veria: el visor seguiria sirviendo el
-- XML viejo de la base.
--
-- Los cuatro estados financieros ya publicaron el suyo el 2026-09-03; este script no los toca.
--
-- Este script archiva la version publicada de cada estado y agrega una nueva con el diseno que
-- genera el codigo actual. Es lo mismo que hace el disenador web al publicar: no borra nada y se
-- puede volver atras reactivando la version anterior.
--
-- El XML sale de ReportTemplateFactory, volcado con la prueba
-- EstadoFinancieroLayoutTests.Vuelca_los_layouts_para_regenerar_los_publicados. Si el diseno
-- vuelve a cambiar, se regenera con ella y se rehace este script; no se edita a mano.
--
-- APLICA A TODAS LAS EMPRESAS que tengan el informe registrado.

BEGIN;

-- ---------------------------------------------------------------- Balance de comprobacion
UPDATE public.rep_reporte_layout l
   SET estado = 'ARCHIVED',
       updated_at = now(),
       updated_by = 'rediseno-estados-financieros'
  FROM public.rep_catalogo_informe i
 WHERE i.informe_id = l.informe_id
   AND i.codigo = 'balance-comprobacion'
   AND l.estado = 'PUBLISHED';

INSERT INTO public.rep_reporte_layout
    (company_id, informe_id, version_num, estado, layout_xml,
     created_at, created_by, published_at, published_by)
SELECT i.company_id,
       i.informe_id,
       COALESCE((SELECT MAX(v.version_num) FROM public.rep_reporte_layout v
                  WHERE v.informe_id = i.informe_id), 0) + 1,
       'PUBLISHED',
       '﻿<?xml version="1.0" encoding="utf-8"?>
<XtraReportsLayoutSerializer SerializerVersion="25.2.4.0" Ref="1" ControlType="DevExpress.XtraReports.UI.XtraReport, DevExpress.XtraReports.v25.2, Version=25.2.4.0, Culture=neutral, PublicKeyToken=b88d1754d700e49a" Name="balance-comprobacion" DisplayName="Balance de comprobacion" Landscape="true" Margins="40, 40, 78, 58" PageWidthF="1100" PageHeightF="850" Version="25.2" DataMember="balance_comprobacion" DataSource="#Ref-0">
  <Parameters>
    <Item1 Ref="3" Visible="false" Description="Empresa del encabezado" ValueInfo="Empresa de Agua y Saneamiento S.A de C.V" AllowNull="true" Name="HeaderCompanyName" />
    <Item2 Ref="4" Visible="false" Description="Datos fiscales/contacto del encabezado" ValueInfo="RTN: R.T.N-05069999182490 | Tel: +504 26271450 / 26271451 | administracion@aguasdepuertocortes.com" AllowNull="true" Name="HeaderCompanyInfoLine" />
    <Item3 Ref="5" Visible="false" Description="Direccion del encabezado" ValueInfo="Bo. Copen 9 calle este, 5 y 6 ave Planta baja del estadio Excelsior" AllowNull="true" Name="HeaderCompanyAddress" />
    <Item4 Ref="7" Visible="false" Description="Empresa actual" ValueInfo="2" Name="CompanyId" Type="#Ref-6" />
    <Item5 Ref="9" Description="Fecha desde" ValueInfo="2026-09-01" Name="FechaDesde" Type="#Ref-8" />
    <Item6 Ref="10" Description="Fecha hasta" ValueInfo="2026-09-04" Name="FechaHasta" Type="#Ref-8" />
    <Item7 Ref="12" Description="Incluir cuentas sin movimiento" ValueInfo="False" Name="IncluirSinMovimiento" Type="#Ref-11" />
  </Parameters>
  <Bands>
    <Item1 Ref="13" ControlType="TopMarginBand" HeightF="78">
      <Controls>
        <Item1 Ref="14" ControlType="XRPictureBox" ImageSource="img,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" Sizing="ZoomImage" ImageAlignment="MiddleRight" SizeF="190,40" LocationFloat="560,2" />
        <Item2 Ref="15" ControlType="XRShape" LineWidth="0" Stretch="true" FillColor="255,140,198,63" SizeF="450.00003,12" LocationFloat="0,52" ForeColor="255,140,198,63">
          <Shape Ref="16" ShapeName="Rectangle" />
        </Item2>
        <Item3 Ref="17" ControlType="XRShape" LineWidth="0" Stretch="true" FillColor="255,43,163,199" SizeF="375,16" LocationFloat="375,48" ForeColor="255,43,163,199">
          <Shape Ref="18" ShapeName="Rectangle" />
        </Item3>
      </Controls>
    </Item1>
    <Item2 Ref="19" ControlType="BottomMarginBand" HeightF="58">
      <Controls>
        <Item1 Ref="20" ControlType="XRShape" LineWidth="0" Stretch="true" FillColor="255,43,163,199" SizeF="315,16" LocationFloat="0,26" ForeColor="255,43,163,199">
          <Shape Ref="21" ShapeName="Rectangle" />
        </Item1>
        <Item2 Ref="22" ControlType="XRShape" LineWidth="0" Stretch="true" FillColor="255,140,198,63" SizeF="525,14" LocationFloat="225.00002,34" ForeColor="255,140,198,63">
          <Shape Ref="23" ShapeName="Rectangle" />
        </Item2>
        <Item3 Ref="24" ControlType="XRPageInfo" PageInfo="Number" TextFormatString="{0}" TextAlignment="MiddleRight" SizeF="60,16" LocationFloat="690,4" Font="Arial, 9pt" ForeColor="255,60,60,60" />
      </Controls>
    </Item2>
    <Item3 Ref="25" ControlType="ReportHeaderBand" HeightF="104">
      <Controls>
        <Item1 Ref="26" ControlType="XRLabel" TextAlignment="MiddleCenter" SizeF="750,15" LocationFloat="0,0" Font="Arial, 11pt, style=Bold">
          <ExpressionBindings>
            <Item1 Ref="27" EventName="BeforePrint" PropertyName="Text" Expression="[empresa_nombre]" />
          </ExpressionBindings>
        </Item1>
        <Item2 Ref="28" ControlType="XRLabel" TextAlignment="MiddleCenter" SizeF="750,15" LocationFloat="0,15" Font="Arial, 11pt, style=Bold">
          <ExpressionBindings>
            <Item1 Ref="29" EventName="BeforePrint" PropertyName="Text" Expression="[empresa_nombre_legal]" />
          </ExpressionBindings>
        </Item2>
        <Item3 Ref="30" ControlType="XRLabel" TextAlignment="MiddleCenter" SizeF="750,15" LocationFloat="0,30" Font="Arial, 11pt, style=Bold">
          <ExpressionBindings>
            <Item1 Ref="31" EventName="BeforePrint" PropertyName="Text" Expression="[empresa_direccion]" />
          </ExpressionBindings>
        </Item3>
        <Item4 Ref="32" ControlType="XRLabel" Text="BALANCE DE COMPROBACION" TextAlignment="MiddleCenter" SizeF="750,15" LocationFloat="0,45" Font="Arial, 11pt, style=Bold" />
        <Item5 Ref="33" ControlType="XRLabel" Text="(Expresado en lempiras)" TextAlignment="MiddleCenter" SizeF="750,15" LocationFloat="0,60" Font="Arial, 9pt, style=Bold" />
      </Controls>
    </Item3>
    <Item4 Ref="34" ControlType="PageHeaderBand" HeightF="34">
      <Controls>
        <Item1 Ref="35" ControlType="XRLabel" Text="SALDO ANTERIOR" TextAlignment="MiddleCenter" SizeF="210,15" LocationFloat="350,0" Font="Arial, 9pt, style=Bold" />
        <Item2 Ref="36" ControlType="XRLabel" TextAlignment="MiddleRight" SizeF="105,15" LocationFloat="350,16" Font="Arial, 8.5pt, style=Bold" Padding="0,6,0,0,100">
          <ExpressionBindings>
            <Item1 Ref="37" EventName="BeforePrint" PropertyName="Text" Expression="''Deudor''" />
          </ExpressionBindings>
        </Item2>
        <Item3 Ref="38" ControlType="XRLabel" TextAlignment="MiddleRight" SizeF="105,15" LocationFloat="455,16" Font="Arial, 8.5pt, style=Bold" Padding="0,6,0,0,100">
          <ExpressionBindings>
            <Item1 Ref="39" EventName="BeforePrint" PropertyName="Text" Expression="''Acreedor''" />
          </ExpressionBindings>
        </Item3>
        <Item4 Ref="40" ControlType="XRLabel" Text="MOVIMIENTOS DEL PERIODO" TextAlignment="MiddleCenter" SizeF="210,15" LocationFloat="560,0" Font="Arial, 9pt, style=Bold" />
        <Item5 Ref="41" ControlType="XRLabel" TextAlignment="MiddleRight" SizeF="105,15" LocationFloat="560,16" Font="Arial, 8.5pt, style=Bold" Padding="0,6,0,0,100">
          <ExpressionBindings>
            <Item1 Ref="42" EventName="BeforePrint" PropertyName="Text" Expression="''Debitos''" />
          </ExpressionBindings>
        </Item5>
        <Item6 Ref="43" ControlType="XRLabel" TextAlignment="MiddleRight" SizeF="105,15" LocationFloat="665,16" Font="Arial, 8.5pt, style=Bold" Padding="0,6,0,0,100">
          <ExpressionBindings>
            <Item1 Ref="44" EventName="BeforePrint" PropertyName="Text" Expression="''Creditos''" />
          </ExpressionBindings>
        </Item6>
        <Item7 Ref="45" ControlType="XRLabel" Text="SALDO ACTUAL" TextAlignment="MiddleCenter" SizeF="210,15" LocationFloat="770,0" Font="Arial, 9pt, style=Bold" />
        <Item8 Ref="46" ControlType="XRLabel" TextAlignment="MiddleRight" SizeF="105,15" LocationFloat="770,16" Font="Arial, 8.5pt, style=Bold" Padding="0,6,0,0,100">
          <ExpressionBindings>
            <Item1 Ref="47" EventName="BeforePrint" PropertyName="Text" Expression="''Deudor''" />
          </ExpressionBindings>
        </Item8>
        <Item9 Ref="48" ControlType="XRLabel" TextAlignment="MiddleRight" SizeF="105,15" LocationFloat="875,16" Font="Arial, 8.5pt, style=Bold" Padding="0,6,0,0,100">
          <ExpressionBindings>
            <Item1 Ref="49" EventName="BeforePrint" PropertyName="Text" Expression="''Acreedor''" />
          </ExpressionBindings>
        </Item9>
      </Controls>
    </Item4>
    <Item5 Ref="50" ControlType="GroupHeaderBand" RepeatEveryPage="true" HeightF="22">
      <GroupFields>
        <Item1 Ref="51" FieldName="rubro_orden" />
        <Item2 Ref="52" FieldName="rubro_nombre" />
      </GroupFields>
      <Controls>
        <Item1 Ref="53" ControlType="XRLabel" TextAlignment="MiddleLeft" SizeF="980,15" LocationFloat="0,6" Font="Arial, 9.5pt, style=Bold">
          <ExpressionBindings>
            <Item1 Ref="54" EventName="BeforePrint" PropertyName="Text" Expression="[rubro_nombre]" />
          </ExpressionBindings>
        </Item1>
      </Controls>
    </Item5>
    <Item6 Ref="55" ControlType="DetailBand" HeightF="15">
      <Controls>
        <Item1 Ref="56" ControlType="XRLine" SizeF="630,2" LocationFloat="350,0" ForeColor="255,70,70,70">
          <ExpressionBindings>
            <Item1 Ref="57" EventName="BeforePrint" PropertyName="Visible" Expression="[tiene_hijos]" />
          </ExpressionBindings>
        </Item1>
        <Item2 Ref="58" ControlType="XRTable" SizeF="980,13" LocationFloat="0,2" Font="Arial, 8.5pt" Borders="None" BorderWidth="0">
          <Rows>
            <Item1 Ref="59" ControlType="XRTableRow" Weight="1.7692307692307692">
              <Cells>
                <Item1 Ref="60" ControlType="XRTableCell" Weight="110" TextAlignment="MiddleLeft" Padding="0,8,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="61" EventName="BeforePrint" PropertyName="Text" Expression="[cuenta_codigo]" />
                    <Item2 Ref="62" EventName="BeforePrint" PropertyName="Font.Bold" Expression="[tiene_hijos]" />
                  </ExpressionBindings>
                </Item1>
                <Item2 Ref="63" ControlType="XRTableCell" Weight="240" TextAlignment="MiddleLeft" Padding="0,8,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="64" EventName="BeforePrint" PropertyName="Text" Expression="[cuenta_nombre_mostrar]" />
                    <Item2 Ref="65" EventName="BeforePrint" PropertyName="Padding" Expression="Padding(8 + ([nivel] - 1) * 12, 6, 0, 0, 100)" />
                    <Item3 Ref="66" EventName="BeforePrint" PropertyName="Font.Bold" Expression="[tiene_hijos]" />
                  </ExpressionBindings>
                </Item2>
                <Item3 Ref="67" ControlType="XRTableCell" Weight="105" TextFormatString="{0:#,##0;(#,##0);-}" TextAlignment="MiddleRight" Padding="0,6,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="68" EventName="BeforePrint" PropertyName="Text" Expression="[saldo_anterior_deudor]" />
                    <Item2 Ref="69" EventName="BeforePrint" PropertyName="Font.Bold" Expression="[tiene_hijos]" />
                  </ExpressionBindings>
                </Item3>
                <Item4 Ref="70" ControlType="XRTableCell" Weight="105" TextFormatString="{0:#,##0;(#,##0);-}" TextAlignment="MiddleRight" Padding="0,6,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="71" EventName="BeforePrint" PropertyName="Text" Expression="[saldo_anterior_acreedor]" />
                    <Item2 Ref="72" EventName="BeforePrint" PropertyName="Font.Bold" Expression="[tiene_hijos]" />
                  </ExpressionBindings>
                </Item4>
                <Item5 Ref="73" ControlType="XRTableCell" Weight="105" TextFormatString="{0:#,##0;(#,##0);-}" TextAlignment="MiddleRight" Padding="0,6,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="74" EventName="BeforePrint" PropertyName="Text" Expression="[debitos_periodo]" />
                    <Item2 Ref="75" EventName="BeforePrint" PropertyName="Font.Bold" Expression="[tiene_hijos]" />
                  </ExpressionBindings>
                </Item5>
                <Item6 Ref="76" ControlType="XRTableCell" Weight="105" TextFormatString="{0:#,##0;(#,##0);-}" TextAlignment="MiddleRight" Padding="0,6,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="77" EventName="BeforePrint" PropertyName="Text" Expression="[creditos_periodo]" />
                    <Item2 Ref="78" EventName="BeforePrint" PropertyName="Font.Bold" Expression="[tiene_hijos]" />
                  </ExpressionBindings>
                </Item6>
                <Item7 Ref="79" ControlType="XRTableCell" Weight="105" TextFormatString="{0:#,##0;(#,##0);-}" TextAlignment="MiddleRight" Padding="0,6,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="80" EventName="BeforePrint" PropertyName="Text" Expression="[saldo_actual_deudor]" />
                    <Item2 Ref="81" EventName="BeforePrint" PropertyName="Font.Bold" Expression="[tiene_hijos]" />
                  </ExpressionBindings>
                </Item7>
                <Item8 Ref="82" ControlType="XRTableCell" Weight="105" TextFormatString="{0:#,##0;(#,##0);-}" TextAlignment="MiddleRight" Padding="0,6,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="83" EventName="BeforePrint" PropertyName="Text" Expression="[saldo_actual_acreedor]" />
                    <Item2 Ref="84" EventName="BeforePrint" PropertyName="Font.Bold" Expression="[tiene_hijos]" />
                  </ExpressionBindings>
                </Item8>
              </Cells>
            </Item1>
          </Rows>
        </Item2>
      </Controls>
    </Item6>
  </Bands>
  <ComponentStorage>
    <Item1 Ref="0" ObjectType="DevExpress.DataAccess.Sql.SqlDataSource,DevExpress.DataAccess.v25.2" Name="balance_comprobacionDataSource" Base64="PFNxbERhdGFTb3VyY2UgTmFtZT0iYmFsYW5jZV9jb21wcm9iYWNpb25EYXRhU291cmNlIj48Q29ubmVjdGlvbiBOYW1lPSJEZWZhdWx0Q29ubmVjdGlvbiIgRnJvbUFwcENvbmZpZz0idHJ1ZSIgLz48UXVlcnkgVHlwZT0iQ3VzdG9tU3FsUXVlcnkiIE5hbWU9ImJhbGFuY2VfY29tcHJvYmFjaW9uIj48UGFyYW1ldGVyIE5hbWU9InBfY29tcGFueV9pZCIgVHlwZT0iRGV2RXhwcmVzcy5EYXRhQWNjZXNzLkV4cHJlc3Npb24iPihTeXN0ZW0uSW50NjQpKD9Db21wYW55SWQpPC9QYXJhbWV0ZXI+PFBhcmFtZXRlciBOYW1lPSJwX2ZlY2hhX2Rlc2RlIiBUeXBlPSJEZXZFeHByZXNzLkRhdGFBY2Nlc3MuRXhwcmVzc2lvbiI+KFN5c3RlbS5EYXRlVGltZSkoP0ZlY2hhRGVzZGUpPC9QYXJhbWV0ZXI+PFBhcmFtZXRlciBOYW1lPSJwX2ZlY2hhX2hhc3RhIiBUeXBlPSJEZXZFeHByZXNzLkRhdGFBY2Nlc3MuRXhwcmVzc2lvbiI+KFN5c3RlbS5EYXRlVGltZSkoP0ZlY2hhSGFzdGEpPC9QYXJhbWV0ZXI+PFBhcmFtZXRlciBOYW1lPSJwX2luY2x1aXJfc2luX21vdmltaWVudG8iIFR5cGU9IkRldkV4cHJlc3MuRGF0YUFjY2Vzcy5FeHByZXNzaW9uIj4oU3lzdGVtLkJvb2xlYW4pKD9JbmNsdWlyU2luTW92aW1pZW50byk8L1BhcmFtZXRlcj48U3FsPlNFTEVDVCAqIEZST00gcHVibGljLnJlcF9iYWxhbmNlX2NvbXByb2JhY2lvbihDQVNUKEBwX2NvbXBhbnlfaWQgQVMgYmlnaW50KSwgQ0FTVChAcF9mZWNoYV9kZXNkZSBBUyBkYXRlKSwgQ0FTVChAcF9mZWNoYV9oYXN0YSBBUyBkYXRlKSwgQ0FTVChAcF9pbmNsdWlyX3Npbl9tb3ZpbWllbnRvIEFTIGJvb2xlYW4pKTwvU3FsPjwvUXVlcnk+PFJlc3VsdFNjaGVtYT48RGF0YVNldCBOYW1lPSJiYWxhbmNlX2NvbXByb2JhY2lvbkRhdGFTb3VyY2UiPjxWaWV3IE5hbWU9ImJhbGFuY2VfY29tcHJvYmFjaW9uIj48RmllbGQgTmFtZT0iZW1wcmVzYV9pZCIgVHlwZT0iSW50NjQiIC8+PEZpZWxkIE5hbWU9ImVtcHJlc2FfY29kaWdvIiBUeXBlPSJTdHJpbmciIC8+PEZpZWxkIE5hbWU9ImVtcHJlc2Ffbm9tYnJlIiBUeXBlPSJTdHJpbmciIC8+PEZpZWxkIE5hbWU9ImVtcHJlc2Ffbm9tYnJlX2xlZ2FsIiBUeXBlPSJTdHJpbmciIC8+PEZpZWxkIE5hbWU9ImVtcHJlc2FfcnRuIiBUeXBlPSJTdHJpbmciIC8+PEZpZWxkIE5hbWU9ImVtcHJlc2FfZW1haWwiIFR5cGU9IlN0cmluZyIgLz48RmllbGQgTmFtZT0iZW1wcmVzYV90ZWxlZm9ubyIgVHlwZT0iU3RyaW5nIiAvPjxGaWVsZCBOYW1lPSJlbXByZXNhX2RpcmVjY2lvbiIgVHlwZT0iU3RyaW5nIiAvPjxGaWVsZCBOYW1lPSJydWJyb19vcmRlbiIgVHlwZT0iSW50MzIiIC8+PEZpZWxkIE5hbWU9InJ1YnJvX25vbWJyZSIgVHlwZT0iU3RyaW5nIiAvPjxGaWVsZCBOYW1lPSJjdWVudGFfaWQiIFR5cGU9IkludDY0IiAvPjxGaWVsZCBOYW1lPSJjdWVudGFfcGFkcmVfaWQiIFR5cGU9IkludDY0IiAvPjxGaWVsZCBOYW1lPSJjdWVudGFfY29kaWdvIiBUeXBlPSJTdHJpbmciIC8+PEZpZWxkIE5hbWU9ImN1ZW50YV9ub21icmUiIFR5cGU9IlN0cmluZyIgLz48RmllbGQgTmFtZT0iY3VlbnRhX25vbWJyZV9tb3N0cmFyIiBUeXBlPSJTdHJpbmciIC8+PEZpZWxkIE5hbWU9InRpcG9fY3VlbnRhIiBUeXBlPSJTdHJpbmciIC8+PEZpZWxkIE5hbWU9ImNhdGVnb3JpYSIgVHlwZT0iU3RyaW5nIiAvPjxGaWVsZCBOYW1lPSJuaXZlbCIgVHlwZT0iSW50MTYiIC8+PEZpZWxkIE5hbWU9InBlcm1pdGVfbW92aW1pZW50byIgVHlwZT0iQm9vbGVhbiIgLz48RmllbGQgTmFtZT0idGllbmVfaGlqb3MiIFR5cGU9IkJvb2xlYW4iIC8+PEZpZWxkIE5hbWU9InNhbGRvX2FudGVyaW9yIiBUeXBlPSJEZWNpbWFsIiAvPjxGaWVsZCBOYW1lPSJzYWxkb19hbnRlcmlvcl9kZXVkb3IiIFR5cGU9IkRlY2ltYWwiIC8+PEZpZWxkIE5hbWU9InNhbGRvX2FudGVyaW9yX2FjcmVlZG9yIiBUeXBlPSJEZWNpbWFsIiAvPjxGaWVsZCBOYW1lPSJkZWJpdG9zX3BlcmlvZG8iIFR5cGU9IkRlY2ltYWwiIC8+PEZpZWxkIE5hbWU9ImNyZWRpdG9zX3BlcmlvZG8iIFR5cGU9IkRlY2ltYWwiIC8+PEZpZWxkIE5hbWU9InNhbGRvX2FjdHVhbCIgVHlwZT0iRGVjaW1hbCIgLz48RmllbGQgTmFtZT0ic2FsZG9fYWN0dWFsX2RldWRvciIgVHlwZT0iRGVjaW1hbCIgLz48RmllbGQgTmFtZT0ic2FsZG9fYWN0dWFsX2FjcmVlZG9yIiBUeXBlPSJEZWNpbWFsIiAvPjwvVmlldz48L0RhdGFTZXQ+PC9SZXN1bHRTY2hlbWE+PENvbm5lY3Rpb25PcHRpb25zIENsb3NlQ29ubmVjdGlvbj0idHJ1ZSIgLz48L1NxbERhdGFTb3VyY2U+" />
  </ComponentStorage>
  <ObjectStorage>
    <Item1 ObjectType="DevExpress.XtraReports.Serialization.ObjectStorageInfo, DevExpress.XtraReports.v25.2" Ref="6" Content="System.Int64" Type="System.Type" />
    <Item2 ObjectType="DevExpress.XtraReports.Serialization.ObjectStorageInfo, DevExpress.XtraReports.v25.2" Ref="8" Content="System.DateTime" Type="System.Type" />
    <Item3 ObjectType="DevExpress.XtraReports.Serialization.ObjectStorageInfo, DevExpress.XtraReports.v25.2" Ref="11" Content="System.Boolean" Type="System.Type" />
  </ObjectStorage>
</XtraReportsLayoutSerializer>',
       now(),
       'rediseno-estados-financieros',
       now(),
       'rediseno-estados-financieros'
FROM public.rep_catalogo_informe i
WHERE i.codigo = 'balance-comprobacion';

-- ---------------------------------------------------------------- Mayor analitico
UPDATE public.rep_reporte_layout l
   SET estado = 'ARCHIVED',
       updated_at = now(),
       updated_by = 'rediseno-estados-financieros'
  FROM public.rep_catalogo_informe i
 WHERE i.informe_id = l.informe_id
   AND i.codigo = 'mayor-analitico'
   AND l.estado = 'PUBLISHED';

INSERT INTO public.rep_reporte_layout
    (company_id, informe_id, version_num, estado, layout_xml,
     created_at, created_by, published_at, published_by)
SELECT i.company_id,
       i.informe_id,
       COALESCE((SELECT MAX(v.version_num) FROM public.rep_reporte_layout v
                  WHERE v.informe_id = i.informe_id), 0) + 1,
       'PUBLISHED',
       '﻿<?xml version="1.0" encoding="utf-8"?>
<XtraReportsLayoutSerializer SerializerVersion="25.2.4.0" Ref="1" ControlType="DevExpress.XtraReports.UI.XtraReport, DevExpress.XtraReports.v25.2, Version=25.2.4.0, Culture=neutral, PublicKeyToken=b88d1754d700e49a" Name="mayor-analitico" DisplayName="Mayor analitico" Landscape="true" Margins="40, 40, 78, 58" PageWidthF="1100" PageHeightF="850" Version="25.2" DataMember="mayor_analitico" DataSource="#Ref-0">
  <Parameters>
    <Item1 Ref="3" Visible="false" Description="Empresa del encabezado" ValueInfo="Empresa de Agua y Saneamiento S.A de C.V" AllowNull="true" Name="HeaderCompanyName" />
    <Item2 Ref="4" Visible="false" Description="Datos fiscales/contacto del encabezado" ValueInfo="RTN: R.T.N-05069999182490 | Tel: +504 26271450 / 26271451 | administracion@aguasdepuertocortes.com" AllowNull="true" Name="HeaderCompanyInfoLine" />
    <Item3 Ref="5" Visible="false" Description="Direccion del encabezado" ValueInfo="Bo. Copen 9 calle este, 5 y 6 ave Planta baja del estadio Excelsior" AllowNull="true" Name="HeaderCompanyAddress" />
    <Item4 Ref="7" Visible="false" Description="Empresa actual" ValueInfo="2" Name="CompanyId" Type="#Ref-6" />
    <Item5 Ref="9" Description="Fecha desde" ValueInfo="2026-01-01" Name="FechaDesde" Type="#Ref-8" />
    <Item6 Ref="10" Description="Fecha hasta" ValueInfo="2026-09-04" Name="FechaHasta" Type="#Ref-8" />
    <Item7 Ref="11" Description="Cuenta desde" AllowNull="true" Name="CuentaDesde" />
    <Item8 Ref="12" Description="Cuenta hasta" AllowNull="true" Name="CuentaHasta" />
    <Item9 Ref="14" Description="Incluir cuentas sin movimiento" ValueInfo="False" Name="IncluirSinMovimiento" Type="#Ref-13" />
    <Item10 Ref="15" Description="Incluir partidas de cierre" ValueInfo="True" Name="IncluirCierre" Type="#Ref-13" />
  </Parameters>
  <Bands>
    <Item1 Ref="16" ControlType="TopMarginBand" HeightF="78">
      <Controls>
        <Item1 Ref="17" ControlType="XRPictureBox" ImageSource="img,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" Sizing="ZoomImage" ImageAlignment="MiddleRight" SizeF="190,40" LocationFloat="560,2" />
        <Item2 Ref="18" ControlType="XRShape" LineWidth="0" Stretch="true" FillColor="255,140,198,63" SizeF="450.00003,12" LocationFloat="0,52" ForeColor="255,140,198,63">
          <Shape Ref="19" ShapeName="Rectangle" />
        </Item2>
        <Item3 Ref="20" ControlType="XRShape" LineWidth="0" Stretch="true" FillColor="255,43,163,199" SizeF="375,16" LocationFloat="375,48" ForeColor="255,43,163,199">
          <Shape Ref="21" ShapeName="Rectangle" />
        </Item3>
      </Controls>
    </Item1>
    <Item2 Ref="22" ControlType="BottomMarginBand" HeightF="58">
      <Controls>
        <Item1 Ref="23" ControlType="XRShape" LineWidth="0" Stretch="true" FillColor="255,43,163,199" SizeF="315,16" LocationFloat="0,26" ForeColor="255,43,163,199">
          <Shape Ref="24" ShapeName="Rectangle" />
        </Item1>
        <Item2 Ref="25" ControlType="XRShape" LineWidth="0" Stretch="true" FillColor="255,140,198,63" SizeF="525,14" LocationFloat="225.00002,34" ForeColor="255,140,198,63">
          <Shape Ref="26" ShapeName="Rectangle" />
        </Item2>
        <Item3 Ref="27" ControlType="XRPageInfo" PageInfo="Number" TextFormatString="{0}" TextAlignment="MiddleRight" SizeF="60,16" LocationFloat="690,4" Font="Arial, 9pt" ForeColor="255,60,60,60" />
      </Controls>
    </Item2>
    <Item3 Ref="28" ControlType="ReportHeaderBand" HeightF="104">
      <Controls>
        <Item1 Ref="29" ControlType="XRLabel" Text="Aguas de Puerto Cortes" TextAlignment="MiddleCenter" SizeF="750,15" LocationFloat="0,0" Font="Arial, 11pt, style=Bold" />
        <Item2 Ref="30" ControlType="XRLabel" Text="Empresa de Agua y Saneamiento S.A de C.V" TextAlignment="MiddleCenter" SizeF="750,15" LocationFloat="0,15" Font="Arial, 11pt, style=Bold" />
        <Item3 Ref="31" ControlType="XRLabel" Text="Bo. Copen 9 calle este, 5 y 6 ave Planta baja del estadio Excelsior" TextAlignment="MiddleCenter" SizeF="750,15" LocationFloat="0,30" Font="Arial, 11pt, style=Bold" />
        <Item4 Ref="32" ControlType="XRLabel" Text="MAYOR ANALITICO" TextAlignment="MiddleCenter" SizeF="750,15" LocationFloat="0,45" Font="Arial, 11pt, style=Bold" />
        <Item5 Ref="33" ControlType="XRLabel" Text="(Expresado en lempiras)" TextAlignment="MiddleCenter" SizeF="750,15" LocationFloat="0,60" Font="Arial, 9pt, style=Bold" />
      </Controls>
    </Item3>
    <Item4 Ref="34" ControlType="PageHeaderBand" HeightF="20">
      <Controls>
        <Item1 Ref="35" ControlType="XRLabel" Text="Fecha" TextAlignment="MiddleLeft" SizeF="70,15" LocationFloat="0,2" Font="Arial, 8.5pt, style=Bold, Underline" Padding="4,0,0,0,100" />
        <Item2 Ref="36" ControlType="XRLabel" Text="Poliza" TextAlignment="MiddleLeft" SizeF="80,15" LocationFloat="70,2" Font="Arial, 8.5pt, style=Bold, Underline" Padding="4,0,0,0,100" />
        <Item3 Ref="37" ControlType="XRLabel" Text="Documento" TextAlignment="MiddleLeft" SizeF="95,15" LocationFloat="150,2" Font="Arial, 8.5pt, style=Bold, Underline" Padding="4,0,0,0,100" />
        <Item4 Ref="38" ControlType="XRLabel" Text="Tipo" TextAlignment="MiddleLeft" SizeF="100,15" LocationFloat="245,2" Font="Arial, 8.5pt, style=Bold, Underline" Padding="4,0,0,0,100" />
        <Item5 Ref="39" ControlType="XRLabel" Text="Descripcion" TextAlignment="MiddleLeft" SizeF="320,15" LocationFloat="345,2" Font="Arial, 8.5pt, style=Bold, Underline" Padding="4,0,0,0,100" />
        <Item6 Ref="40" ControlType="XRLabel" Text="Debe" TextAlignment="MiddleRight" SizeF="105,15" LocationFloat="665,2" Font="Arial, 8.5pt, style=Bold, Underline" Padding="0,6,0,0,100" />
        <Item7 Ref="41" ControlType="XRLabel" Text="Haber" TextAlignment="MiddleRight" SizeF="105,15" LocationFloat="770,2" Font="Arial, 8.5pt, style=Bold, Underline" Padding="0,6,0,0,100" />
        <Item8 Ref="42" ControlType="XRLabel" Text="Saldo" TextAlignment="MiddleRight" SizeF="105,15" LocationFloat="875,2" Font="Arial, 8.5pt, style=Bold, Underline" Padding="0,6,0,0,100" />
      </Controls>
    </Item4>
    <Item5 Ref="43" ControlType="GroupHeaderBand" RepeatEveryPage="true" HeightF="24">
      <GroupFields>
        <Item1 Ref="44" FieldName="cuenta_codigo" />
      </GroupFields>
      <Controls>
        <Item1 Ref="45" ControlType="XRLabel" TextAlignment="MiddleLeft" SizeF="770,15" LocationFloat="0,6" Font="Arial, 9.5pt, style=Bold">
          <ExpressionBindings>
            <Item1 Ref="46" EventName="BeforePrint" PropertyName="Text" Expression="[cuenta_codigo] + ''  '' + [cuenta_nombre]" />
          </ExpressionBindings>
        </Item1>
        <Item2 Ref="47" ControlType="XRLabel" Text="Saldo anterior" TextAlignment="MiddleRight" SizeF="157.5,15" LocationFloat="717.5,6" Font="Arial, 8.5pt" />
        <Item3 Ref="48" ControlType="XRLabel" TextFormatString="{0:#,##0;(#,##0);-}" TextAlignment="MiddleRight" SizeF="105,15" LocationFloat="875,6" Font="Arial, 9pt, style=Bold" Padding="0,6,0,0,100">
          <ExpressionBindings>
            <Item1 Ref="49" EventName="BeforePrint" PropertyName="Text" Expression="[saldo_anterior]" />
          </ExpressionBindings>
        </Item3>
      </Controls>
    </Item5>
    <Item6 Ref="50" ControlType="DetailBand" HeightF="15">
      <Controls>
        <Item1 Ref="51" ControlType="XRTable" SizeF="980,13" LocationFloat="0,2" Font="Arial, 8.5pt" Borders="None" BorderWidth="0">
          <Rows>
            <Item1 Ref="52" ControlType="XRTableRow" Weight="1.7692307692307692">
              <Cells>
                <Item1 Ref="53" ControlType="XRTableCell" Weight="70" TextFormatString="{0:dd/MM/yyyy}" TextAlignment="MiddleLeft" Padding="0,8,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="54" EventName="BeforePrint" PropertyName="Text" Expression="[fecha]" />
                  </ExpressionBindings>
                </Item1>
                <Item2 Ref="55" ControlType="XRTableCell" Weight="80" TextAlignment="MiddleLeft" Padding="0,8,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="56" EventName="BeforePrint" PropertyName="Text" Expression="[poliza_number]" />
                  </ExpressionBindings>
                </Item2>
                <Item3 Ref="57" ControlType="XRTableCell" Weight="95" TextAlignment="MiddleLeft" Padding="0,8,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="58" EventName="BeforePrint" PropertyName="Text" Expression="[documento]" />
                  </ExpressionBindings>
                </Item3>
                <Item4 Ref="59" ControlType="XRTableCell" Weight="100" TextAlignment="MiddleLeft" Padding="0,8,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="60" EventName="BeforePrint" PropertyName="Text" Expression="[tipo_transaccion]" />
                  </ExpressionBindings>
                </Item4>
                <Item5 Ref="61" ControlType="XRTableCell" Weight="320" TextAlignment="MiddleLeft" Padding="0,8,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="62" EventName="BeforePrint" PropertyName="Text" Expression="[descripcion]" />
                  </ExpressionBindings>
                </Item5>
                <Item6 Ref="63" ControlType="XRTableCell" Weight="105" TextFormatString="{0:#,##0;(#,##0);-}" TextAlignment="MiddleRight" Padding="0,6,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="64" EventName="BeforePrint" PropertyName="Text" Expression="[debe]" />
                  </ExpressionBindings>
                </Item6>
                <Item7 Ref="65" ControlType="XRTableCell" Weight="105" TextFormatString="{0:#,##0;(#,##0);-}" TextAlignment="MiddleRight" Padding="0,6,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="66" EventName="BeforePrint" PropertyName="Text" Expression="[haber]" />
                  </ExpressionBindings>
                </Item7>
                <Item8 Ref="67" ControlType="XRTableCell" Weight="105" TextFormatString="{0:#,##0;(#,##0);-}" TextAlignment="MiddleRight" Padding="0,6,0,0,96" Borders="None">
                  <ExpressionBindings>
                    <Item1 Ref="68" EventName="BeforePrint" PropertyName="Text" Expression="[saldo_corriente]" />
                  </ExpressionBindings>
                </Item8>
              </Cells>
            </Item1>
          </Rows>
        </Item1>
      </Controls>
    </Item6>
    <Item7 Ref="69" ControlType="GroupFooterBand" HeightF="20">
      <Controls>
        <Item1 Ref="70" ControlType="XRLine" SizeF="210,2" LocationFloat="665,0" ForeColor="255,70,70,70">
          <ExpressionBindings>
            <Item1 Ref="71" EventName="BeforePrint" PropertyName="Visible" Expression="true" />
          </ExpressionBindings>
        </Item1>
        <Item2 Ref="72" ControlType="XRLabel" TextAlignment="MiddleLeft" SizeF="665,15" LocationFloat="0,3" Font="Arial, 9pt, style=Bold" Padding="8,0,0,0,100">
          <ExpressionBindings>
            <Item1 Ref="73" EventName="BeforePrint" PropertyName="Text" Expression="''Suman los movimientos de '' + [cuenta_codigo]" />
          </ExpressionBindings>
        </Item2>
        <Item3 Ref="74" ControlType="XRLabel" TextFormatString="{0:#,##0;(#,##0);-}" TextAlignment="MiddleRight" SizeF="105,15" LocationFloat="665,3" Font="Arial, 9pt, style=Bold" Padding="0,6,0,0,100">
          <Summary Ref="75" Running="Group" IgnoreNullValues="true" />
          <ExpressionBindings>
            <Item1 Ref="76" EventName="BeforePrint" PropertyName="Text" Expression="sumSum([debe])" />
          </ExpressionBindings>
        </Item3>
        <Item4 Ref="77" ControlType="XRLabel" TextFormatString="{0:#,##0;(#,##0);-}" TextAlignment="MiddleRight" SizeF="105,15" LocationFloat="770,3" Font="Arial, 9pt, style=Bold" Padding="0,6,0,0,100">
          <Summary Ref="78" Running="Group" IgnoreNullValues="true" />
          <ExpressionBindings>
            <Item1 Ref="79" EventName="BeforePrint" PropertyName="Text" Expression="sumSum([haber])" />
          </ExpressionBindings>
        </Item4>
      </Controls>
    </Item7>
  </Bands>
  <ComponentStorage>
    <Item1 Ref="0" ObjectType="DevExpress.DataAccess.Sql.SqlDataSource,DevExpress.DataAccess.v25.2" Name="mayor_analiticoDataSource" Base64="PFNxbERhdGFTb3VyY2UgTmFtZT0ibWF5b3JfYW5hbGl0aWNvRGF0YVNvdXJjZSI+PENvbm5lY3Rpb24gTmFtZT0iRGVmYXVsdENvbm5lY3Rpb24iIEZyb21BcHBDb25maWc9InRydWUiIC8+PFF1ZXJ5IFR5cGU9IkN1c3RvbVNxbFF1ZXJ5IiBOYW1lPSJtYXlvcl9hbmFsaXRpY28iPjxQYXJhbWV0ZXIgTmFtZT0icF9jb21wYW55X2lkIiBUeXBlPSJEZXZFeHByZXNzLkRhdGFBY2Nlc3MuRXhwcmVzc2lvbiI+KFN5c3RlbS5JbnQ2NCkoP0NvbXBhbnlJZCk8L1BhcmFtZXRlcj48UGFyYW1ldGVyIE5hbWU9InBfZmVjaGFfZGVzZGUiIFR5cGU9IkRldkV4cHJlc3MuRGF0YUFjY2Vzcy5FeHByZXNzaW9uIj4oU3lzdGVtLkRhdGVUaW1lKSg/RmVjaGFEZXNkZSk8L1BhcmFtZXRlcj48UGFyYW1ldGVyIE5hbWU9InBfZmVjaGFfaGFzdGEiIFR5cGU9IkRldkV4cHJlc3MuRGF0YUFjY2Vzcy5FeHByZXNzaW9uIj4oU3lzdGVtLkRhdGVUaW1lKSg/RmVjaGFIYXN0YSk8L1BhcmFtZXRlcj48UGFyYW1ldGVyIE5hbWU9InBfY3VlbnRhX2Rlc2RlIiBUeXBlPSJEZXZFeHByZXNzLkRhdGFBY2Nlc3MuRXhwcmVzc2lvbiI+KFN5c3RlbS5TdHJpbmcpKD9DdWVudGFEZXNkZSk8L1BhcmFtZXRlcj48UGFyYW1ldGVyIE5hbWU9InBfY3VlbnRhX2hhc3RhIiBUeXBlPSJEZXZFeHByZXNzLkRhdGFBY2Nlc3MuRXhwcmVzc2lvbiI+KFN5c3RlbS5TdHJpbmcpKD9DdWVudGFIYXN0YSk8L1BhcmFtZXRlcj48UGFyYW1ldGVyIE5hbWU9InBfaW5jbHVpcl9zaW5fbW92aW1pZW50byIgVHlwZT0iRGV2RXhwcmVzcy5EYXRhQWNjZXNzLkV4cHJlc3Npb24iPihTeXN0ZW0uQm9vbGVhbikoP0luY2x1aXJTaW5Nb3ZpbWllbnRvKTwvUGFyYW1ldGVyPjxQYXJhbWV0ZXIgTmFtZT0icF9pbmNsdWlyX2NpZXJyZSIgVHlwZT0iRGV2RXhwcmVzcy5EYXRhQWNjZXNzLkV4cHJlc3Npb24iPihTeXN0ZW0uQm9vbGVhbikoP0luY2x1aXJDaWVycmUpPC9QYXJhbWV0ZXI+PFNxbD5TRUxFQ1QgKiBGUk9NIHB1YmxpYy5yZXBfbWF5b3JfYW5hbGl0aWNvKENBU1QoQHBfY29tcGFueV9pZCBBUyBiaWdpbnQpLCBDQVNUKEBwX2ZlY2hhX2Rlc2RlIEFTIGRhdGUpLCBDQVNUKEBwX2ZlY2hhX2hhc3RhIEFTIGRhdGUpLCBDQVNUKEBwX2N1ZW50YV9kZXNkZSBBUyB0ZXh0KSwgQ0FTVChAcF9jdWVudGFfaGFzdGEgQVMgdGV4dCksIENBU1QoQHBfaW5jbHVpcl9zaW5fbW92aW1pZW50byBBUyBib29sZWFuKSwgQ0FTVChAcF9pbmNsdWlyX2NpZXJyZSBBUyBib29sZWFuKSk8L1NxbD48L1F1ZXJ5PjxSZXN1bHRTY2hlbWE+PERhdGFTZXQgTmFtZT0ibWF5b3JfYW5hbGl0aWNvRGF0YVNvdXJjZSI+PFZpZXcgTmFtZT0ibWF5b3JfYW5hbGl0aWNvIj48RmllbGQgTmFtZT0iY3VlbnRhX2lkIiBUeXBlPSJJbnQ2NCIgLz48RmllbGQgTmFtZT0iY3VlbnRhX2NvZGlnbyIgVHlwZT0iU3RyaW5nIiAvPjxGaWVsZCBOYW1lPSJjdWVudGFfbm9tYnJlIiBUeXBlPSJTdHJpbmciIC8+PEZpZWxkIE5hbWU9InRpcG9fY3VlbnRhIiBUeXBlPSJTdHJpbmciIC8+PEZpZWxkIE5hbWU9InNhbGRvX2FudGVyaW9yIiBUeXBlPSJEZWNpbWFsIiAvPjxGaWVsZCBOYW1lPSJwb2xpemFfaWQiIFR5cGU9IkludDY0IiAvPjxGaWVsZCBOYW1lPSJmZWNoYSIgVHlwZT0iRGF0ZVRpbWUiIC8+PEZpZWxkIE5hbWU9InBvbGl6YV9udW1iZXIiIFR5cGU9IlN0cmluZyIgLz48RmllbGQgTmFtZT0iZG9jdW1lbnRvIiBUeXBlPSJTdHJpbmciIC8+PEZpZWxkIE5hbWU9InRpcG9fdHJhbnNhY2Npb24iIFR5cGU9IlN0cmluZyIgLz48RmllbGQgTmFtZT0iZGVzY3JpcGNpb24iIFR5cGU9IlN0cmluZyIgLz48RmllbGQgTmFtZT0iZGViZSIgVHlwZT0iRGVjaW1hbCIgLz48RmllbGQgTmFtZT0iaGFiZXIiIFR5cGU9IkRlY2ltYWwiIC8+PEZpZWxkIE5hbWU9InNhbGRvX2NvcnJpZW50ZSIgVHlwZT0iRGVjaW1hbCIgLz48RmllbGQgTmFtZT0iZXNfbW92aW1pZW50byIgVHlwZT0iQm9vbGVhbiIgLz48L1ZpZXc+PC9EYXRhU2V0PjwvUmVzdWx0U2NoZW1hPjxDb25uZWN0aW9uT3B0aW9ucyBDbG9zZUNvbm5lY3Rpb249InRydWUiIC8+PC9TcWxEYXRhU291cmNlPg==" />
  </ComponentStorage>
  <ObjectStorage>
    <Item1 ObjectType="DevExpress.XtraReports.Serialization.ObjectStorageInfo, DevExpress.XtraReports.v25.2" Ref="6" Content="System.Int64" Type="System.Type" />
    <Item2 ObjectType="DevExpress.XtraReports.Serialization.ObjectStorageInfo, DevExpress.XtraReports.v25.2" Ref="8" Content="System.DateTime" Type="System.Type" />
    <Item3 ObjectType="DevExpress.XtraReports.Serialization.ObjectStorageInfo, DevExpress.XtraReports.v25.2" Ref="13" Content="System.Boolean" Type="System.Type" />
  </ObjectStorage>
</XtraReportsLayoutSerializer>',
       now(),
       'rediseno-estados-financieros',
       now(),
       'rediseno-estados-financieros'
FROM public.rep_catalogo_informe i
WHERE i.codigo = 'mayor-analitico';

COMMIT;

-- Verificacion:
--   SELECT i.codigo, l.estado, l.version_num, length(l.layout_xml)
--   FROM public.rep_reporte_layout l
--   JOIN public.rep_catalogo_informe i ON i.informe_id = l.informe_id
--   WHERE i.codigo IN ('balance-comprobacion', 'mayor-analitico')
--     AND l.estado = 'PUBLISHED'
--   ORDER BY 1;
--
-- Cada uno debe tener UNA version publicada, la nueva, y bastante mas grande que la anterior.
