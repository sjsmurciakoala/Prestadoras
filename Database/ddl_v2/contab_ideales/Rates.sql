USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[Rates]    Script Date: 10/12/2025 14:05:49 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Rates](
	[ID_Currency] [varchar](3) NOT NULL,
	[siYear] [smallint] NOT NULL,
	[siMonth] [tinyint] NOT NULL,
	[CurrencyRate] [decimal](28, 9) NOT NULL,
 CONSTRAINT [Rates0] PRIMARY KEY CLUSTERED 
(
	[ID_Currency] ASC,
	[siYear] ASC,
	[siMonth] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[Rates] ADD  DEFAULT ((0)) FOR [siYear]
GO

ALTER TABLE [dbo].[Rates] ADD  DEFAULT ((0)) FOR [siMonth]
GO

ALTER TABLE [dbo].[Rates] ADD  DEFAULT ((0)) FOR [CurrencyRate]
GO

ALTER TABLE [dbo].[Rates]  WITH CHECK ADD  CONSTRAINT [RatesFK01] FOREIGN KEY([ID_Currency])
REFERENCES [dbo].[Currency] ([ID_Currency])
GO

ALTER TABLE [dbo].[Rates] CHECK CONSTRAINT [RatesFK01]
GO

ALTER TABLE [dbo].[Rates]  WITH CHECK ADD CHECK  (([siMonth]>=(1) AND [siMonth]<=(12)))
GO


