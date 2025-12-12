USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[CGAUTR]    Script Date: 10/12/2025 13:52:10 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[CGAUTR](
	[CodCnx] [varchar](15) NOT NULL,
	[CodCmpy] [varchar](15) NOT NULL,
	[CodAutr] [varchar](15) NOT NULL,
	[Modulo] [int] NOT NULL,
	[Parametro] [int] NOT NULL,
	[Autoriza] [varchar](15) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[CodCnx] ASC,
	[CodCmpy] ASC,
	[CodAutr] ASC,
	[Modulo] ASC,
	[Parametro] ASC,
	[Autoriza] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[CGAUTR] ADD  DEFAULT ((0)) FOR [Modulo]
GO

ALTER TABLE [dbo].[CGAUTR] ADD  DEFAULT ((0)) FOR [Parametro]
GO


