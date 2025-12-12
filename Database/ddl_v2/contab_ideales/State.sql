USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[State]    Script Date: 10/12/2025 14:07:08 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[State](
	[IDCountry] [varchar](4) NOT NULL,
	[IDState] [varchar](4) NOT NULL,
	[Descrip] [varchar](80) NOT NULL,
 CONSTRAINT [State0] PRIMARY KEY CLUSTERED 
(
	[IDCountry] ASC,
	[IDState] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[State] ADD  DEFAULT ('REGISTRO NUEVO') FOR [Descrip]
GO

ALTER TABLE [dbo].[State]  WITH CHECK ADD CHECK  ((len([Descrip])<>(0)))
GO

ALTER TABLE [dbo].[State]  WITH CHECK ADD CHECK  ((len([IDCountry])>(0)))
GO

ALTER TABLE [dbo].[State]  WITH CHECK ADD CHECK  ((len([IDState])>(0)))
GO


