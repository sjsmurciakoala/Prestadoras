USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01Thirds]    Script Date: 10/12/2025 13:47:01 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01Thirds](
	[ID_Third] [varchar](30) NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
	[Name1] [varchar](20) NULL,
	[Name2] [varchar](20) NULL,
	[LastName1] [varchar](20) NULL,
	[LastName2] [varchar](20) NULL,
	[ID_OrgThird] [varchar](30) NOT NULL,
	[Address] [varchar](40) NULL,
	[Codtax] [varchar](30) NULL,
	[TaxIdent] [smallint] NULL,
	[Address1] [varchar](40) NULL,
	[City] [varchar](30) NULL,
	[Country] [varchar](30) NULL,
	[Email] [varchar](40) NULL,
	[Phone] [varchar](25) NULL,
	[Contact] [varchar](40) NULL,
	[dtDate] [datetime] NOT NULL,
	[boStatus] [bit] NOT NULL,
	[TypeID] [smallint] NULL,
	[TypeThird] [smallint] NULL,
	[IDCountry] [varchar](4) NULL,
	[IDState] [varchar](4) NULL,
	[IDCity] [varchar](4) NULL,
	[IDMunicipality] [varchar](4) NULL,
 CONSTRAINT [C01Thirds0] PRIMARY KEY CLUSTERED 
(
	[ID_Third] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01Thirds] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[C01Thirds] ADD  DEFAULT ((0)) FOR [TaxIdent]
GO

ALTER TABLE [dbo].[C01Thirds] ADD  DEFAULT (getdate()) FOR [dtDate]
GO

ALTER TABLE [dbo].[C01Thirds] ADD  DEFAULT ((0)) FOR [boStatus]
GO

ALTER TABLE [dbo].[C01Thirds] ADD  DEFAULT ((0)) FOR [TypeID]
GO

ALTER TABLE [dbo].[C01Thirds] ADD  DEFAULT ((0)) FOR [TypeThird]
GO

ALTER TABLE [dbo].[C01Thirds]  WITH CHECK ADD CHECK  ((len([Descrip])<>(0)))
GO


