USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01ConsolTransThird]    Script Date: 10/12/2025 13:32:57 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01ConsolTransThird](
	[ID_Origin] [varchar](5) NOT NULL,
	[siPeriod] [smallint] NOT NULL,
	[TypeTrans] [tinyint] NOT NULL,
	[ID_Entry] [varchar](10) NOT NULL,
	[ID_Trans] [int] NOT NULL,
	[NLine] [int] IDENTITY(1,1) NOT NULL,
	[ID_Third] [varchar](30) NULL,
	[siType] [tinyint] NOT NULL,
	[ID_Account] [varchar](24) NULL,
	[Amount] [decimal](28, 2) NOT NULL,
	[ID_Cost] [varchar](8) NULL,
	[Cr] [smallint] NOT NULL,
	[TaxBase] [decimal](28, 2) NULL,
	[dtDate] [datetime] NOT NULL,
	[AmountFOB] [decimal](28, 2) NULL,
	[TaxBaseFOB] [decimal](28, 2) NULL,
	[Reference] [varchar](20) NULL,
	[Detail] [varchar](120) NULL,
	[Status] [smallint] NOT NULL,
 CONSTRAINT [C01ConsolTransThird0] PRIMARY KEY CLUSTERED 
(
	[ID_Origin] ASC,
	[siPeriod] ASC,
	[TypeTrans] ASC,
	[ID_Entry] ASC,
	[ID_Trans] ASC,
	[NLine] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01ConsolTransThird] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01ConsolTransThird] ADD  DEFAULT ((0)) FOR [TypeTrans]
GO

ALTER TABLE [dbo].[C01ConsolTransThird] ADD  DEFAULT ((0)) FOR [ID_Trans]
GO

ALTER TABLE [dbo].[C01ConsolTransThird] ADD  DEFAULT ((0)) FOR [siType]
GO

ALTER TABLE [dbo].[C01ConsolTransThird] ADD  DEFAULT ((0)) FOR [Amount]
GO

ALTER TABLE [dbo].[C01ConsolTransThird] ADD  DEFAULT ((1)) FOR [Cr]
GO

ALTER TABLE [dbo].[C01ConsolTransThird] ADD  DEFAULT ((0)) FOR [TaxBase]
GO

ALTER TABLE [dbo].[C01ConsolTransThird] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[C01ConsolTransThird] ADD  DEFAULT ((0)) FOR [AmountFOB]
GO

ALTER TABLE [dbo].[C01ConsolTransThird] ADD  DEFAULT ((0)) FOR [TaxBaseFOB]
GO

ALTER TABLE [dbo].[C01ConsolTransThird] ADD  DEFAULT ((0)) FOR [Status]
GO

ALTER TABLE [dbo].[C01ConsolTransThird]  WITH NOCHECK ADD  CONSTRAINT [C01ConsolTransThirdFK01] FOREIGN KEY([ID_Origin], [siPeriod], [TypeTrans], [ID_Entry], [ID_Trans])
REFERENCES [dbo].[C01ConsolTrans] ([ID_Origin], [siPeriod], [TypeTrans], [ID_Entry], [ID_Trans])
GO

ALTER TABLE [dbo].[C01ConsolTransThird] CHECK CONSTRAINT [C01ConsolTransThirdFK01]
GO

ALTER TABLE [dbo].[C01ConsolTransThird]  WITH CHECK ADD  CONSTRAINT [C01ConsolTransThirdFK02] FOREIGN KEY([siPeriod])
REFERENCES [dbo].[C01Periods] ([siPeriod])
GO

ALTER TABLE [dbo].[C01ConsolTransThird] CHECK CONSTRAINT [C01ConsolTransThirdFK02]
GO

ALTER TABLE [dbo].[C01ConsolTransThird]  WITH CHECK ADD  CONSTRAINT [C01ConsolTransThirdFK03] FOREIGN KEY([ID_Account])
REFERENCES [dbo].[C01Account] ([ID_Account])
GO

ALTER TABLE [dbo].[C01ConsolTransThird] CHECK CONSTRAINT [C01ConsolTransThirdFK03]
GO

ALTER TABLE [dbo].[C01ConsolTransThird]  WITH CHECK ADD  CONSTRAINT [C01ConsolTransThirdFK04] FOREIGN KEY([ID_Third])
REFERENCES [dbo].[C01Thirds] ([ID_Third])
GO

ALTER TABLE [dbo].[C01ConsolTransThird] CHECK CONSTRAINT [C01ConsolTransThirdFK04]
GO

ALTER TABLE [dbo].[C01ConsolTransThird]  WITH CHECK ADD  CONSTRAINT [C01ConsolTransThirdFK05] FOREIGN KEY([ID_Origin])
REFERENCES [dbo].[C01ConsolOrigin] ([ID_Origin])
GO

ALTER TABLE [dbo].[C01ConsolTransThird] CHECK CONSTRAINT [C01ConsolTransThirdFK05]
GO

ALTER TABLE [dbo].[C01ConsolTransThird]  WITH CHECK ADD CHECK  (([Amount]>=(0)))
GO

ALTER TABLE [dbo].[C01ConsolTransThird]  WITH CHECK ADD CHECK  (([AmountFOB]>=(0)))
GO

ALTER TABLE [dbo].[C01ConsolTransThird]  WITH CHECK ADD CHECK  ((len([ID_Origin])<>(0)))
GO

ALTER TABLE [dbo].[C01ConsolTransThird]  WITH CHECK ADD CHECK  (([siType]=(1) OR [siType]=(0)))
GO

ALTER TABLE [dbo].[C01ConsolTransThird]  WITH CHECK ADD CHECK  (([Status]=(1) OR [Status]=(0) OR [Status]=(-1)))
GO

ALTER TABLE [dbo].[C01ConsolTransThird]  WITH CHECK ADD CHECK  (([TaxBase]>=(0)))
GO

ALTER TABLE [dbo].[C01ConsolTransThird]  WITH CHECK ADD CHECK  (([TaxBaseFOB]>=(0)))
GO

ALTER TABLE [dbo].[C01ConsolTransThird]  WITH CHECK ADD CHECK  (([TypeTrans]>=(0) AND [TypeTrans]<=(5)))
GO

ALTER TABLE [dbo].[C01ConsolTransThird]  WITH CHECK ADD CHECK  (([Cr]=(1) OR [Cr]=(-1)))
GO


