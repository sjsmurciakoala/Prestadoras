USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01TransClass]    Script Date: 10/12/2025 13:47:37 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01TransClass](
	[ID_Class] [varchar](5) NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
	[TypeTrans] [tinyint] NOT NULL,
	[siTypeOper] [tinyint] NOT NULL,
	[NrEntries] [int] NOT NULL,
	[Frequency] [tinyint] NOT NULL,
	[boCostCenter] [bit] NOT NULL,
	[boAccount] [bit] NOT NULL,
	[boThird] [bit] NOT NULL,
	[boCashFlow] [bit] NOT NULL,
	[boDefault] [bit] NOT NULL,
	[boStatus] [bit] NOT NULL,
 CONSTRAINT [C01TransClass0] PRIMARY KEY CLUSTERED 
(
	[ID_Class] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01TransClass] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[C01TransClass] ADD  DEFAULT ((0)) FOR [TypeTrans]
GO

ALTER TABLE [dbo].[C01TransClass] ADD  DEFAULT ((0)) FOR [siTypeOper]
GO

ALTER TABLE [dbo].[C01TransClass] ADD  DEFAULT ((1)) FOR [NrEntries]
GO

ALTER TABLE [dbo].[C01TransClass] ADD  DEFAULT ((0)) FOR [Frequency]
GO

ALTER TABLE [dbo].[C01TransClass] ADD  DEFAULT ((0)) FOR [boCostCenter]
GO

ALTER TABLE [dbo].[C01TransClass] ADD  DEFAULT ((0)) FOR [boAccount]
GO

ALTER TABLE [dbo].[C01TransClass] ADD  DEFAULT ((1)) FOR [boThird]
GO

ALTER TABLE [dbo].[C01TransClass] ADD  DEFAULT ((0)) FOR [boCashFlow]
GO

ALTER TABLE [dbo].[C01TransClass] ADD  DEFAULT ((1)) FOR [boDefault]
GO

ALTER TABLE [dbo].[C01TransClass] ADD  DEFAULT ((1)) FOR [boStatus]
GO

ALTER TABLE [dbo].[C01TransClass]  WITH CHECK ADD CHECK  (([Frequency]>=(0) AND [Frequency]<=(2)))
GO

ALTER TABLE [dbo].[C01TransClass]  WITH CHECK ADD CHECK  (([NrEntries]>=(0)))
GO

ALTER TABLE [dbo].[C01TransClass]  WITH CHECK ADD CHECK  (([siTypeOper]>=(0) AND [siTypeOper]<=(10)))
GO

ALTER TABLE [dbo].[C01TransClass]  WITH CHECK ADD CHECK  (([TypeTrans]>=(0) AND [TypeTrans]<=(5)))
GO


