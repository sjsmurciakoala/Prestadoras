USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[Currency]    Script Date: 10/12/2025 13:57:43 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Currency](
	[ID_Currency] [varchar](3) NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
	[Country] [varchar](30) NOT NULL,
	[Symbol] [varchar](3) NOT NULL,
	[boStatus] [bit] NOT NULL,
 CONSTRAINT [Currency0] PRIMARY KEY CLUSTERED 
(
	[ID_Currency] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[Currency] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[Currency] ADD  DEFAULT ((1)) FOR [boStatus]
GO


