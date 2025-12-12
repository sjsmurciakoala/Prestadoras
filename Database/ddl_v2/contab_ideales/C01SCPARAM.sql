USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01SCPARAM]    Script Date: 10/12/2025 13:45:06 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01SCPARAM](
	[DB] [varchar](255) NULL,
	[TipoCon] [int] NULL,
	[PrxComprobante] [int] NULL,
	[TExentoV] [varchar](24) NULL,
	[TGravableV] [varchar](24) NULL,
	[AlicuotaV] [varchar](24) NULL,
	[MtoTaxV] [varchar](24) NULL,
	[MtoTotalV] [varchar](24) NULL,
	[RetenIVAV] [varchar](24) NULL,
	[RetenISLRV] [varchar](24) NULL,
	[CtaCxC] [varchar](24) NULL,
	[ClaseV] [varchar](5) NULL,
	[TExentoC] [varchar](24) NULL,
	[TGravableC] [varchar](24) NULL,
	[AlicuotaC] [varchar](24) NULL,
	[MtoTaxC] [varchar](24) NULL,
	[MtoTotalC] [varchar](24) NULL,
	[RetenIVAC] [varchar](24) NULL,
	[RetenISLRC] [varchar](24) NULL,
	[CtaCxP] [varchar](24) NULL,
	[Consolidar] [varchar](24) NULL,
	[CodOperC] [varchar](24) NULL,
	[ClaseC] [varchar](5) NULL
) ON [PRIMARY]
GO


