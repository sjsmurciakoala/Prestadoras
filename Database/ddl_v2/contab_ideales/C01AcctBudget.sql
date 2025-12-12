USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01AcctBudget]    Script Date: 10/12/2025 13:16:30 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01AcctBudget](
	[KeyBud] [int] IDENTITY(1,1) NOT NULL,
	[siPeriod] [smallint] NOT NULL,
	[ID_Account] [varchar](24) NOT NULL,
	[Budget] [decimal](28, 2) NULL,
	[LastBudget] [decimal](28, 2) NULL,
	[siMonth] [tinyint] NOT NULL,
	[Status] [smallint] NOT NULL,
 CONSTRAINT [C01AcctBudget0] PRIMARY KEY CLUSTERED 
(
	[KeyBud] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01AcctBudget] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01AcctBudget] ADD  DEFAULT ((0)) FOR [Budget]
GO

ALTER TABLE [dbo].[C01AcctBudget] ADD  DEFAULT ((0)) FOR [LastBudget]
GO

ALTER TABLE [dbo].[C01AcctBudget] ADD  DEFAULT ((0)) FOR [siMonth]
GO

ALTER TABLE [dbo].[C01AcctBudget] ADD  DEFAULT ((0)) FOR [Status]
GO

ALTER TABLE [dbo].[C01AcctBudget]  WITH CHECK ADD  CONSTRAINT [C01AcctBudgetFK01] FOREIGN KEY([ID_Account])
REFERENCES [dbo].[C01Account] ([ID_Account])
GO

ALTER TABLE [dbo].[C01AcctBudget] CHECK CONSTRAINT [C01AcctBudgetFK01]
GO

ALTER TABLE [dbo].[C01AcctBudget]  WITH CHECK ADD  CONSTRAINT [C01AcctBudgetFK02] FOREIGN KEY([siPeriod])
REFERENCES [dbo].[C01Periods] ([siPeriod])
GO

ALTER TABLE [dbo].[C01AcctBudget] CHECK CONSTRAINT [C01AcctBudgetFK02]
GO

ALTER TABLE [dbo].[C01AcctBudget]  WITH CHECK ADD CHECK  ((len([ID_Account])<>(0)))
GO

ALTER TABLE [dbo].[C01AcctBudget]  WITH CHECK ADD CHECK  (([siMonth]>=(1) AND [siMonth]<=(13)))
GO


