USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[CGPARM]    Script Date: 10/12/2025 13:54:23 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[CGPARM](
	[CodCnx] [varchar](10) NOT NULL,
	[CodCmpy] [varchar](10) NOT NULL,
	[CodParm] [varchar](15) NOT NULL,
	[Modulo] [int] NOT NULL,
	[Parametro] [int] NOT NULL,
	[Activo] [int] NOT NULL,
	[Clave] [int] NOT NULL,
	[Habilitado] [int] NOT NULL,
	[SSFld] [varchar](35) NULL,
PRIMARY KEY CLUSTERED 
(
	[CodCnx] ASC,
	[CodCmpy] ASC,
	[CodParm] ASC,
	[Modulo] ASC,
	[Parametro] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[CGPARM] ADD  DEFAULT ((0)) FOR [Modulo]
GO

ALTER TABLE [dbo].[CGPARM] ADD  DEFAULT ((0)) FOR [Parametro]
GO

ALTER TABLE [dbo].[CGPARM] ADD  DEFAULT ((0)) FOR [Activo]
GO

ALTER TABLE [dbo].[CGPARM] ADD  DEFAULT ((0)) FOR [Clave]
GO

ALTER TABLE [dbo].[CGPARM] ADD  DEFAULT ((0)) FOR [Habilitado]
GO


