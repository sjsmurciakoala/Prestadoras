USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01LicSoft]    Script Date: 10/12/2025 13:40:39 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01LicSoft](
	[ID_Soft] [varchar](6) NOT NULL,
	[ID_Fix] [varchar](15) NULL,
	[ID_Third] [varchar](30) NULL,
	[Descrip] [varchar](80) NOT NULL,
	[Factory] [varchar](40) NOT NULL,
	[Price] [decimal](28, 2) NULL,
	[Version] [varchar](8) NOT NULL,
	[OpSystem] [varchar](20) NOT NULL,
	[CDSerial] [varchar](20) NOT NULL,
	[CDKey] [varchar](20) NULL,
	[CDReg] [varchar](20) NULL,
	[dtPurchase] [datetime] NULL,
	[dtInstall] [datetime] NULL,
	[dtUpDate] [datetime] NULL,
	[dtEnd] [datetime] NULL,
	[CDUse] [varchar](30) NULL,
	[Status] [smallint] NOT NULL,
	[boStatus] [bit] NOT NULL,
 CONSTRAINT [C01LicSoft0] PRIMARY KEY CLUSTERED 
(
	[ID_Soft] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01LicSoft] ADD  DEFAULT ((0)) FOR [Price]
GO

ALTER TABLE [dbo].[C01LicSoft] ADD  DEFAULT ('Windows') FOR [OpSystem]
GO

ALTER TABLE [dbo].[C01LicSoft] ADD  DEFAULT (getdate()) FOR [dtPurchase]
GO

ALTER TABLE [dbo].[C01LicSoft] ADD  DEFAULT (getdate()) FOR [dtInstall]
GO

ALTER TABLE [dbo].[C01LicSoft] ADD  DEFAULT (getdate()) FOR [dtUpDate]
GO

ALTER TABLE [dbo].[C01LicSoft] ADD  DEFAULT (getdate()) FOR [dtEnd]
GO

ALTER TABLE [dbo].[C01LicSoft] ADD  DEFAULT ((1)) FOR [Status]
GO

ALTER TABLE [dbo].[C01LicSoft] ADD  DEFAULT ((1)) FOR [boStatus]
GO

ALTER TABLE [dbo].[C01LicSoft]  WITH CHECK ADD  CONSTRAINT [C01LicSoftFK01] FOREIGN KEY([ID_Third])
REFERENCES [dbo].[C01Thirds] ([ID_Third])
GO

ALTER TABLE [dbo].[C01LicSoft] CHECK CONSTRAINT [C01LicSoftFK01]
GO

ALTER TABLE [dbo].[C01LicSoft]  WITH CHECK ADD  CONSTRAINT [C01LicSoftFK02] FOREIGN KEY([ID_Fix])
REFERENCES [dbo].[C01FixedAssest] ([ID_Fix])
GO

ALTER TABLE [dbo].[C01LicSoft] CHECK CONSTRAINT [C01LicSoftFK02]
GO

ALTER TABLE [dbo].[C01LicSoft]  WITH CHECK ADD CHECK  (([Status]>=(1) AND [Status]<=(3)))
GO


