USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01Opening]    Script Date: 10/12/2025 13:41:32 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01Opening](
	[siPeriod] [smallint] NOT NULL,
	[TypeTrans] [tinyint] NOT NULL,
	[ID_Account] [varchar](24) NOT NULL,
	[Amount] [decimal](28, 2) NOT NULL,
	[Cr] [smallint] NOT NULL,
 CONSTRAINT [C01Opening0] PRIMARY KEY CLUSTERED 
(
	[siPeriod] ASC,
	[TypeTrans] ASC,
	[ID_Account] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01Opening] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01Opening] ADD  DEFAULT ((0)) FOR [TypeTrans]
GO

ALTER TABLE [dbo].[C01Opening] ADD  DEFAULT ((0)) FOR [Amount]
GO

ALTER TABLE [dbo].[C01Opening] ADD  DEFAULT ((1)) FOR [Cr]
GO

ALTER TABLE [dbo].[C01Opening]  WITH CHECK ADD  CONSTRAINT [C01OpeningFK01] FOREIGN KEY([ID_Account])
REFERENCES [dbo].[C01Account] ([ID_Account])
GO

ALTER TABLE [dbo].[C01Opening] CHECK CONSTRAINT [C01OpeningFK01]
GO

ALTER TABLE [dbo].[C01Opening]  WITH CHECK ADD  CONSTRAINT [C01OpeningFK02] FOREIGN KEY([siPeriod])
REFERENCES [dbo].[C01Periods] ([siPeriod])
GO

ALTER TABLE [dbo].[C01Opening] CHECK CONSTRAINT [C01OpeningFK02]
GO

ALTER TABLE [dbo].[C01Opening]  WITH CHECK ADD CHECK  (([Amount]>=(0)))
GO

ALTER TABLE [dbo].[C01Opening]  WITH CHECK ADD CHECK  ((len([ID_Account])<>(0)))
GO

ALTER TABLE [dbo].[C01Opening]  WITH CHECK ADD CHECK  (([TypeTrans]>=(0) AND [TypeTrans]<=(5)))
GO

ALTER TABLE [dbo].[C01Opening]  WITH CHECK ADD CHECK  (([Cr]=(1) OR [Cr]=(-1)))
GO


