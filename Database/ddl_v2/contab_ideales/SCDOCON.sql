USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[SCDOCON]    Script Date: 10/12/2025 14:06:17 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[SCDOCON](
	[DB] [varchar](255) NULL,
	[DataContab] [varchar](255) NULL,
	[FechaI] [datetime] NULL,
	[FechaF] [datetime] NULL,
	[TipoOper] [varchar](4) NULL,
	[User] [varchar](255) NULL
) ON [PRIMARY]
GO


