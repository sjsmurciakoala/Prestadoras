USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01Policys]    Script Date: 10/12/2025 13:43:18 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01Policys](
	[ID_Policy] [varchar](8) NOT NULL,
	[NrPolicy] [varchar](10) NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
	[dtPurchase] [datetime] NOT NULL,
	[dtEnd] [datetime] NOT NULL,
	[ID_Third] [varchar](30) NULL,
	[Price] [decimal](28, 2) NOT NULL,
	[Amount] [decimal](28, 2) NOT NULL,
	[Deduct] [decimal](28, 2) NOT NULL,
	[boStatus] [bit] NOT NULL,
 CONSTRAINT [C01Policys0] PRIMARY KEY CLUSTERED 
(
	[ID_Policy] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01Policys] ADD  DEFAULT (getdate()) FOR [dtPurchase]
GO

ALTER TABLE [dbo].[C01Policys] ADD  DEFAULT (getdate()) FOR [dtEnd]
GO

ALTER TABLE [dbo].[C01Policys] ADD  DEFAULT ((1)) FOR [Price]
GO

ALTER TABLE [dbo].[C01Policys] ADD  DEFAULT ((1)) FOR [Amount]
GO

ALTER TABLE [dbo].[C01Policys] ADD  DEFAULT ((0)) FOR [Deduct]
GO

ALTER TABLE [dbo].[C01Policys] ADD  DEFAULT ((1)) FOR [boStatus]
GO

ALTER TABLE [dbo].[C01Policys]  WITH CHECK ADD  CONSTRAINT [C01PolicysFK01] FOREIGN KEY([ID_Third])
REFERENCES [dbo].[C01Thirds] ([ID_Third])
GO

ALTER TABLE [dbo].[C01Policys] CHECK CONSTRAINT [C01PolicysFK01]
GO

ALTER TABLE [dbo].[C01Policys]  WITH CHECK ADD CHECK  (([Amount]>(0)))
GO

ALTER TABLE [dbo].[C01Policys]  WITH CHECK ADD CHECK  (([Deduct]>=(0)))
GO

ALTER TABLE [dbo].[C01Policys]  WITH CHECK ADD CHECK  (([NrPolicy]>(0)))
GO

ALTER TABLE [dbo].[C01Policys]  WITH CHECK ADD CHECK  (([Price]>(0)))
GO


