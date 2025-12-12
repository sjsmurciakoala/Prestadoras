USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[TMP_ProfLoss]    Script Date: 10/12/2025 14:08:53 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[TMP_ProfLoss](
	[ID_NETWORK] [varchar](100) NULL,
	[ID] [numeric](18, 0) IDENTITY(1,1) NOT NULL,
	[ID_Account] [varchar](100) NULL,
	[Descrip] [varchar](100) NULL,
	[siLevel] [int] NULL,
	[boMovement] [int] NULL,
	[Tipo] [int] NULL,
	[Saldo1] [varchar](30) NULL,
	[Saldo2] [varchar](30) NULL,
	[Saldo3] [varchar](30) NULL,
	[Saldo4] [varchar](30) NULL,
	[Saldo5] [varchar](30) NULL
) ON [PRIMARY]
GO


