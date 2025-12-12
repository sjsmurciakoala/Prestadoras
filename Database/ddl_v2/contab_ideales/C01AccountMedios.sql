USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[C01AccountMedios]    Script Date: 10/12/2025 13:15:23 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[C01AccountMedios](
	[ID_Account] [varchar](24) NOT NULL,
	[Format] [varchar](10) NOT NULL,
	[Concept] [varchar](10) NOT NULL,
	[TypeFormat] [varchar](1) NOT NULL,
	[TypeFormat2] [varchar](1) NOT NULL,
	[TypeFormat3] [varchar](1) NOT NULL,
	[TopAcc] [decimal](28, 2) NULL,
	[ID_ThirdLess] [varchar](30) NULL,
 CONSTRAINT [C01AccountMedios0] PRIMARY KEY CLUSTERED 
(
	[ID_Account] ASC,
	[Format] ASC,
	[Concept] ASC,
	[TypeFormat] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[C01AccountMedios] ADD  DEFAULT ((0)) FOR [TopAcc]
GO

ALTER TABLE [dbo].[C01AccountMedios]  WITH CHECK ADD CHECK  ((len([Concept])<>(0)))
GO

ALTER TABLE [dbo].[C01AccountMedios]  WITH CHECK ADD CHECK  ((len([Format])<>(0)))
GO

ALTER TABLE [dbo].[C01AccountMedios]  WITH CHECK ADD CHECK  ((len([ID_Account])<>(0)))
GO

ALTER TABLE [dbo].[C01AccountMedios]  WITH CHECK ADD CHECK  ((len([ID_ThirdLess])<>(0)))
GO

ALTER TABLE [dbo].[C01AccountMedios]  WITH CHECK ADD CHECK  ((len([TypeFormat])<>(0)))
GO

ALTER TABLE [dbo].[C01AccountMedios]  WITH CHECK ADD CHECK  ((len([TypeFormat2])<>(0)))
GO

ALTER TABLE [dbo].[C01AccountMedios]  WITH CHECK ADD CHECK  ((len([TypeFormat3])<>(0)))
GO


