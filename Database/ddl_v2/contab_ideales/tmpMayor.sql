USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[tmpMayor]    Script Date: 10/12/2025 14:09:55 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[tmpMayor](
	[ID_NETWORK] [varchar](100) NULL,
	[ID] [numeric](18, 0) IDENTITY(1,1) NOT NULL,
	[ID_Account] [varchar](100) NULL,
	[Descrip] [varchar](100) NULL,
	[siLevel] [int] NULL,
	[boMovement] [int] NULL,
	[Tipo] [int] NULL,
	[SaldoAnterior] [varchar](25) NULL,
	[Debitos] [varchar](25) NULL,
	[Creditos] [varchar](25) NULL,
	[SaldoActual] [varchar](25) NULL,
	[SaldoMes] [varchar](25) NULL
) ON [PRIMARY]
GO


