USE [MD_CONTAB]
GO

/****** Object:  Table [dbo].[CompanyJoin]    Script Date: 10/12/2025 13:56:34 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[CompanyJoin](
	[ID_Entity] [varchar](10) NOT NULL,
	[ID_SubEntity] [varchar](10) NOT NULL,
	[NLine] [int] IDENTITY(1,1) NOT NULL,
	[Status] [smallint] NOT NULL,
 CONSTRAINT [CompanyJoin0] PRIMARY KEY CLUSTERED 
(
	[ID_Entity] ASC,
	[ID_SubEntity] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[CompanyJoin] ADD  DEFAULT ((1)) FOR [Status]
GO

ALTER TABLE [dbo].[CompanyJoin]  WITH CHECK ADD  CONSTRAINT [CompanyJoinFK01] FOREIGN KEY([ID_Entity])
REFERENCES [dbo].[Company] ([ID_Entity])
GO

ALTER TABLE [dbo].[CompanyJoin] CHECK CONSTRAINT [CompanyJoinFK01]
GO

ALTER TABLE [dbo].[CompanyJoin]  WITH CHECK ADD CHECK  (([Status]>=(0) AND [Status]<=(3)))
GO


