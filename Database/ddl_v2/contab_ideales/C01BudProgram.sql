USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01BudProgram]    Script Date: 10/12/2025 13:27:52 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01BudProgram](
	[ID_Program] [varchar](10) NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
	[Note] [text] NULL,
	[BudDebit] [decimal](28, 2) NOT NULL,
	[BudCredit] [decimal](28, 2) NOT NULL,
	[Additions] [decimal](28, 2) NOT NULL,
	[Reserved] [decimal](28, 2) NOT NULL,
	[Debits] [decimal](28, 2) NOT NULL,
	[Credits] [decimal](28, 2) NOT NULL,
	[Balance]  AS ((([BudDebit]+[Additions])-[Reserved])-[Debits]),
	[boMovement] [bit] NOT NULL,
	[boStatus] [bit] NOT NULL,
	[siPeriod] [smallint] NOT NULL,
 CONSTRAINT [C01BudProgram0] PRIMARY KEY CLUSTERED 
(
	[ID_Program] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01BudProgram] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[C01BudProgram] ADD  DEFAULT ((0)) FOR [BudDebit]
GO

ALTER TABLE [dbo].[C01BudProgram] ADD  DEFAULT ((0)) FOR [BudCredit]
GO

ALTER TABLE [dbo].[C01BudProgram] ADD  DEFAULT ((0)) FOR [Additions]
GO

ALTER TABLE [dbo].[C01BudProgram] ADD  DEFAULT ((0)) FOR [Reserved]
GO

ALTER TABLE [dbo].[C01BudProgram] ADD  DEFAULT ((0)) FOR [Debits]
GO

ALTER TABLE [dbo].[C01BudProgram] ADD  DEFAULT ((0)) FOR [Credits]
GO

ALTER TABLE [dbo].[C01BudProgram] ADD  DEFAULT ((0)) FOR [boMovement]
GO

ALTER TABLE [dbo].[C01BudProgram] ADD  DEFAULT ((1)) FOR [boStatus]
GO

ALTER TABLE [dbo].[C01BudProgram] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01BudProgram]  WITH CHECK ADD CHECK  (([Additions]>=(0)))
GO

ALTER TABLE [dbo].[C01BudProgram]  WITH CHECK ADD CHECK  (([boMovement]=(1) OR [boMovement]=(0)))
GO

ALTER TABLE [dbo].[C01BudProgram]  WITH CHECK ADD CHECK  (([BudCredit]>=(0)))
GO

ALTER TABLE [dbo].[C01BudProgram]  WITH CHECK ADD CHECK  (([BudDebit]>=(0)))
GO

ALTER TABLE [dbo].[C01BudProgram]  WITH CHECK ADD CHECK  (([Credits]>=(0)))
GO

ALTER TABLE [dbo].[C01BudProgram]  WITH CHECK ADD CHECK  (([Debits]>=(0)))
GO

ALTER TABLE [dbo].[C01BudProgram]  WITH CHECK ADD CHECK  (([Reserved]>=(0)))
GO


