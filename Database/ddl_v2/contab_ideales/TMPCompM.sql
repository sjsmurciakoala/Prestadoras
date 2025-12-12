USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[TMPCompM]    Script Date: 10/12/2025 14:09:14 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[TMPCompM](
	[ID_NETWORK] [varchar](120) NULL,
	[dtDate] [datetime] NULL,
	[ID_Entry] [varchar](10) NULL,
	[Concept] [varchar](60) NULL,
	[ID_Account] [varchar](120) NULL,
	[Descrip_Account] [varchar](60) NULL,
	[Amount] [decimal](28, 2) NULL,
	[Cr] [decimal](2, 0) NULL,
	[Debits] [decimal](28, 2) NULL,
	[Credits] [decimal](28, 2) NULL
) ON [PRIMARY]
GO


