USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01ConsolEntry]    Script Date: 10/12/2025 13:30:59 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01ConsolEntry](
	[ID_Origin] [varchar](5) NOT NULL,
	[siPeriod] [smallint] NOT NULL,
	[TypeTrans] [tinyint] NOT NULL,
	[ID_Entry] [varchar](10) NOT NULL,
	[siType] [tinyint] NOT NULL,
	[Concept] [varchar](50) NOT NULL,
	[ID_Class] [varchar](5) NULL,
	[ID_Budget] [varchar](10) NULL,
	[dtDate] [datetime] NOT NULL,
	[ID_Cost] [varchar](8) NULL,
	[Debits] [decimal](28, 2) NULL,
	[Credits] [decimal](28, 2) NULL,
	[ID_Currency] [varchar](3) NULL,
	[AmountFOB] [decimal](28, 2) NULL,
	[CurrencyRate] [decimal](28, 9) NULL,
	[boCashFlow] [bit] NULL,
	[Status] [smallint] NOT NULL,
	[StatusConsol] [smallint] NULL,
 CONSTRAINT [C01ConsolEntry0] PRIMARY KEY CLUSTERED 
(
	[ID_Origin] ASC,
	[siPeriod] ASC,
	[TypeTrans] ASC,
	[ID_Entry] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01ConsolEntry] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01ConsolEntry] ADD  DEFAULT ((0)) FOR [TypeTrans]
GO

ALTER TABLE [dbo].[C01ConsolEntry] ADD  DEFAULT ((0)) FOR [siType]
GO

ALTER TABLE [dbo].[C01ConsolEntry] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Concept]
GO

ALTER TABLE [dbo].[C01ConsolEntry] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[C01ConsolEntry] ADD  DEFAULT ((0)) FOR [Debits]
GO

ALTER TABLE [dbo].[C01ConsolEntry] ADD  DEFAULT ((0)) FOR [Credits]
GO

ALTER TABLE [dbo].[C01ConsolEntry] ADD  DEFAULT ((0)) FOR [AmountFOB]
GO

ALTER TABLE [dbo].[C01ConsolEntry] ADD  DEFAULT ((0)) FOR [CurrencyRate]
GO

ALTER TABLE [dbo].[C01ConsolEntry] ADD  DEFAULT ((0)) FOR [boCashFlow]
GO

ALTER TABLE [dbo].[C01ConsolEntry] ADD  DEFAULT ((0)) FOR [Status]
GO

ALTER TABLE [dbo].[C01ConsolEntry] ADD  DEFAULT ((0)) FOR [StatusConsol]
GO

ALTER TABLE [dbo].[C01ConsolEntry]  WITH CHECK ADD  CONSTRAINT [C01ConsolEntryFK01] FOREIGN KEY([siPeriod])
REFERENCES [dbo].[C01Periods] ([siPeriod])
GO

ALTER TABLE [dbo].[C01ConsolEntry] CHECK CONSTRAINT [C01ConsolEntryFK01]
GO

ALTER TABLE [dbo].[C01ConsolEntry]  WITH CHECK ADD  CONSTRAINT [C01ConsolEntryFK02] FOREIGN KEY([ID_Currency])
REFERENCES [dbo].[Currency] ([ID_Currency])
GO

ALTER TABLE [dbo].[C01ConsolEntry] CHECK CONSTRAINT [C01ConsolEntryFK02]
GO

ALTER TABLE [dbo].[C01ConsolEntry]  WITH CHECK ADD CHECK  (([AmountFOB]>=(0)))
GO

ALTER TABLE [dbo].[C01ConsolEntry]  WITH CHECK ADD CHECK  ((len([Concept])<>(0)))
GO

ALTER TABLE [dbo].[C01ConsolEntry]  WITH CHECK ADD CHECK  (([Credits]>=(0)))
GO

ALTER TABLE [dbo].[C01ConsolEntry]  WITH CHECK ADD CHECK  (([CurrencyRate]>=(0)))
GO

ALTER TABLE [dbo].[C01ConsolEntry]  WITH CHECK ADD CHECK  (([Debits]>=(0)))
GO

ALTER TABLE [dbo].[C01ConsolEntry]  WITH CHECK ADD CHECK  ((len([ID_Entry])<>(0)))
GO

ALTER TABLE [dbo].[C01ConsolEntry]  WITH CHECK ADD CHECK  ((len([ID_Origin])<>(0)))
GO

ALTER TABLE [dbo].[C01ConsolEntry]  WITH CHECK ADD CHECK  (([siType]=(1) OR [siType]=(0)))
GO

ALTER TABLE [dbo].[C01ConsolEntry]  WITH CHECK ADD CHECK  (([Status]=(1) OR [Status]=(0) OR [Status]=(-1)))
GO

ALTER TABLE [dbo].[C01ConsolEntry]  WITH CHECK ADD CHECK  (([TypeTrans]>=(0) AND [TypeTrans]<=(5)))
GO


