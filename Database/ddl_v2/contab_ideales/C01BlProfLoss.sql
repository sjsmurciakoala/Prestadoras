USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01BlProfLoss]    Script Date: 10/12/2025 13:16:48 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01BlProfLoss](
	[siPeriod] [smallint] NOT NULL,
	[NLine] [smallint] NOT NULL,
	[siType] [tinyint] NOT NULL,
	[ColumOffset] [tinyint] NULL,
	[ID_Account] [varchar](24) NULL,
	[Descrip] [varchar](80) NOT NULL,
 CONSTRAINT [C01BlProfLoss0] PRIMARY KEY CLUSTERED 
(
	[siPeriod] ASC,
	[NLine] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01BlProfLoss] ADD  DEFAULT ((0)) FOR [siPeriod]
GO

ALTER TABLE [dbo].[C01BlProfLoss] ADD  DEFAULT ((0)) FOR [NLine]
GO

ALTER TABLE [dbo].[C01BlProfLoss] ADD  DEFAULT ((0)) FOR [siType]
GO

ALTER TABLE [dbo].[C01BlProfLoss] ADD  DEFAULT (NULL) FOR [ColumOffset]
GO

ALTER TABLE [dbo].[C01BlProfLoss] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[C01BlProfLoss]  WITH CHECK ADD  CONSTRAINT [C01BlProfLossFK01] FOREIGN KEY([ID_Account])
REFERENCES [dbo].[C01Account] ([ID_Account])
GO

ALTER TABLE [dbo].[C01BlProfLoss] CHECK CONSTRAINT [C01BlProfLossFK01]
GO

ALTER TABLE [dbo].[C01BlProfLoss]  WITH CHECK ADD  CONSTRAINT [C01BlProfLossFK02] FOREIGN KEY([siPeriod])
REFERENCES [dbo].[C01Periods] ([siPeriod])
GO

ALTER TABLE [dbo].[C01BlProfLoss] CHECK CONSTRAINT [C01BlProfLossFK02]
GO

ALTER TABLE [dbo].[C01BlProfLoss]  WITH CHECK ADD CHECK  (([ColumOffset]=(1) OR [ColumOffset]=(0)))
GO

ALTER TABLE [dbo].[C01BlProfLoss]  WITH CHECK ADD CHECK  (([siType]=(2) OR [siType]=(1) OR [siType]=(0)))
GO


