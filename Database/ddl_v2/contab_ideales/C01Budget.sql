USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01Budget]    Script Date: 10/12/2025 13:17:32 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01Budget](
	[siPeriod] [smallint] NOT NULL,
	[ID_Budget] [varchar](10) NOT NULL,
	[ID_Program] [varchar](10) NOT NULL,
	[ID_Account] [varchar](24) NOT NULL,
	[dtDate] [datetime] NOT NULL,
	[Detail] [varchar](120) NULL,
	[BudDebit] [decimal](28, 2) NULL,
	[BudCredit] [decimal](28, 2) NULL,
	[AddDebit] [decimal](28, 2) NULL,
	[AddCredit] [decimal](28, 2) NULL,
	[Reserved] [tinyint] NULL,
	[boStatus] [bit] NOT NULL,
 CONSTRAINT [C01Budget0] PRIMARY KEY CLUSTERED 
(
	[ID_Budget] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01Budget] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01Budget] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[C01Budget] ADD  DEFAULT ((0)) FOR [BudDebit]
GO

ALTER TABLE [dbo].[C01Budget] ADD  DEFAULT ((0)) FOR [BudCredit]
GO

ALTER TABLE [dbo].[C01Budget] ADD  DEFAULT ((0)) FOR [AddDebit]
GO

ALTER TABLE [dbo].[C01Budget] ADD  DEFAULT ((0)) FOR [AddCredit]
GO

ALTER TABLE [dbo].[C01Budget] ADD  DEFAULT ((0)) FOR [Reserved]
GO

ALTER TABLE [dbo].[C01Budget] ADD  DEFAULT ((1)) FOR [boStatus]
GO

ALTER TABLE [dbo].[C01Budget]  WITH CHECK ADD  CONSTRAINT [C01BudgetFK01] FOREIGN KEY([siPeriod])
REFERENCES [dbo].[C01Periods] ([siPeriod])
GO

ALTER TABLE [dbo].[C01Budget] CHECK CONSTRAINT [C01BudgetFK01]
GO

ALTER TABLE [dbo].[C01Budget]  WITH CHECK ADD  CONSTRAINT [C01BudgetFK02] FOREIGN KEY([ID_Account])
REFERENCES [dbo].[C01Account] ([ID_Account])
GO

ALTER TABLE [dbo].[C01Budget] CHECK CONSTRAINT [C01BudgetFK02]
GO

ALTER TABLE [dbo].[C01Budget]  WITH CHECK ADD CHECK  (([AddCredit]>=(0)))
GO

ALTER TABLE [dbo].[C01Budget]  WITH CHECK ADD CHECK  (([AddDebit]>=(0)))
GO

ALTER TABLE [dbo].[C01Budget]  WITH CHECK ADD CHECK  (([BudCredit]>=(0)))
GO

ALTER TABLE [dbo].[C01Budget]  WITH CHECK ADD CHECK  (([BudDebit]>=(0)))
GO


