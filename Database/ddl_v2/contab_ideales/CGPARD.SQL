USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[CGPARD]    Script Date: 10/12/2025 13:54:13 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[CGPARD](
	[CodCnx] [varchar](10) NOT NULL,
	[CodCmpy] [varchar](10) NOT NULL,
	[CodParm] [varchar](15) NOT NULL,
	[Modulo] [int] NOT NULL,
	[Parametro] [int] NOT NULL,
	[NoData] [int] NOT NULL,
	[TipoData] [int] NOT NULL,
	[IDataF] [int] NULL,
	[SDataF] [varchar](50) NULL,
	[FDataF] [decimal](28, 2) NULL,
	[SSFld] [varchar](35) NULL,
PRIMARY KEY CLUSTERED 
(
	[CodCnx] ASC,
	[CodCmpy] ASC,
	[CodParm] ASC,
	[Modulo] ASC,
	[Parametro] ASC,
	[NoData] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[CGPARD] ADD  DEFAULT ((0)) FOR [Modulo]
GO

ALTER TABLE [dbo].[CGPARD] ADD  DEFAULT ((0)) FOR [Parametro]
GO

ALTER TABLE [dbo].[CGPARD] ADD  DEFAULT ((0)) FOR [NoData]
GO

ALTER TABLE [dbo].[CGPARD] ADD  DEFAULT ((0)) FOR [TipoData]
GO

ALTER TABLE [dbo].[CGPARD] ADD  DEFAULT ((0)) FOR [IDataF]
GO

ALTER TABLE [dbo].[CGPARD] ADD  DEFAULT ((0)) FOR [FDataF]
GO


