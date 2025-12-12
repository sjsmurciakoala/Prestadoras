USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01Entry]    Script Date: 10/12/2025 13:37:12 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01Entry](
	[KeyEntry] [int] IDENTITY(1,1) NOT NULL,
	[siPeriod] [smallint] NOT NULL,
	[TypeTrans] [tinyint] NOT NULL,
	[ID_Entry] [varchar](10) NOT NULL,
	[siType] [tinyint] NOT NULL,
	[Concept] [varchar](50) NOT NULL,
	[dtDate] [datetime] NOT NULL,
	[ID_Class] [varchar](5) NOT NULL,
	[ID_Budget] [varchar](10) NULL,
	[ID_Cost] [varchar](8) NULL,
	[ID_Origin] [varchar](5) NULL,
	[Debits] [decimal](28, 2) NOT NULL,
	[Credits] [decimal](28, 2) NOT NULL,
	[NDebits] [int] NOT NULL,
	[NCredits] [int] NOT NULL,
	[CashDebits] [decimal](28, 2) NOT NULL,
	[CashCredits] [decimal](28, 2) NOT NULL,
	[NCashDebits] [int] NOT NULL,
	[NCashCredits] [int] NOT NULL,
	[Note] [text] NULL,
	[boAlert] [bit] NULL,
	[boCashFlow] [bit] NULL,
	[Status] [smallint] NOT NULL,
 CONSTRAINT [C01Entry0] PRIMARY KEY CLUSTERED 
(
	[KeyEntry] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT ((0)) FOR [TypeTrans]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT ((0)) FOR [siType]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Concept]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT ((0)) FOR [Debits]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT ((0)) FOR [Credits]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT ((0)) FOR [NDebits]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT ((0)) FOR [NCredits]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT ((0)) FOR [CashDebits]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT ((0)) FOR [CashCredits]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT ((0)) FOR [NCashDebits]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT ((0)) FOR [NCashCredits]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT ((0)) FOR [boAlert]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT ((0)) FOR [boCashFlow]
GO

ALTER TABLE [dbo].[C01Entry] ADD  DEFAULT ((0)) FOR [Status]
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD  CONSTRAINT [C01EntryFK01] FOREIGN KEY([siPeriod])
REFERENCES [dbo].[C01Periods] ([siPeriod])
GO

ALTER TABLE [dbo].[C01Entry] CHECK CONSTRAINT [C01EntryFK01]
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD  CONSTRAINT [C01EntryFK02] FOREIGN KEY([ID_Class])
REFERENCES [dbo].[C01TransClass] ([ID_Class])
GO

ALTER TABLE [dbo].[C01Entry] CHECK CONSTRAINT [C01EntryFK02]
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD  CONSTRAINT [C01EntryFK03] FOREIGN KEY([ID_Cost])
REFERENCES [dbo].[C01CostCenter] ([ID_Cost])
GO

ALTER TABLE [dbo].[C01Entry] CHECK CONSTRAINT [C01EntryFK03]
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD  CONSTRAINT [C01EntryFK04] FOREIGN KEY([ID_Budget])
REFERENCES [dbo].[C01Budget] ([ID_Budget])
GO

ALTER TABLE [dbo].[C01Entry] CHECK CONSTRAINT [C01EntryFK04]
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD  CONSTRAINT [C01EntryFK05] FOREIGN KEY([ID_Origin])
REFERENCES [dbo].[C01ConsolOrigin] ([ID_Origin])
GO

ALTER TABLE [dbo].[C01Entry] CHECK CONSTRAINT [C01EntryFK05]
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD CHECK  (([CashCredits]>=(0)))
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD CHECK  (([CashDebits]>=(0)))
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD CHECK  ((len([Concept])<>(0)))
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD CHECK  (([Credits]>=(0)))
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD CHECK  (([Debits]>=(0)))
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD CHECK  ((len([ID_Class])<>(0)))
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD CHECK  ((len([ID_Entry])<>(0)))
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD CHECK  (([NCashCredits]>=(0)))
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD CHECK  (([NCashDebits]>=(0)))
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD CHECK  (([NCredits]>=(0)))
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD CHECK  (([NDebits]>=(0)))
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD CHECK  (([siType]=(1) OR [siType]=(0)))
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD CHECK  (([Status]=(1) OR [Status]=(0) OR [Status]=(-1)))
GO

ALTER TABLE [dbo].[C01Entry]  WITH CHECK ADD CHECK  (([TypeTrans]>=(0) AND [TypeTrans]<=(5)))
GO


