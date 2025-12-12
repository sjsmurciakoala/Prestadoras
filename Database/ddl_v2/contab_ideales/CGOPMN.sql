USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[CGOPMN]    Script Date: 10/12/2025 13:53:07 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[CGOPMN](
	[CodCnx] [varchar](15) NOT NULL,
	[CodCmpy] [varchar](15) NOT NULL,
	[CodMenu] [varchar](15) NOT NULL,
	[CodOpMn] [varchar](13) NOT NULL,
	[Nombre] [varchar](55) NOT NULL,
	[Accion] [int] NULL,
	[OpActiva] [int] NULL,
	[UPrinter] [int] NULL,
	[SSFld] [varchar](35) NULL,
	[formatName] [varchar](80) NULL,
	[formatType] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[CodCnx] ASC,
	[CodCmpy] ASC,
	[CodMenu] ASC,
	[CodOpMn] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[CGOPMN] ADD  DEFAULT ((0)) FOR [Accion]
GO

ALTER TABLE [dbo].[CGOPMN] ADD  DEFAULT ((0)) FOR [OpActiva]
GO

ALTER TABLE [dbo].[CGOPMN] ADD  DEFAULT ((0)) FOR [UPrinter]
GO

ALTER TABLE [dbo].[CGOPMN] ADD  DEFAULT ((0)) FOR [formatType]
GO


