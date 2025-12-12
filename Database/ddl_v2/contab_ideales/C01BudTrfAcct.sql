USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01BudTrfAcct]    Script Date: 10/12/2025 13:29:41 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01BudTrfAcct](
	[siPeriod] [smallint] NOT NULL,
	[ID_TrfActy] [varchar](6) NOT NULL,
	[ID_FromBudget] [varchar](10) NOT NULL,
	[ID_ToBudget] [varchar](10) NOT NULL,
	[dtDate] [datetime] NOT NULL,
	[Note] [text] NULL,
	[Amount] [decimal](28, 2) NOT NULL,
	[Cr] [smallint] NOT NULL,
 CONSTRAINT [C01BudTrfAcct0] PRIMARY KEY CLUSTERED 
(
	[siPeriod] ASC,
	[ID_TrfActy] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01BudTrfAcct] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01BudTrfAcct] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[C01BudTrfAcct] ADD  DEFAULT ((0)) FOR [Amount]
GO

ALTER TABLE [dbo].[C01BudTrfAcct] ADD  DEFAULT ((1)) FOR [Cr]
GO

ALTER TABLE [dbo].[C01BudTrfAcct]  WITH CHECK ADD  CONSTRAINT [C01BudTrfAcctFK01] FOREIGN KEY([siPeriod])
REFERENCES [dbo].[C01Periods] ([siPeriod])
GO

ALTER TABLE [dbo].[C01BudTrfAcct] CHECK CONSTRAINT [C01BudTrfAcctFK01]
GO

ALTER TABLE [dbo].[C01BudTrfAcct]  WITH CHECK ADD CHECK  (([Cr]=(1) OR [Cr]=(-1)))
GO


