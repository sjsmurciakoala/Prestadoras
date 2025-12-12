USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01BudTrfPrg]    Script Date: 10/12/2025 13:30:08 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01BudTrfPrg](
	[siPeriod] [smallint] NOT NULL,
	[ID_TrfPrg] [varchar](6) NOT NULL,
	[ID_FromPrg] [varchar](10) NOT NULL,
	[ID_ToPrg] [varchar](10) NOT NULL,
	[Amount] [decimal](28, 2) NOT NULL,
	[Cr] [smallint] NOT NULL,
	[dtDate] [datetime] NOT NULL,
	[Note] [text] NULL,
 CONSTRAINT [C01BudTrfPrg0] PRIMARY KEY CLUSTERED 
(
	[siPeriod] ASC,
	[ID_TrfPrg] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01BudTrfPrg] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01BudTrfPrg] ADD  DEFAULT ((0)) FOR [Amount]
GO

ALTER TABLE [dbo].[C01BudTrfPrg] ADD  DEFAULT ((1)) FOR [Cr]
GO

ALTER TABLE [dbo].[C01BudTrfPrg] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[C01BudTrfPrg]  WITH CHECK ADD  CONSTRAINT [C01BudTrfPrgFK01] FOREIGN KEY([siPeriod])
REFERENCES [dbo].[C01Periods] ([siPeriod])
GO

ALTER TABLE [dbo].[C01BudTrfPrg] CHECK CONSTRAINT [C01BudTrfPrgFK01]
GO

ALTER TABLE [dbo].[C01BudTrfPrg]  WITH CHECK ADD CHECK  (([Cr]=(1) OR [Cr]=(-1)))
GO


