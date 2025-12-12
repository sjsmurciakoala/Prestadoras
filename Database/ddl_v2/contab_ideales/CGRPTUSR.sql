USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[CGRPTUSR]    Script Date: 10/12/2025 13:54:46 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[CGRPTUSR](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[Title] [varchar](80) NOT NULL,
	[formatName] [varchar](80) NOT NULL,
	[formatType] [int] NULL,
	[CodOpMn] [varchar](13) NOT NULL
) ON [PRIMARY]
GO


