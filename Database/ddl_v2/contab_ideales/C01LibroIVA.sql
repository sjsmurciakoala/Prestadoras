USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01LibroIVA]    Script Date: 10/12/2025 13:40:17 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01LibroIVA](
	[NroUnico] [int] IDENTITY(1,1) NOT NULL,
	[IdTercero] [varchar](25) NOT NULL,
	[Concepto] [varchar](20) NOT NULL,
	[Detalle] [varchar](40) NOT NULL,
	[FechaE] [datetime] NULL,
	[FechaT] [datetime] NULL,
	[TipoDoc] [varchar](5) NULL,
	[NumeroDoc] [varchar](25) NULL,
	[NroCtrol] [varchar](20) NULL,
	[FactAfectada] [varchar](25) NULL,
	[NroImprFiscal] [varchar](20) NULL,
	[TExento] [decimal](28, 2) NOT NULL,
	[TGravable] [decimal](28, 2) NOT NULL,
	[Alicuota] [decimal](28, 2) NOT NULL,
	[MtoTax] [decimal](28, 2) NOT NULL,
	[MtoTotal] [decimal](28, 2) NOT NULL,
	[RetenIVA] [decimal](28, 2) NOT NULL,
	[PorctReten] [decimal](28, 2) NOT NULL,
	[NroRetencion] [varchar](25) NULL,
	[FechaRet] [datetime] NULL,
	[CodOper] [varchar](15) NULL,
 CONSTRAINT [PK_C01LibroIVA] PRIMARY KEY CLUSTERED 
(
	[NroUnico] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01LibroIVA] ADD  DEFAULT (getdate()) FOR [FechaT]
GO

ALTER TABLE [dbo].[C01LibroIVA] ADD  CONSTRAINT [DF__C01LibroI__TipoD__5412D5B8]  DEFAULT ('FAC') FOR [TipoDoc]
GO

ALTER TABLE [dbo].[C01LibroIVA] ADD  DEFAULT ((0)) FOR [TExento]
GO

ALTER TABLE [dbo].[C01LibroIVA] ADD  DEFAULT ((0)) FOR [TGravable]
GO

ALTER TABLE [dbo].[C01LibroIVA] ADD  CONSTRAINT [DF_C01LibroIVA_Alicuota]  DEFAULT ((0)) FOR [Alicuota]
GO

ALTER TABLE [dbo].[C01LibroIVA] ADD  CONSTRAINT [DF_C01LibroIVA_MtoTax]  DEFAULT ((0)) FOR [MtoTax]
GO

ALTER TABLE [dbo].[C01LibroIVA] ADD  DEFAULT ((0)) FOR [MtoTotal]
GO

ALTER TABLE [dbo].[C01LibroIVA] ADD  DEFAULT ((0)) FOR [RetenIVA]
GO

ALTER TABLE [dbo].[C01LibroIVA] ADD  DEFAULT ((100)) FOR [PorctReten]
GO


