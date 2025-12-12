USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01AcctBalance]    Script Date: 10/12/2025 13:15:39 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01AcctBalance](
	[siPeriod] [smallint] NOT NULL,
	[TypeTrans] [tinyint] NOT NULL,
	[ID_Account] [varchar](24) NOT NULL,
	[siMonth] [tinyint] NOT NULL,
	[Debits] [decimal](28, 2) NULL,
	[Credits] [decimal](28, 2) NULL,
	[Budget] [decimal](28, 2) NULL,
	[NDebits] [int] NULL,
	[NCredits] [int] NULL,
 CONSTRAINT [C01AcctBalance0] PRIMARY KEY CLUSTERED 
(
	[siPeriod] ASC,
	[TypeTrans] ASC,
	[ID_Account] ASC,
	[siMonth] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01AcctBalance] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01AcctBalance] ADD  DEFAULT ((0)) FOR [TypeTrans]
GO

ALTER TABLE [dbo].[C01AcctBalance] ADD  DEFAULT ((0)) FOR [siMonth]
GO

ALTER TABLE [dbo].[C01AcctBalance] ADD  DEFAULT ((0)) FOR [Debits]
GO

ALTER TABLE [dbo].[C01AcctBalance] ADD  DEFAULT ((0)) FOR [Credits]
GO

ALTER TABLE [dbo].[C01AcctBalance] ADD  DEFAULT ((0)) FOR [Budget]
GO

ALTER TABLE [dbo].[C01AcctBalance] ADD  DEFAULT ((0)) FOR [NDebits]
GO

ALTER TABLE [dbo].[C01AcctBalance] ADD  DEFAULT ((0)) FOR [NCredits]
GO

ALTER TABLE [dbo].[C01AcctBalance]  WITH CHECK ADD  CONSTRAINT [C01AcctBalanceFK01] FOREIGN KEY([ID_Account])
REFERENCES [dbo].[C01Account] ([ID_Account])
GO

ALTER TABLE [dbo].[C01AcctBalance] CHECK CONSTRAINT [C01AcctBalanceFK01]
GO

ALTER TABLE [dbo].[C01AcctBalance]  WITH CHECK ADD  CONSTRAINT [C01AcctBalanceFK02] FOREIGN KEY([siPeriod])
REFERENCES [dbo].[C01Periods] ([siPeriod])
GO

ALTER TABLE [dbo].[C01AcctBalance] CHECK CONSTRAINT [C01AcctBalanceFK02]
GO

ALTER TABLE [dbo].[C01AcctBalance]  WITH CHECK ADD CHECK  (([Credits]<=(0)))
GO

ALTER TABLE [dbo].[C01AcctBalance]  WITH CHECK ADD CHECK  (([Debits]>=(0)))
GO

ALTER TABLE [dbo].[C01AcctBalance]  WITH CHECK ADD CHECK  ((len([ID_Account])<>(0)))
GO

ALTER TABLE [dbo].[C01AcctBalance]  WITH CHECK ADD CHECK  (([siMonth]>=(1) AND [siMonth]<=(13)))
GO

ALTER TABLE [dbo].[C01AcctBalance]  WITH CHECK ADD CHECK  (([TypeTrans]>=(0) AND [TypeTrans]<=(5)))
GO


