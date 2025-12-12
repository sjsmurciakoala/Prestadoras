USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01ConsolTrans]    Script Date: 10/12/2025 13:32:28 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01ConsolTrans](
	[ID_Origin] [varchar](5) NOT NULL,
	[siPeriod] [smallint] NOT NULL,
	[TypeTrans] [tinyint] NOT NULL,
	[ID_Entry] [varchar](10) NOT NULL,
	[ID_Trans] [int] NOT NULL,
	[ID_Account] [varchar](24) NOT NULL,
	[siType] [tinyint] NOT NULL,
	[TypeDoc] [smallint] NULL,
	[ID_Document] [varchar](20) NULL,
	[NDocLine] [smallint] NULL,
	[boGrouped] [bit] NOT NULL,
	[dtDate] [datetime] NOT NULL,
	[siMonth] [tinyint] NOT NULL,
	[Reference] [varchar](20) NULL,
	[dtDateTrc] [datetime] NOT NULL,
	[Detail] [varchar](120) NULL,
	[ID_Cost] [varchar](8) NULL,
	[ID_Class] [varchar](5) NULL,
	[ID_Budget] [varchar](10) NULL,
	[boCashFlow] [bit] NULL,
	[Amount] [decimal](28, 2) NOT NULL,
	[Cr] [smallint] NOT NULL,
	[Cash] [decimal](28, 2) NULL,
	[TaxBase] [decimal](28, 2) NULL,
	[ID_Currency] [varchar](3) NULL,
	[AmountFOB] [decimal](28, 2) NULL,
	[TaxBaseFOB] [decimal](28, 2) NULL,
	[CurrencyRate] [decimal](28, 9) NULL,
	[Status] [smallint] NOT NULL,
 CONSTRAINT [C01ConsolTrans0] PRIMARY KEY CLUSTERED 
(
	[ID_Origin] ASC,
	[siPeriod] ASC,
	[TypeTrans] ASC,
	[ID_Entry] ASC,
	[ID_Trans] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [TypeTrans]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [ID_Trans]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [siType]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [TypeDoc]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [NDocLine]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [boGrouped]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [siMonth]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT (getdate()) FOR [dtDateTrc]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [boCashFlow]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [Amount]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((1)) FOR [Cr]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [Cash]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [TaxBase]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [AmountFOB]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [TaxBaseFOB]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [CurrencyRate]
GO

ALTER TABLE [dbo].[C01ConsolTrans] ADD  DEFAULT ((0)) FOR [Status]
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH NOCHECK ADD  CONSTRAINT [C01ConsolTransFK01] FOREIGN KEY([ID_Origin], [siPeriod], [TypeTrans], [ID_Entry])
REFERENCES [dbo].[C01ConsolEntry] ([ID_Origin], [siPeriod], [TypeTrans], [ID_Entry])
GO

ALTER TABLE [dbo].[C01ConsolTrans] CHECK CONSTRAINT [C01ConsolTransFK01]
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD  CONSTRAINT [C01ConsolTransFK02] FOREIGN KEY([siPeriod])
REFERENCES [dbo].[C01Periods] ([siPeriod])
GO

ALTER TABLE [dbo].[C01ConsolTrans] CHECK CONSTRAINT [C01ConsolTransFK02]
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD  CONSTRAINT [C01ConsolTransFK03] FOREIGN KEY([ID_Account])
REFERENCES [dbo].[C01Account] ([ID_Account])
GO

ALTER TABLE [dbo].[C01ConsolTrans] CHECK CONSTRAINT [C01ConsolTransFK03]
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD  CONSTRAINT [C01ConsolTransFK04] FOREIGN KEY([ID_Cost])
REFERENCES [dbo].[C01CostCenter] ([ID_Cost])
GO

ALTER TABLE [dbo].[C01ConsolTrans] CHECK CONSTRAINT [C01ConsolTransFK04]
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD  CONSTRAINT [C01ConsolTransFK05] FOREIGN KEY([ID_Currency])
REFERENCES [dbo].[Currency] ([ID_Currency])
GO

ALTER TABLE [dbo].[C01ConsolTrans] CHECK CONSTRAINT [C01ConsolTransFK05]
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD CHECK  (([Amount]>=(0)))
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD CHECK  (([AmountFOB]>=(0)))
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD CHECK  (([CurrencyRate]>=(0)))
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD CHECK  ((len([ID_Account])<>(0)))
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD CHECK  ((len([ID_Entry])<>(0)))
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD CHECK  ((len([ID_Origin])<>(0)))
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD CHECK  (([siMonth]>=(1) AND [siMonth]<=(13)))
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD CHECK  (([siType]=(1) OR [siType]=(0)))
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD CHECK  (([Status]=(1) OR [Status]=(0) OR [Status]=(-1)))
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD CHECK  (([TaxBase]>=(0)))
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD CHECK  (([TaxBaseFOB]>=(0)))
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD CHECK  (([TypeTrans]>=(0) AND [TypeTrans]<=(5)))
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD CHECK  (([Cash]>=(0)))
GO

ALTER TABLE [dbo].[C01ConsolTrans]  WITH CHECK ADD CHECK  (([Cr]=(1) OR [Cr]=(-1)))
GO


