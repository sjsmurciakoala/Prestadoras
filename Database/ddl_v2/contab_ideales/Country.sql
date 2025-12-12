USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[Country]    Script Date: 10/12/2025 13:57:12 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Country](
	[IDCountry] [varchar](4) NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
	[SCountry] [varchar](30) NULL,
	[SState] [varchar](30) NULL,
	[SCity] [varchar](30) NULL,
	[SMunicipality] [varchar](30) NULL,
 CONSTRAINT [Country0] PRIMARY KEY CLUSTERED 
(
	[IDCountry] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[Country] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[Country]  WITH CHECK ADD CHECK  ((len([Descrip])<>(0)))
GO

ALTER TABLE [dbo].[Country]  WITH CHECK ADD CHECK  ((len([IDCountry])>(0)))
GO


