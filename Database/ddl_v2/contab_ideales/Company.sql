USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[Company]    Script Date: 10/12/2025 13:56:05 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Company](
	[ID_Entity] [varchar](10) NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
	[IDLabel] [varchar](15) NOT NULL,
	[IDFiscal] [varchar](15) NULL,
	[ID_TypeEntity] [varchar](3) NOT NULL,
	[EtySize] [tinyint] NOT NULL,
	[EtyCapital] [tinyint] NOT NULL,
	[Contact] [varchar](40) NULL,
	[Address] [varchar](40) NULL,
	[Address2] [varchar](40) NULL,
	[Phone] [varchar](25) NULL,
	[Email] [varchar](40) NULL,
	[WEB] [varchar](40) NULL,
	[City] [varchar](30) NULL,
	[Country] [varchar](30) NULL,
	[dtDate] [datetime] NULL,
	[siPeriodDef] [smallint] NOT NULL,
	[boStatus] [bit] NOT NULL,
	[DBPassword] [varchar](25) NULL,
	[MaskCode] [varchar](25) NULL,
	[EtyConsol] [smallint] NOT NULL,
	[ID_Master] [varchar](10) NULL,
	[dtMigrated] [datetime] NULL,
	[Reserved] [varchar](40) NOT NULL,
	[Prefix] [smallint] NOT NULL,
	[dtCCDate] [datetime] NULL,
 CONSTRAINT [Company0] PRIMARY KEY CLUSTERED 
(
	[ID_Entity] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[Company] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[Company] ADD  DEFAULT ('ID Fiscal') FOR [IDLabel]
GO

ALTER TABLE [dbo].[Company] ADD  DEFAULT ((0)) FOR [EtySize]
GO

ALTER TABLE [dbo].[Company] ADD  DEFAULT ((0)) FOR [EtyCapital]
GO

ALTER TABLE [dbo].[Company] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[Company] ADD  DEFAULT ((0)) FOR [siPeriodDef]
GO

ALTER TABLE [dbo].[Company] ADD  DEFAULT ((1)) FOR [boStatus]
GO

ALTER TABLE [dbo].[Company] ADD  DEFAULT ((0)) FOR [EtyConsol]
GO

ALTER TABLE [dbo].[Company] ADD  DEFAULT (getdate()) FOR [dtMigrated]
GO

ALTER TABLE [dbo].[Company] ADD  DEFAULT ('000000000000000') FOR [Reserved]
GO

ALTER TABLE [dbo].[Company] ADD  DEFAULT ((0)) FOR [Prefix]
GO

ALTER TABLE [dbo].[Company]  WITH CHECK ADD  CONSTRAINT [CompanyFK01] FOREIGN KEY([ID_TypeEntity])
REFERENCES [dbo].[CompaniyType] ([ID_TypeEntity])
GO

ALTER TABLE [dbo].[Company] CHECK CONSTRAINT [CompanyFK01]
GO

ALTER TABLE [dbo].[Company]  WITH CHECK ADD CHECK  ((len([Descrip])>(0)))
GO

ALTER TABLE [dbo].[Company]  WITH CHECK ADD CHECK  (([EtyCapital]=(2) OR [EtyCapital]=(1) OR [EtyCapital]=(0)))
GO

ALTER TABLE [dbo].[Company]  WITH CHECK ADD CHECK  (([EtyConsol]>=(0) AND [EtyConsol]<=(2)))
GO

ALTER TABLE [dbo].[Company]  WITH CHECK ADD CHECK  (([EtySize]=(2) OR [EtySize]=(1) OR [EtySize]=(0)))
GO

ALTER TABLE [dbo].[Company]  WITH CHECK ADD CHECK  ((len([ID_TypeEntity])<>(0)))
GO

ALTER TABLE [dbo].[Company]  WITH CHECK ADD CHECK  ((len([IDLabel])>(0)))
GO

ALTER TABLE [dbo].[Company]  WITH CHECK ADD CHECK  ((len([Reserved])=(15)))
GO


