USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01CostOpening]    Script Date: 10/12/2025 13:34:36 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01CostOpening](
	[siPeriod] [smallint] NOT NULL,
	[TypeTrans] [tinyint] NOT NULL,
	[ID_Cost] [varchar](8) NOT NULL,
	[ID_Account] [varchar](24) NOT NULL,
	[Amount] [decimal](28, 2) NOT NULL,
	[Cr] [smallint] NOT NULL,
 CONSTRAINT [C01CostOpening0] PRIMARY KEY CLUSTERED 
(
	[siPeriod] ASC,
	[TypeTrans] ASC,
	[ID_Cost] ASC,
	[ID_Account] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01CostOpening] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01CostOpening] ADD  DEFAULT ((0)) FOR [TypeTrans]
GO

ALTER TABLE [dbo].[C01CostOpening] ADD  DEFAULT ((0)) FOR [Amount]
GO

ALTER TABLE [dbo].[C01CostOpening] ADD  DEFAULT ((1)) FOR [Cr]
GO

ALTER TABLE [dbo].[C01CostOpening]  WITH CHECK ADD  CONSTRAINT [C01CostOpeningFK01] FOREIGN KEY([ID_Cost])
REFERENCES [dbo].[C01CostCenter] ([ID_Cost])
GO

ALTER TABLE [dbo].[C01CostOpening] CHECK CONSTRAINT [C01CostOpeningFK01]
GO

ALTER TABLE [dbo].[C01CostOpening]  WITH CHECK ADD  CONSTRAINT [C01CostOpeningFK02] FOREIGN KEY([siPeriod])
REFERENCES [dbo].[C01Periods] ([siPeriod])
GO

ALTER TABLE [dbo].[C01CostOpening] CHECK CONSTRAINT [C01CostOpeningFK02]
GO

ALTER TABLE [dbo].[C01CostOpening]  WITH CHECK ADD  CONSTRAINT [C01CostOpeningFK03] FOREIGN KEY([ID_Account])
REFERENCES [dbo].[C01Account] ([ID_Account])
GO

ALTER TABLE [dbo].[C01CostOpening] CHECK CONSTRAINT [C01CostOpeningFK03]
GO

ALTER TABLE [dbo].[C01CostOpening]  WITH CHECK ADD CHECK  (([Amount]>=(0)))
GO

ALTER TABLE [dbo].[C01CostOpening]  WITH CHECK ADD CHECK  (([ID_Account]<>(0)))
GO

ALTER TABLE [dbo].[C01CostOpening]  WITH CHECK ADD CHECK  (([TypeTrans]>=(0) AND [TypeTrans]<=(5)))
GO

ALTER TABLE [dbo].[C01CostOpening]  WITH CHECK ADD CHECK  (([Cr]=(1) OR [Cr]=(-1)))
GO


