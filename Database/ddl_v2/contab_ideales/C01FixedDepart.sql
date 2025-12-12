USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01FixedDepart]    Script Date: 10/12/2025 13:38:30 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01FixedDepart](
	[ID_DepFix] [varchar](8) NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
	[Descrip2] [varchar](80) NULL,
	[Contact] [varchar](40) NULL,
	[boStatus] [bit] NOT NULL,
 CONSTRAINT [C01FixedDepart0] PRIMARY KEY CLUSTERED 
(
	[ID_DepFix] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01FixedDepart] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[C01FixedDepart] ADD  DEFAULT ((1)) FOR [boStatus]
GO


